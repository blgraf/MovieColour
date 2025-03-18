namespace MovieColour.Helper
{
    internal static class FfCmds
    {
        internal static string Ffmpeg = "ffmpeg";
        internal static string Ffprobe = "ffprobe";

        /// <summary>
        /// Get the command to determine the crop from a video file
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <returns></returns>
        internal static string FfmpegCropCommand(string fullFilePath)
            => 
            // -ss 90: start at 90 seconds
            // is an issue if the video is shorter than 90 seconds
            // ToDo: #31 - Check for video length to adjust crop check
            $"-hide_banner -i \"{fullFilePath}\" -ss 90 -vframes 10 -vf cropdetect -f null -";
        

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
        internal static string FfmpegConvertCommand(string fullFilePath, string crop, int scale, string outputFilePath) =>
            $"-hide_banner -i \"{fullFilePath}\" -map 0:v:0 -vf crop={crop},scale=-2:{scale} -sws_flags sinc \"{outputFilePath}\"";

        /// <summary>
        /// Get the command to get the frame rate of a video file
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <returns></returns>
        internal static string FfprobeGetFpsCommand(string fullFilePath) =>
            $"\"{fullFilePath}\" -v 0 -select_streams v:0 -print_format flat -show_entries stream=avg_frame_rate";

        /// <summary>
        /// Get the command to get the first frame of a video file which will be output as a byte representation of a BMP file to stdout
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <returns></returns>
        internal static string FfmpegGetSingleFrameCommand(string fullFilePath) =>
            $"-hide_banner -i \"{fullFilePath}\" -frames:v 1 -c:v bmp -f image2pipe -";

        /// <summary>
        /// Get the command to get X frames from a video file with the given offset which will be output as a byte representation of BMP files to stdout
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <param name="offsetInSeconds"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        internal static string FfmpegGetXFramesCommand(string fullFilePath, double offsetInSeconds, int x) =>
            $"-hide_banner -ss {offsetInSeconds}s -i \"{fullFilePath}\" -frames:v {x} -c:v bmp -f image2pipe -";

    }
}
