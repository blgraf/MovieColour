using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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
                // Log it, throw an exception, etc., as appropriate
                Console.WriteLine("FFmpeg returned an error:\n" + errorLog); // ToDo #3
            }

            return ms.ToArray();
        }

        /// <summary>
        /// Run a command and return the stdout as a string
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal static string RunCommandAndGetStdoutAsString(string fileName, string command, bool returnStdErrInstead = false)
        {
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
                var errorLog = stdErrBuilder.ToString();
                // Log it, throw an exception, etc., as appropriate
                Console.WriteLine("FFmpeg returned an error:\n" + errorLog); // ToDo #3
            }

            return returnStdErrInstead ? error : output;
        }

        /// <summary>
        /// Run a command and return the stdout as a string
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal static string RunCommandAndGetStdoutAsString(string command)
        {
            var output = string.Empty;
            try
            {
                var startInfo = GetCmdProcessStartInfo(command);

                var process = Process.Start(startInfo);

                output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                if (string.IsNullOrEmpty(output))
                    throw new Exception(error); // ToDo #12

                process.WaitForExit();

                return output;
            }
            catch
            {
                return output;
            }
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
                MainWindow.logger.Error(e.Message, Strings.ErrWhileCheckCmdAvailable, exeName);
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
        /// Get the default ProcessStartInfo for running a command in the command prompt
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private static ProcessStartInfo GetCmdProcessStartInfo(string command)
        {
            return new ProcessStartInfo
            {
                Verb = "runas",
                FileName = "cmd.exe",
                Arguments = $"/C {command}",
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        /// <summary>
        /// Get the default ProcessStartInfo for running a command for the given file (e.g. FFmpeg)
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
