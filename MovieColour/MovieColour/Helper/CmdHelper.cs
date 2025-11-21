using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MovieColour.Helper
{
    internal static class CmdHelper
    {
        #region Execute Commands

        /// <summary>
        /// Run a command and return the stdout as a byte array
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal static byte[] RunCommandAndGetStdoutAsByteArray(string fileName, string command)
        {
            Log.Logger.Verbose("Running command: {FileName} {Command}", fileName, command);
            var startInfo = GetCustomFileProcessStartInfo(fileName, command);

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            var stdErrBuilder = new StringBuilder();

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    stdErrBuilder.AppendLine(e.Data);
                }
            };

            process.Start();

            process.BeginErrorReadLine();

            using var ms = new MemoryStream();
            var outputStream = process.StandardOutput.BaseStream as FileStream;

            var buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = outputStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, bytesRead);
            }

            // Wait for ffmpeg to exit
            process.WaitForExit();

            // Optionally check the exit code; if non-zero, examine stdErrBuilder
            if (process.ExitCode != 0)
            {
                var errorLog = stdErrBuilder.ToString();
                Log.Logger.Error(Strings.CommandThrewError0, command);
                Log.Logger.Error(errorLog);
            }

            Log.Logger.Verbose("Command executed successfully");
            return ms.ToArray();
        }
        
        internal static async Task RunFfmpegWithTimeProgressAsync(string fileName, string arguments, int totalSeconds, IProgress<int>? progress, CancellationToken cancellationToken = default)
        {
            var si = GetCustomFileProcessStartInfo(fileName, arguments);
            using var p = new Process { StartInfo = si, EnableRaisingEvents = false };

            p.Start();

            // We only need stderr for progress, but keep stdout redirected for completeness.
            var stderr = p.StandardError;
            // ffmpeg writes "key=value" lines when -progress pipe:2 is used.
            // scan for out_time or out_time_ms.
            string? line;
            while ((line = await stderr.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // out_time=HH:MM:SS.micro
                if (line.StartsWith("out_time=") && totalSeconds > 0)
                {
                    var t = line.Substring("out_time=".Length);
                    if (TimeSpan.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var ts))
                    {
                        var pct = (int)Math.Clamp(ts.TotalSeconds / totalSeconds * 100.0, 0.0, 100.0);
                        progress?.Report(pct);
                    }
                }
                // out_time_ms is actually microseconds; divide by 1_000_000
                else if (line.StartsWith("out_time_ms=") && totalSeconds > 0)
                {
                    if (double.TryParse(line.AsSpan("out_time_ms=".Length), out var micros))
                    {
                        var seconds = micros / 1_000_000.0;
                        var pct = (int)Math.Clamp(seconds / totalSeconds * 100.0, 0.0, 100.0);
                        progress?.Report(pct);
                    }
                }
                else if (line == "progress=end")
                {
                    progress?.Report(100);
                }
            }

            await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (p.ExitCode != 0)
                Log.Logger.Error("ffmpeg exited with {ExitCode}", p.ExitCode);
        }

        
        internal static async Task<string> RunCommandAndGetStdoutAsStringAsync(string fileName, string command, bool returnStdErrInstead = false, CancellationToken cancellationToken = default)
        {
            Log.Logger.Verbose("Running command: {FileName} {Command}", fileName, command);
            var startInfo = GetCustomFileProcessStartInfo(fileName, command);

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = false };

            process.Start();

            // Read both streams asynchronously.
            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var output = await stdOutTask.ConfigureAwait(false);
            var error  = await stdErrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                Log.Logger.Error(Strings.CommandThrewError0, command);
                Log.Logger.Error(error);
            }

            Log.Logger.Verbose("Command executed successfully");
            return returnStdErrInstead ? error : output;
        }


        /// <summary>
        /// Run a command and return the stdout as a string
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="command"></param>
        /// <param name="returnStdErrInstead"></param>
        /// <returns></returns>
        internal static string RunCommandAndGetStdoutAsString(string fileName, string command, bool returnStdErrInstead = false)
        {
            Log.Logger.Verbose("Running command: {FileName} {Command}", fileName, command);
            var startInfo = GetCustomFileProcessStartInfo(fileName, command);

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var stdOutBuilder = new StringBuilder();
            var stdErrBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    stdOutBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    stdErrBuilder.AppendLine(e.Data);
                }
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            var output = stdOutBuilder.ToString();
            var error = stdErrBuilder.ToString();

            // Check the exit code; if non-zero, examine stdErrBuilder
            if (process.ExitCode != 0)
            {
                Log.Logger.Error(Strings.CommandThrewError0, command);
                Log.Logger.Error(error);
            }

            Log.Logger.Verbose("Command executed successfully");
            return returnStdErrInstead ? error : output;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Checks if the given executable is available on the system by trying to run it with a version argument
        /// </summary>
        /// <param name="exeName"></param>
        /// <param name="argument"></param>
        /// <returns>True, if process exited with code 0, else false and logs error </returns>
        internal static bool IsExecutableAvailable(string exeName, string argument = "-version")
        {
            try
            {
                var startInfo = GetCustomFileProcessStartInfo(exeName, argument);

                using var process = Process.Start(startInfo);
                process.WaitForExit(3000);
                return process.ExitCode == 0;
            }
            catch (Exception e)
            {
                // If an exception is thrown (for example, if the file isn't found),
                // the executable is not available.
                // ToDo #3 - Ensure style/call matches the rest
                Log.Logger.Error(e.Message, Strings.ErrWhileCheckCmdAvailable, exeName);
                return false;
            }
        }


        /// <summary>
        /// Split a byte array into chunks of a specified size
        /// This is mainly used to split the output of FFmpeg (which are BMP files) into individual frames (byte arrays)
        /// </summary>
        /// <param name="stdout"></param>
        /// <param name="chunkSize"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal static byte[][] SplitStdoutByChunk(byte[] stdout, int chunkSize)
        {
            if (stdout == null || stdout.Length == 0)
                throw new Exception("stdout is null or empty"); // ToDo #12

            if (stdout.Length % chunkSize != 0)
                throw new Exception("stdout length is not a multiple of chunk size"); // ToDo #12

            var chunks = new byte[stdout.Length / chunkSize][];
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i] = new byte[chunkSize];
                Array.Copy(stdout, i * chunkSize, chunks[i], 0, chunkSize);
            }

            return chunks;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Get the default ProcessStartInfo for running a command for the given file (e.g. FFmpeg)
        /// Can also be used to run a command in the command prompt (fileName = "cmd.exe", command starting with "/C ")
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        private static ProcessStartInfo GetCustomFileProcessStartInfo(string fileName, string command)
        {
            return new ProcessStartInfo
            {
                Verb = "runas",
                FileName = fileName,
                Arguments = command,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        #endregion
    }
}
