using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace MovieColour
{
	internal class ImageHelper
	{
		internal List<Color[]> GetColoursFromFiles(string[] files, bool IsUncompressedApproach, int splitcount, int xframe, int BucketAmount, int ThreadID = 0)
		{
			List<Color[]> ColoursFromFiles = new List<Color[]>();
			Stopwatch stopwatch = new Stopwatch();
			TimeSpan ts;

			stopwatch.Start();
			for (int i = 0; i < files.Length; i++)
			{
				if (!IsUncompressedApproach && i % xframe != 0)
					continue;
				Image<Argb32> img = Image.Load<Argb32>(files[i]);
				Color[] colours = new Color[3];
				if (IsUncompressedApproach)
					colours = GetColoursFromImage(img, BucketAmount);
				else
					colours[0] = img[0, 0];

				ColoursFromFiles.Add(colours);

				if (i % splitcount == 0 && i != 0)
				{
					stopwatch.Stop();
					int x = IsUncompressedApproach ? i : (i / xframe);
					int y = IsUncompressedApproach ? files.Length : (files.Length / xframe);
					float p = ((float)x / (float)y) * 100;
					string msg = string.Format("Processed image: {0}/{1} [{2:00}%]", x, y, p);
					Logger.WriteLogMessage(msg, ThreadID);

					ts = stopwatch.Elapsed;
					Logger.WriteElapsedTime(string.Format("analysing last {0} images", splitcount), ts, ThreadID);

					GC.Collect();
					stopwatch.Reset();
					stopwatch.Start();
				}
			}

			return ColoursFromFiles;
		}

		internal Color[] GetColoursFromImage(Image<Argb32> img, int BucketAmount)
		{
			// 0: AvgClr
			// 1: FrqClr
			// 2: BktClr
			Color[] ReturnColours = new Color[3];

			// Avg
			int r, g, b, total;
			r = g = b = total = 0;

			// Frequency
			Dictionary<Color, int> FrequencyColourDictionary = new Dictionary<Color, int>();

			// Bucket
			List<Color> BucketColours = new List<Color>();
			List<Color>[,,] buckets = new List<Color>[BucketAmount, BucketAmount, BucketAmount];
			//List<Color>[] buckets = new List<Color>[BucketAmount];
			//for (int i = 0; i < buckets.Length; i++)
			//	buckets[i] = new List<Color>();

			for (int x = 0; x < img.Width; x++)
			{
				for (int y = 0; y < img.Height; y++)
				{
					Color c = img[x, y];

					// Avg
					r += c.ToPixel<Argb32>().R;
					g += c.ToPixel<Argb32>().G;
					b += c.ToPixel<Argb32>().B;
					total++;


					// Frequency
					if (FrequencyColourDictionary.ContainsKey(c))
						FrequencyColourDictionary[c] = FrequencyColourDictionary[c]++;
					else
						FrequencyColourDictionary.Add(c, 1);

					// Bucket
					BucketColours.Add(c);
				}
			}

			// Avg
			r /= total;
			g /= total;
			b /= total;
			Color AverageColour = Color.FromRgb((byte)r, (byte)g, (byte)b);
			ReturnColours[0] = AverageColour;


			// Frequency
			Color MostFrequentColor = FrequencyColourDictionary.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
			ReturnColours[1] = MostFrequentColor;

			// Bucket
			//for (int i = 0; i < BucketColours.Count(); i++)
			//{
			//	int bucket = NormaliseToBucketIndex(BucketAmount, BucketColours[i]);
			//	buckets[bucket].Add(BucketColours[i]);
			//}
			foreach (Color c in BucketColours)
			{
				var pxl = c.ToPixel<Argb32>();
				var red = (int)Math.Floor(pxl.R * BucketAmount / Math.Pow(2, 8));
				var grn = (int)Math.Floor(pxl.G * BucketAmount / Math.Pow(2, 8));
				var blu = (int)Math.Floor(pxl.B * BucketAmount / Math.Pow(2, 8));

				if (buckets[red, grn, blu] == null)
					buckets[red, grn, blu] = new List<Color>();
				buckets[red, grn, blu].Add(c);

			}
			List<Color> fullestbucket = FindFullestBucket(buckets);

			//List<Color> fullestbucket = buckets.Aggregate((l, r) => l.Count > r.Count ? l : r);
			ReturnColours[2] = GetColourFromBucket(fullestbucket, MyEnums.BucketColorSelectionMode.AvgMinMax);


			return ReturnColours;
		}

		internal Color GetWeightedAverageColor(Image<Argb32> img, int BucketAmount)
		{
			List<Color>[] buckets = new List<Color>[BucketAmount];
			for (int i = 0; i < BucketAmount; i++)
				buckets[i] = new List<Color>();

			List<Color> tmpcolours = new List<Color>();
			for (int x = 0; x < img.Width; x++)
				for (int y = 0; y < img.Height; y++)
					tmpcolours.Add(img[x, y]);

			for (int i = 0; i < tmpcolours.Count; i++)
			{
				int bucket = NormaliseToBucketIndex(BucketAmount, tmpcolours[i]);
				buckets[bucket].Add(tmpcolours[i]);
			}

			var fullestbucket = buckets.Aggregate((l, r) => l.Count > r.Count ? l : r);

			Color colour = GetColourFromBucket(fullestbucket, MyEnums.BucketColorSelectionMode.AvgTotal);

			return colour;
		}

		internal Color getMostFrequentColour(Image<Argb32> img)
		{
			Dictionary<Color, int> usedColours = new Dictionary<Color, int>();

			for (int x = 0; x < img.Width; x++)
			{
				for (int y = 0; y < img.Height; y++)
				{
					Argb32 pixel = img[x, y];

					if (usedColours.ContainsKey(pixel))
						usedColours[pixel] = usedColours[pixel]++;
					else
						usedColours.Add(pixel, 1);
				}
			}
			var max = usedColours.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;

			return max;
		}

		internal Color GetAverageColor(Image<Argb32> img)
		{
			//Used for tally
			int r = 0;
			int g = 0;
			int b = 0;

			int total = 0;

			for (int x = 0; x < img.Width; x++)
			{
				for (int y = 0; y < img.Height; y++)
				{
					Argb32 pixel = img[x, y];

					r += pixel.R;
					g += pixel.G;
					b += pixel.B;

					total++;
				}
			}

			//Calculate average
			r /= total;
			g /= total;
			b /= total;

			return Color.FromRgb((byte)r, (byte)g, (byte)b);
		}

		internal Image CreateBarcodeImageFromColours(List<Color> colors, int Width, int Height)
		{
			var image = new Image<Argb32>(Width, Height);
			for (int x = 0; x < image.Width; x++)
				for (int y = 0; y < image.Height; y++)
					image[x, y] = colors[x];

			return image;
		}

		internal async Task ConvertToScale(string FilePath, string Scale, string OutputPath)
		{
			Logger.WriteLogMessage("Checking if cropping is necessary...");

			IMediaInfo info = await FFmpeg.GetMediaInfo(FilePath).ConfigureAwait(false);
			IVideoStream videoStream = info.VideoStreams.First();

			string crop = await GetCropFromStreamAsync(videoStream);
			int progress = -1;

			IConversion conversion = FFmpeg.Conversions.New()
				.AddStream(videoStream)
				.AddParameter("-vf " + crop + "scale=" + Scale)
				.AddParameter("-sws_flags sinc")
				.SetOutput(OutputPath);

			conversion.OnProgress += (sender, args) =>
			{
				var percent = (int)(Math.Round(args.Duration.TotalSeconds / args.TotalLength.TotalSeconds, 2) * 100);
				if (percent > progress)
				{
					Logger.WriteLogMessage($"[{args.Duration} / {args.TotalLength}] {percent}%");
					progress = percent;
				}
			};

			Logger.WriteLogMessage("Cropping the video if necessary and converting to " + Scale);

			await conversion.Start();
		}

		internal async Task ExtractEveryXthFrame(string FilePath, int XthFrame, Func<string, string> OutputFileNameBuilder, bool IsUncompressed = true)
		{
			IMediaInfo info = await FFmpeg.GetMediaInfo(FilePath).ConfigureAwait(false);
			IVideoStream videoStream = info.VideoStreams.First();
			VideoCodec VC = GetVideoCodecFromString(videoStream.Codec);
			int progress = -1;

			IConversion conversion = FFmpeg.Conversions.New()
				.AddStream(videoStream);
				//.UseHardwareAcceleration(HardwareAccelerator.d3d11va, VC, VideoCodec.png);

			if (!IsUncompressed)
				conversion.AddParameter("-vf scale=1:1").AddParameter("-sws-flags sinc");

			conversion.ExtractEveryNthFrame(XthFrame, OutputFileNameBuilder);

			Logger.WriteLogMessage("Extracting every " + XthFrame + "-th frame");

			conversion.OnProgress += (sender, args) =>
			{
				var percent = (int)(Math.Round(args.Duration.TotalSeconds / args.TotalLength.TotalSeconds, 2) * 100);
				if (percent > progress)
				{
					Logger.WriteLogMessage($"[{args.Duration} / {args.TotalLength}] {percent}%");
					progress = percent;
				}
			};

			await conversion.Start();
		}

		private Color GetColourFromBucket(List<Color> bucket, MyEnums.BucketColorSelectionMode colorSelectionMode)
		{
			switch (colorSelectionMode)
			{
				case MyEnums.BucketColorSelectionMode.AvgMinMax:
					return GetAvgMinMaxFromBucket(bucket);
				case MyEnums.BucketColorSelectionMode.AvgTotal:
					return getAvgColourFromBucket(bucket);
				case MyEnums.BucketColorSelectionMode.Median:
					return getMedianColourFromBucket(bucket);
				default:
					Logger.WriteLogMessage("Encountered an error trying to get colour from bucket!");
					return Color.Black;
			}

		}

		private Color getMedianColourFromBucket(List<Color> bucket)
		{




			return Color.Black;
		}

		private Color getAvgColourFromBucket(List<Color> bucket)
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

		private Color GetAvgMinMaxFromBucket(List<Color> bucket)
		{
			//Used for tally
			int r = 0;
			int g = 0;
			int b = 0;

			int total = 0;

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

		private int NormaliseToBucketIndex(int BucketAmount, Color colour)
		{
			var pixel = colour.ToPixel<Argb32>();
			byte[] bytes = { pixel.A, pixel.R, pixel.G, pixel.B };
			if (BitConverter.IsLittleEndian)
				Array.Reverse(bytes);
			int argb = BitConverter.ToInt32(bytes);

			int a = 0;
			int b = BucketAmount - 1;
			int min = -16777216;
			int max = -1;
			long y = (long)(b - a) * (long)(argb - min);
			int ret = (int)(y / (max - min));
			//Console.WriteLine(argb + " -> " + ret);
			return ret;
		}

		private List<Color> FindFullestBucket(List<Color>[,,] buckets)
		{
			List<Color> fullest = new List<Color>();
			int max = 0;

			foreach (List<Color> bucket in buckets)
			{
				if (bucket != null && bucket.Count > max)
				{
					fullest = bucket;
					max = bucket.Count;
				}
			}

			if (fullest.Count == 0)
				fullest = buckets[0, 0, 0];

			return fullest;
		}

		private async Task<string> GetCropFromStreamAsync(IVideoStream videoStream)
		{
			VideoCodec VC = GetVideoCodecFromString(videoStream.Codec);
			string crop = "";

			IConversion CropDetectConversion = FFmpeg.Conversions.New()
				.AddStream(videoStream)
				.AddParameter("-ss 90")
				.AddParameter("-vframes 10")
				.AddParameter("-vf cropdetect")
				.AddParameter("-f null -");

			string ffmpegoutput = "";

			CropDetectConversion.OnDataReceived += (sender, args) =>
			{
				ffmpegoutput += "\n" + args.Data;
			};

			await CropDetectConversion.Start();

			var matches = Regex.Matches(ffmpegoutput, @"crop=\d{1,4}:\d[0-9]{1,4}:\d:\d[0-9]{0,4}");

			if (matches != null)
			{
				if (matches.Count > 1)
					crop = matches[matches.Count - 1].Value;
				else
					crop = matches[0].Value;
				Logger.WriteLogMessage("Crop found: " + crop);
				crop += ",";
			}

			return crop;
		}

		private VideoCodec GetVideoCodecFromString(string codec)
		{
			switch (codec)
			{
				case "h264":
					return VideoCodec.h264;
				case "hevc":
					return VideoCodec.hevc;
				default:
					return VideoCodec.h264;
			}
		}
	}
}
