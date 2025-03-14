﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using static MovieColour.Helper.Enums;

namespace MovieColour.Helper
{
    internal class ImageHelper
	{
        #region Internal methods

        /// <summary>
        /// Converts a video file to the given scale using FFmpeg
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="scale">The full int of the height of the frame, e.g. 360 for 360p</param>
        /// <param name="outputPath"></param>
        /// <param name="useGPU">ToDo: #30 - Add support to use the GPU for video conversion</param>
        internal void ConvertToScale(string filePath, int scale, string outputPath, bool useGPU)
		{
			string crop = GetCropFromFile(filePath);

			var command = CmdHelper.GetConvertCommand(filePath, crop, scale, outputPath);

			_ = CmdHelper.RunCommandAndGetStdoutAsString(command);
		}

		/// <summary>
		/// Analyse a single frame file using the given methods
		/// Returns a FrameAnalysisResult object with the results for each method
		/// </summary>
		/// <param name="imageBytes"></param>
		/// <param name="BucketAmount"></param>
		/// <param name="methods"></param>
		/// <returns></returns>
		internal static FrameAnalysisResult GetColoursFromByteArrayUsingMethods(byte[] imageBytes, int BucketAmount, List<AnalysisMethod> methods)
		{
            // Variables needed
            var analysisResult = new FrameAnalysisResult();
            var img = Image.Load<Argb32>(imageBytes);

            // Avg
            int r, g, b, total;
			r = g = b = total = 0;
			// Most frequent
			var FrequencyColourDictionary = new Dictionary<Color, int>();
			//Bucket
			//List<Color> BucketColours = new List<Color>();
			//List<Color>[,,] buckets = new List<Color>[BucketAmount, BucketAmount, BucketAmount];

			// Analyse frame for avg colour and add colours to buckets/frequent dictionary
			for (int x = 0; x < img.Width; x++)
			{
				for (int y = 0; y < img.Height; y++)
				{
					Color c = img[x, y];

					// Avg
					if (methods.Contains(AnalysisMethod.FrameAvg))
					{
						r += c.ToPixel<Argb32>().R;
						g += c.ToPixel<Argb32>().G;
						b += c.ToPixel<Argb32>().B;
						total++;
					}

					// Most frequent
					if (methods.Contains(AnalysisMethod.FrameMostFrequent))
					{
						if (FrequencyColourDictionary.ContainsKey(c))
							FrequencyColourDictionary[c] = FrequencyColourDictionary[c]++;
						else
							FrequencyColourDictionary.Add(c, 1);
					}

					// Bucket
					//if (methods.Contains(AnalysisMethod.BucketAvgMinMax) 
					//	|| methods.Contains(AnalysisMethod.BucketAvgTotal)
					//	|| methods.Contains(AnalysisMethod.BucketMedian))
					//	BucketColours.Add(c);
				}
			}

			// Avg
			if (methods.Contains(AnalysisMethod.FrameAvg))
			{
				r /= total;
				g /= total;
				b /= total;
				Color AverageColour = Color.FromRgb((byte)r, (byte)g, (byte)b);
				analysisResult.FrameAvgResult = AverageColour;
			}

			// Most frequent
			if (methods.Contains(AnalysisMethod.FrameMostFrequent))
			{
				Color MostFrequentColor = FrequencyColourDictionary.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
				analysisResult.FrameMostFrequentResult = MostFrequentColor;
			}

			// Bucket
			//if (methods.Contains(AnalysisMethod.BucketAvgMinMax)
			//	|| methods.Contains(AnalysisMethod.BucketAvgTotal)
			//	|| methods.Contains(AnalysisMethod.BucketMedian))
			//{
			//	foreach (Color c in BucketColours)
			//	{
			//		var pxl = c.ToPixel<Argb32>();
			//		var red = (int)Math.Floor(pxl.R * BucketAmount / Math.Pow(2, 8));
			//		var grn = (int)Math.Floor(pxl.G * BucketAmount / Math.Pow(2, 8));
			//		var blu = (int)Math.Floor(pxl.B * BucketAmount / Math.Pow(2, 8));

			//		if (buckets[red, grn, blu] == null)
			//			buckets[red, grn, blu] = new List<Color>();
			//		buckets[red, grn, blu].Add(c);

			//	}
			//	List<Color> fullestbucket = FindFullestBucket(buckets);

			//	if (methods.Contains(AnalysisMethod.BucketAvgMinMax))
			//		analysisResult.BucketAvgMinMaxResult = GetAvgMinMaxFromBucket(fullestbucket);

			//	if (methods.Contains(AnalysisMethod.BucketAvgTotal))
			//		analysisResult.BucketAvgTotalResult = GetAvgMinMaxFromBucket(fullestbucket);
			//}

            return analysisResult;
		}

		/// <summary>
		/// Creates an image from the given List of colours. The image will be 1 pixel high and the same width as the List.
		/// </summary>
		/// <param name="colours"></param>
		/// <returns></returns>
		internal static Image CreateBarcodeImageFromColours(Color[] colours)
		{
			var img = new Image<Argb32>(colours.Length, 1);
			for (int x = 0; x < img.Width; x++)
				img[x, 0] = colours[x];

			return img;
		}

