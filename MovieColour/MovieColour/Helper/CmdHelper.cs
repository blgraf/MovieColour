using System;
using System.Diagnostics;
using System.IO;

namespace MovieColour.Helper
{
    internal static class CmdHelper
    {
        /// <summary>
        /// Run a command and return the stdout as a byte array
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal static byte[] RunCommandAndGetStdoutAsByteArray(string command)
        {
            byte[] output = null;
            try
            {
                var startInfo = GetDefaultCmdProcessStartInfo(command);

                var process = Process.Start(startInfo);

                var baseStream = process.StandardOutput.BaseStream as FileStream;
                int lastRead = 0;

                using (var ms = new MemoryStream())
                {
                    var buffer = new byte[4096];
                    do
                    {
                        lastRead = baseStream.Read(buffer, 0, buffer.Length);
                        ms.Write(buffer, 0, lastRead);
                    } while (lastRead > 0);

                    output = ms.ToArray();
                }

                process.WaitForExit();

                return output;
            }
            catch
            {
                return output;
            }
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
                var startInfo = GetDefaultCmdProcessStartInfo(command);

                var process = Process.Start(startInfo);

                output = process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                return output;
            }
            catch
            {
                return output;
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
                throw new Exception("stdout is null or empty"); // ToDo

            if (stdout.Length % chunkSize != 0)
                throw new Exception("stdout length is not a multiple of chunk size"); // ToDo

            var chunks = new byte[stdout.Length / chunkSize][];
            for (int i = 0; i < chunks.Length; i++)
            {
                chunks[i] = new byte[chunkSize];
                Array.Copy(stdout, i * chunkSize, chunks[i], 0, chunkSize);
            }

            return chunks;
        }

        /// <summary>
        /// Get the default ProcessStartInfo for running a command in the command prompt
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private static ProcessStartInfo GetDefaultCmdProcessStartInfo(string command)
        {
            return new ProcessStartInfo
            {
                Verb = "runas",
                FileName = "cmd.exe",
                Arguments = "/C " + command,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false
            };
        }
    }
}
