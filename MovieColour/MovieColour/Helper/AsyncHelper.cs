using Serilog.Core;
using System.IO;
using System.Threading.Tasks;

namespace MovieColour.Helper
{
    public static class AsyncHelper
    {
        /// <summary>
        /// Delete a file asynchronously
        /// </summary>
        /// <param name="path"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static Task DeleteFileAsync(string path, Logger logger)
        {
            return Task.Run(() =>
            {
                File.Delete(path);
                logger.Information(string.Format(Strings.DeletedFile, path));
            });
        }

    }
}