        /// <summary>
        /// Gets the FPS of a video file using ffprobe
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <returns>The frame rate as a double</returns>
        internal static double GetFps(string fullFilePath)
        {
            var output = CmdHelper.RunCommandAndGetStdoutAsString(CmdHelper.GetGetFpsCommand(fullFilePath));

            // This returns something like:
			// stream.0.avg_frame_rate="24000/1001"
            // From this we need a RegEx, that extracts the numbers and divides them
            var match = RegexHelper.FramerateRegex().Match(output);
            var numbers = match.Value.Split('/');
            return double.Parse(numbers[0]) / double.Parse(numbers[1]);
        }

		/// <summary>
		/// Gets the first frame of a video file as a byte array
		/// </summary>
		/// <param name="fullFilePath"></param>
		/// <returns></returns>
		internal static byte[] GetSingleFrameAsByteArray(string fullFilePath)
		{
            return CmdHelper.RunCommandAndGetStdoutAsByteArray(CmdHelper.GetSingleFrameCommand(fullFilePath));
        }

        /// <summary>
        /// Gets X frames from a video file with the given offset as a byte array
        /// </summary>
        /// <param name="fullFilePath"></param>
        /// <param name="offsetInSeconds"></param>
        /// <param name="x"></param>
        /// <param name="chunkSize">This represents the size of a single frame and is used to split the FFmpeg output</param>
        /// <returns></returns>
        internal static byte[][] GetXFrames(string fullFilePath, double offsetInSeconds, int x, int chunkSize)
		{
			var stdout = CmdHelper.RunCommandAndGetStdoutAsByteArray(CmdHelper.GetXFramesCommand(fullFilePath, offsetInSeconds, x));
			return CmdHelper.SplitStdoutByChunk(stdout, chunkSize);
        }

        #endregion

        #region Private methods

		/// <summary>
		/// Get the crop from the file using FFmpeg
		/// </summary>
		/// <param name="fullFilePath"></param>
		/// <returns></returns>
        private string GetCropFromFile(string fullFilePath)
		{
			MainWindow.logger.Information(Strings.DetectingCrop);

			var crop = String.Empty;

			var command = CmdHelper.GetCropCommand(fullFilePath);
			var result = CmdHelper.RunCommandAndGetStdoutAsString(command);

            var matches = RegexHelper.CropRegex().Matches(result);

			if (matches != null)
			{
				if (matches.Count > 1)
					crop = matches[^1].Value;
				else
					crop = matches[0].Value;
				crop += ",";
			}

			MainWindow.logger.Information(String.Format(Strings.CropFound, crop));

			return crop;
		}

		/// <summary>
		/// Find the fullest bucket in the given array of buckets
		/// </summary>
		/// <param name="buckets"></param>
		/// <returns></returns>
		private List<Color> FindFullestBucket(List<Color>[,,] buckets)
		{
			var fullestBucket = new List<Color>();
			int max = 0;

			foreach (List<Color> bucket in buckets)
			{
				if (bucket != null && bucket.Count > max)
				{
					fullestBucket = bucket;
					max = bucket.Count;
				}
			}

			if (fullestBucket.Count == 0)
				fullestBucket = buckets[0, 0, 0];

			return fullestBucket;
		}

		/// <summary>
		/// Get the average colour from the given bucket by calculating the average of the min and max colours
		/// </summary>
		/// <param name="bucket"></param>
		/// <returns></returns>
		private Color GetAvgMinMaxFromBucket(List<Color> bucket)
		{
			//Used for tally
			int r = 0;
			int g = 0;
			int b = 0;

			int total = 0;

			//bucket.Sort();

			r += bucket[0].ToPixel<Argb32>().R;
			r += bucket[bucket.Count - 1].ToPixel<Argb32>().R;
			g += bucket[0].ToPixel<Argb32>().G;
			g += bucket[bucket.Count - 1].ToPixel<Argb32>().G;
			b += bucket[0].ToPixel<Argb32>().B;
			b += bucket[bucket.Count - 1].ToPixel<Argb32>().B;

			total = 2;

			r /= total;
			g /= total;
			b /= total;

			return Color.FromRgb((byte)r, (byte)g, (byte)b);
		}

		/// <summary>
		/// Get the average colour from the given bucket by calculating the average of all colours
		/// </summary>
		/// <param name="bucket"></param>
		/// <returns></returns>
		private Color GetAvgTotalFromBucket(List<Color> bucket)
		{
			//Used for tally
			int r = 0;
			int g = 0;
			int b = 0;

			int total = 0;

			for (int x = 0; x < bucket.Count; x++)
			{
				var pixel = bucket[x].ToPixel<Argb32>();

				r += pixel.R;
				g += pixel.G;
				b += pixel.B;

				total++;
			}

			//Calculate average
			r /= total;
			g /= total;
			b /= total;

			return Color.FromRgb((byte)r, (byte)g, (byte)b);
		}

        #endregion


    }
}
