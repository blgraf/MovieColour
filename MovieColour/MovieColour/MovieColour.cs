using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace MovieColour
{
	internal class MovieColour
	{
		private static string BasePath = @"D:\movies\";
		private static string MovieFile = @"DespicableMe3.mkv";
		private static string OutputFolder = @"gru-ff-6";
		private static string OutputFolderPath = Path.Combine(BasePath, OutputFolder);
		private static VideoCodec VC = VideoCodec.hevc; // must be the same the MovieFile uses.
		private static int X = 6;
		private static bool EnableFrameExtraction = true;
		private static bool IsUncompressedApproach = true;
		private static int ThreadCount = 12;
		private static int BucketAmount = 3;
		private static List<Color[]>[] MTColours = new List<Color[]>[ThreadCount];
		private static Func<string, string> OutputFileNameBuilder = (number) =>
			{
				string ret = OutputFolderPath + "\\";
				if (number.Length > 1)
					ret += number.Substring(1) + ".png";
				else
					ret += number + ".png";
				return ret;
			};

		internal static async Task Main(string[] args)
		{
			ImageHelper helper = new ImageHelper();
			Stopwatch stopwatch = new Stopwatch();
			TimeSpan ts = new TimeSpan();

			if (!Directory.Exists(OutputFolderPath))
			{
				Directory.CreateDirectory(OutputFolderPath);
				EnableFrameExtraction = true;
			}

			if (EnableFrameExtraction)
			{
				stopwatch.Start();
				await helper.ExtractEveryXthFrame(Path.Combine(BasePath, MovieFile), X, OutputFileNameBuilder, VC, IsUncompressedApproach);
				stopwatch.Stop();
				ts = stopwatch.Elapsed;
				stopwatch.Reset();
				Console.WriteLine("[" + DateTime.Now.ToString() + "] Time elapsed extracting every frame: {0:00}:{1:00}:{2:00}.{3}",
								ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
			}
			var di = new DirectoryInfo(OutputFolderPath);
			var files = di.GetFiles().OrderBy(n => Regex.Replace(n.Name, @"\d+", n => n.Value.PadLeft(6, '0'))).Select(f => f.FullName).ToArray();
			int splitcount = X < 6 ? 2500 : 50;

			stopwatch.Start();

			int split = (int)(files.Length / ThreadCount);

			List<string[]> SplitFiles = new List<string[]>();
			List<ThreadController> threadControllers = new List<ThreadController>();
			List<Thread> threads = new List<Thread>();

			for (int i = 0; i < ThreadCount; i++)
			{
				if (i != ThreadCount-1)
					SplitFiles.Add(files.Skip(split * i).Take(split).ToArray());
				else
					SplitFiles.Add(files.Skip(split * i).ToArray());
			}

			for (int i = 0; i < SplitFiles.Count; i++)
				threadControllers.Add(new ThreadController(i, SplitFiles[i], IsUncompressedApproach, X, BucketAmount, new ExCallback(ResCallback)));

			for (int i = 0; i < threadControllers.Count; i++)
				threads.Add(new Thread(new ThreadStart(threadControllers[i].Proc)));

			for (int i = 0; i < threads.Count; i++)
				threads[i].Start();

			for (int i = 0; i < threads.Count; i++)
				threads[i].Join();

			//List<Color> colors = helper.GetColoursFromFiles(files, IsUncompressedApproach, splitcount);
			stopwatch.Stop();
			ts = stopwatch.Elapsed;
			stopwatch.Reset();

			Console.WriteLine("\n[" + DateTime.Now.ToString() + "] Time elapsed analysing all images: {0:00}:{1:00}:{2:00}.{3}\n",
							ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);

			stopwatch.Start();

			List<Color[]> colors = new List<Color[]>();

			foreach (List<Color[]> list in MTColours)
			{
				foreach (Color[] c in list)
				{
					colors.Add(c);
				}
			}

			List<Color> ColoursForAvgImage = new List<Color>();
			List<Color> ColoursForFrqImage = new List<Color>();
			List<Color> ColoursForBktImage = new List<Color>();

			foreach (Color[] c in colors)
			{
				ColoursForAvgImage.Add(c[0]);
				ColoursForFrqImage.Add(c[1]);
				ColoursForBktImage.Add(c[2]);
			}

			Bitmap AvgImage = helper.CreateBarcodeImageFromColours(ColoursForAvgImage, ColoursForAvgImage.Count, 200);
			Bitmap FrqImage = helper.CreateBarcodeImageFromColours(ColoursForFrqImage, ColoursForFrqImage.Count, 200);
			Bitmap BktImage = helper.CreateBarcodeImageFromColours(ColoursForBktImage, ColoursForBktImage.Count, 200);

			AvgImage = helper.ResizeImage(AvgImage, 1000, 200);
			FrqImage = helper.ResizeImage(FrqImage, 1000, 200);
			BktImage = helper.ResizeImage(BktImage, 1000, 200);

			string filename = BasePath + MovieFile.Substring(0, MovieFile.Length - 4) + "-" + X;
			filename += "-" + (IsUncompressedApproach ? "ff" : "c");

			string AvgImageFilename = filename + "-AvgC.png";
			string FrqImageFilename = filename + "-FrqC.png";
			string BktImageFilename = filename + "-" + BucketAmount + "-bucket.png";

			AvgImage.Save(AvgImageFilename, ImageFormat.Png);
			FrqImage.Save(FrqImageFilename, ImageFormat.Png);
			BktImage.Save(BktImageFilename, ImageFormat.Png);

			stopwatch.Stop();
			ts = stopwatch.Elapsed;

			Console.WriteLine("[" + DateTime.Now.ToString() + "] Time elapsed creating new image: {0:00}:{1:00}:{2:00}.{3}",
							ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);

			Console.WriteLine("Successfully finished.");
		}

		internal static void ResCallback(int id, List<Color[]> colours)
		{
			MTColours[id] = colours;
		}

	}
}
