using System;
using System.Diagnostics;
using System.IO;

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

        #endregion

        #region FFmpeg Commands

        /// <summary>
        /// Get the command to determine the crop from a video file
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <returns></returns>
        internal static string GetCropCommand(string fullFilePath)
        {
            // -ss 90: start at 90 seconds
            // is an issue if the video is shorter than 90 seconds
            // ToDo: #31 - Check for video length to adjust crop check
            return $"ffmpeg -hide_banner -i {fullFilePath} -ss 10 -vframes 10 -vf cropdetect -f null -";
        }

        /// <summary>
        /// Get the command to convert a video file to a specified scale
        /// Crop is specified as string and expected in the format "width:height" (e.g. "1920:1072")
        /// Scale is specified as int and expected in the format "height" (e.g. 360 (p))
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <param name="crop">Expected format: "width:height", e.g. "1920:1072"</param>
        /// <param name="scale">Expected format: "height", e.g. 360</param>
        /// <param name="outputFilePath"></param>
        /// <returns></returns>
        internal static string GetConvertCommand(string fullFilePath, string crop, int scale, string outputFilePath) => 
            $"ffmpeg -hide_banner -i {fullFilePath} -map 0:v:0 -vf crop={crop},scale=-2:{scale} -sws_flags sinc {outputFilePath}";

        /// <summary>
        /// Get the command to get the frame rate of a video file
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <returns></returns>
        internal static string GetGetFpsCommand(string fullFilePath) => 
            $"ffprobe {fullFilePath} -v 0 -select_streams v:0 -print_format flat -show_entries stream=avg_frame_rate";

        /// <summary>
        /// Get the command to get the first frame of a video file which will be output as a byte representation of a BMP file to stdout
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <returns></returns>
        internal static string GetSingleFrameCommand(string fullFilePath) =>
            $"ffmpeg -hide_banner -i {fullFilePath} -frames:v 1 -c:v bmp -f image2pipe -";

        /// <summary>
        /// Get the command to get X frames from a video file with the given offset which will be output as a byte representation of BMP files to stdout
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <param name="offsetInSeconds"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        internal static string GetXFramesCommand(string fullFilePath, double offsetInSeconds, int x) =>
            $"ffmpeg -hide_banner -ss {offsetInSeconds}s -i {fullFilePath} -frames:v {x} -c:v bmp -f image2pipe -";

        #endregion

        #region Private Methods

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
                RedirectStandardError = true
            };
        }

        #endregion
    }
}
