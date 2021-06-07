using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
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
				Bitmap bmp = new Bitmap(files[i]);
				Color[] colours = new Color[3];
				if (IsUncompressedApproach)
					colours = GetColoursFromImage(bmp, BucketAmount);
				else
					colours[0] = bmp.GetPixel(0, 0);

				ColoursFromFiles.Add(colours);

				if (i % splitcount == 0 && i != 0)
				{
					stopwatch.Stop();
					Console.WriteLine("[" + DateTime.Now.ToString() + "] Thread{0}: Processed image: {1}/{2}", ThreadID, IsUncompressedApproach ? i : i / xframe, IsUncompressedApproach ? files.Length : files.Length / xframe);
					ts = stopwatch.Elapsed;

					Console.WriteLine("[" + DateTime.Now.ToString() + "] Thread{0}: Time elapsed analysing last {1} images: {2:00}:{3:00}:{4:00}.{5}",
									ThreadID, splitcount, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);

					GC.Collect();
					stopwatch.Reset();
					stopwatch.Start();
				}
			}

			return ColoursFromFiles;
		}

		internal Color[] GetColoursFromImage(Bitmap bmp, int BucketAmount)
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
			List<int>[] buckets = new List<int>[BucketAmount];
			for (int i = 0; i < BucketAmount; i++)
				buckets[i] = new List<int>();

			for (int x = 0; x < bmp.Width; x++)
			{
				for (int y = 0; y < bmp.Height; y++)
				{
					Color c = bmp.GetPixel(x, y);

					// Avg
					r += c.R;
					g += c.G;
					b += c.B;
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
			Color AverageColour = Color.FromArgb(r, g, b);
			ReturnColours[0] = AverageColour;


			// Frequency
			Color MostFrequentColor = FrequencyColourDictionary.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
			ReturnColours[1] = MostFrequentColor;

			// Bucket
			for (int i = 0; i < BucketColours.Count(); i++)
			{
				int bucket = NormaliseToBucketIndex(BucketAmount, BucketColours[i].ToArgb());
				buckets[bucket].Add(BucketColours[i].ToArgb());
			}
			List<int> fullestbucket = buckets.Aggregate((l, r) => l.Count > r.Count ? l : r);
			ReturnColours[2] = GetAvgColourFromBucket(fullestbucket);


			return ReturnColours;
		}

		internal Color GetWeightedAverageColor(int BucketAmount, Bitmap bmp)
		{
			List<int>[] buckets = new List<int>[BucketAmount];
			for (int i = 0; i < BucketAmount; i++)
				buckets[i] = new List<int>();

			List<Color> tmpcolours = new List<Color>();
			for (int x = 0; x < bmp.Width; x++)
				for (int y = 0; y < bmp.Height; y++)
					tmpcolours.Add(bmp.GetPixel(x, y));

			int[] colours = new int[bmp.Width * bmp.Height];

			for (int i = 0; i < tmpcolours.Count(); i++)
				colours[i] = tmpcolours[i].ToArgb();

			for (int i = 0; i < colours.Length; i++)
			{
				int bucket = NormaliseToBucketIndex(BucketAmount, colours[i]);
				buckets[bucket].Add(colours[i]);
			}

			var fullestbucket = buckets.Aggregate((l, r) => l.Count > r.Count ? l : r);

			Color colour = GetAvgColourFromBucket(fullestbucket);

			return colour;
		}

		internal Color getMostFrequentColour(Bitmap bmp)
		{
			Dictionary<Color, int> usedColours = new Dictionary<Color, int>();

			for (int x = 0; x < bmp.Width; x++)
			{
				for (int y = 0; y < bmp.Height; y++)
				{
					Color c = bmp.GetPixel(x, y);

					if (usedColours.ContainsKey(c))
						usedColours[c] = usedColours[c]++;
					else
						usedColours.Add(c, 1);
				}
			}
			var max = usedColours.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;

			return max;
		}

		internal Color GetAverageColor(Bitmap bmp)
		{
			//Used for tally
			int r = 0;
			int g = 0;
			int b = 0;

			int total = 0;

			for (int x = 0; x < bmp.Width; x++)
			{
				for (int y = 0; y < bmp.Height; y++)
				{
					Color clr = bmp.GetPixel(x, y);

					r += clr.R;
					g += clr.G;
					b += clr.B;

					total++;
				}
			}

			//Calculate average
			r /= total;
			g /= total;
			b /= total;

			return Color.FromArgb(r, g, b);
		}

		internal Bitmap CreateBarcodeImageFromColours(List<Color> colors, int Width, int Height)
		{
			Bitmap FinalImage = new Bitmap(Width, Height);

			for (int i = 0; i < FinalImage.Width; i++)
			{
				for (int j = 0; j < FinalImage.Height; j++)
				{
					FinalImage.SetPixel(i, j, colors[i]);
				}
			}

			return FinalImage;
		}

		internal async Task ExtractEveryXthFrame(string FilePath, int XthFrame, Func<string, string> OutputFileNameBuilder, VideoCodec VC, bool IsUncompressedApproach = true)
		{
			IMediaInfo info = await FFmpeg.GetMediaInfo(FilePath).ConfigureAwait(false);
			IVideoStream videoStream = info.VideoStreams.First()?.SetCodec(VideoCodec.png);

			Console.WriteLine("[" + DateTime.Now.ToString() + "] Extracting every " + XthFrame + "-th frame");

			IConversion conversion = FFmpeg.Conversions.New()
				.AddStream(videoStream);

			if (!IsUncompressedApproach)
			{
				conversion.AddParameter("-vf scale=1:1")
				.AddParameter("-sws_flags sinc");
			}

			conversion.UseHardwareAcceleration(HardwareAccelerator.d3d11va, VC, VideoCodec.png)
				.ExtractEveryNthFrame(XthFrame, OutputFileNameBuilder);

			conversion.OnProgress += (sender, args) =>
			{
				var percent = (int)(Math.Round(args.Duration.TotalSeconds / args.TotalLength.TotalSeconds, 2) * 100);
				Console.WriteLine($"[{args.Duration} / {args.TotalLength}] {percent}%");
			};

			await conversion.Start();
		}
		
		/// <summary>
		 /// Resize the image to the specified width and height.
		 /// </summary>
		 /// <param name="image">The image to resize.</param>
		 /// <param name="width">The width to resize to.</param>
		 /// <param name="height">The height to resize to.</param>
		 /// <returns>The resized image.</returns>
		internal Bitmap ResizeImage(Bitmap image, int width, int height)
		{
			var destRect = new Rectangle(0, 0, width, height);
			var destImage = new Bitmap(width, height);

			destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

			using (var graphics = Graphics.FromImage(destImage))
			{
				graphics.CompositingMode = CompositingMode.SourceCopy;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

				using (var wrapMode = new ImageAttributes())
				{
					wrapMode.SetWrapMode(WrapMode.TileFlipXY);
					graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
				}
			}

			return destImage;
		}

		private Color GetAvgColourFromBucket(List<int> bucket)
		{
			//Used for tally
			int r = 0;
			int g = 0;
			int b = 0;

			int total = 0;

			r += Color.FromArgb(bucket[0]).R;
			r += Color.FromArgb(bucket[bucket.Count - 1]).R;
			g += Color.FromArgb(bucket[0]).G;
			g += Color.FromArgb(bucket[bucket.Count - 1]).G;
			b += Color.FromArgb(bucket[0]).B;
			b += Color.FromArgb(bucket[bucket.Count - 1]).B;

			total = 2;

			//for (int x = 0; x < bucket.Count; x++)
			//{
			//	Color clr = Color.FromArgb(bucket[x]);

			//	r += clr.R;
			//	g += clr.G;
			//	b += clr.B;

			//	total++;
			//}

			//Calculate average
			r /= total;
			g /= total;
			b /= total;

			return Color.FromArgb(r, g, b);
		}

		private int NormaliseToBucketIndex(int BucketAmount, int x)
		{
			int a = 0;
			int b = BucketAmount-1;
			int min = -16777216;
			int max = -1;
			long y = (long)(b - a) * (long)(x - min);
			int ret = (int)(y / (max - min));
			//Console.WriteLine(x + " -> " + ret);
			return ret;
		}
	}
}
