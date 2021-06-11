using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		//private static string BasePath = @"C:\Users\Benschii\Desktop\asdf\";
		private static string BasePath = @"D:\movies\";
		private static string MovieFile = @"Coco.mkv";
		private static string IntermediateFile = "tmp.mkv";
		private static string IntermediateFilePath = Path.Combine(BasePath, IntermediateFile);
		private static string MovieFilePath = Path.Combine(BasePath, MovieFile);
		private static string OutputFolder = "Coco-ff-1";
		private static string OutputFolderPath = Path.Combine(BasePath, OutputFolder);
		private static int X = 1;
		private static bool EnableConversion = true;
		private static bool EnableFrameExtraction = true;
		private static bool IsUncompressedApproach = true;
		private static bool DeleteByProducts = true;
		private static string WorkingScale = "720:-2";
		private static int ThreadCount = 16;
		private static int BucketAmount = 3;
		private static List<Color[]>[] MTColours = new List<Color[]>[ThreadCount];
		private static TimeSpan TotalTimeSpan = new TimeSpan();
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
			Logger.WriteLogMessage("Creating barcode image from: " + MovieFile);
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

				if (IsUncompressedApproach)
				{
					if (EnableConversion)
					{
						await helper.ConvertToScale(MovieFilePath, WorkingScale, IntermediateFilePath);
						stopwatch.Stop();
						ts = stopwatch.Elapsed;
						TotalTimeSpan.Add(ts);
						stopwatch.Reset();
						stopwatch.Start();
						Logger.WriteElapsedTime("converting movie to " + WorkingScale, ts);
						await helper.ExtractEveryXthFrame(IntermediateFilePath, X, OutputFileNameBuilder) ;
					}
					else
					{
						if (File.Exists(IntermediateFilePath))
							await helper.ExtractEveryXthFrame(IntermediateFilePath, X, OutputFileNameBuilder);
						else
							await helper.ExtractEveryXthFrame(MovieFilePath, X, OutputFileNameBuilder);
					}
				}
				else
				{
					await helper.ExtractEveryXthFrame(MovieFilePath, X, OutputFileNameBuilder, IsUncompressedApproach);
				}

				stopwatch.Stop();
				ts = stopwatch.Elapsed;
				TotalTimeSpan.Add(ts);
				stopwatch.Reset();
				Logger.WriteElapsedTime("extracting every frame", ts);
			}
			var DI = new DirectoryInfo(OutputFolderPath);
			var files = DI.GetFiles().OrderBy(n => Regex.Replace(n.Name, @"\d+", n => n.Value.PadLeft(6, '0'))).Select(f => f.FullName).ToArray();

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

			stopwatch.Stop();
			ts = stopwatch.Elapsed;
			TotalTimeSpan.Add(ts);
			stopwatch.Reset();

			Logger.WriteElapsedTime("analysing all images", ts);

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

			Image AvgImage = helper.CreateBarcodeImageFromColours(ColoursForAvgImage, ColoursForAvgImage.Count, 200);
			Image FrqImage = helper.CreateBarcodeImageFromColours(ColoursForFrqImage, ColoursForFrqImage.Count, 200);
			Image BktImage = helper.CreateBarcodeImageFromColours(ColoursForBktImage, ColoursForBktImage.Count, 200);

			AvgImage.Mutate(x => x.Resize(1000, 200));
			FrqImage.Mutate(x => x.Resize(1000, 200));
			BktImage.Mutate(x => x.Resize(1000, 200));

			string filename = BasePath + MovieFile.Substring(0, MovieFile.Length - 4) + "-" + X;
			filename += "-" + (IsUncompressedApproach ? "ff" : "c");

			string AvgImageFilename = filename + "-AvgC.png";
			string FrqImageFilename = filename + "-FrqC.png";
			string BktImageFilename = filename + "-" + BucketAmount + "-bucket.png";

			AvgImage.SaveAsPng(AvgImageFilename);
			FrqImage.SaveAsPng(FrqImageFilename);
			BktImage.SaveAsPng(BktImageFilename);

			Image FullSizeTest = helper.CreateBarcodeImageFromColours(ColoursForBktImage, ColoursForBktImage.Count, 1);
			FullSizeTest.SaveAsPng(filename + "-FULLSIZED.png");
			FullSizeTest.Mutate(x => x.Resize(8192, 2048));
			FullSizeTest.SaveAsPng(filename + "-Wallpaper.png");

			stopwatch.Stop();
			ts = stopwatch.Elapsed;
			TotalTimeSpan.Add(ts);

			Logger.WriteElapsedTime("creating new image", ts);

			if (DeleteByProducts)
			{
				Logger.WriteLogMessage("Deleting temporary files");
				File.Delete(IntermediateFilePath);
				File.Delete(OutputFolderPath);
			}

			Logger.WriteLogMessage("\nSuccessfully finished.");
			Logger.WriteElapsedTime("doing all tasks", TotalTimeSpan);
		}

		internal static void ResCallback(int id, List<Color[]> colours)
		{
			MTColours[id] = colours;
		}

	}
}
