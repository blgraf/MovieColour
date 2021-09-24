using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using static MovieColour.Helper.Enums;

namespace MovieColour.Helper
{
	class ImageHelper
	{
		#region Internal methods

		internal async Task ConvertToScale(string FilePath, string Scale, string OutputPath, bool useGPU, IProgress<int> progress)
		{
			IMediaInfo info = await FFmpeg.GetMediaInfo(FilePath).ConfigureAwait(false);
			IVideoStream videoStream = info.VideoStreams.First();
			VideoCodec VC = GetVideoCodecFromString(videoStream.Codec);

			string crop = await GetCropFromStreamAsync(videoStream);

			IConversion conversion = FFmpeg.Conversions.New()
				.AddStream(videoStream)
				.AddParameter("-vf " + crop + "scale=" + Scale)
				.AddParameter("-sws_flags sinc")
				.SetOutput(OutputPath);

			if (useGPU)
				conversion.UseHardwareAcceleration(HardwareAccelerator.d3d11va, VC, VC);

			conversion.OnProgress += (sender, args) =>
			{
				var percent = (int)(args.Duration.TotalSeconds * 100 / args.TotalLength.TotalSeconds);
				progress.Report(percent);
			};

			await conversion.Start();
		}

		internal async Task ExtractEveryXthFrame(string FilePath, int XthFrame, Func<string, string> OutputFileNameBuilder, bool useGPU, IProgress<int> progress, bool IsUncompressed = true)
		{
			IMediaInfo info = await FFmpeg.GetMediaInfo(FilePath).ConfigureAwait(false);
			IVideoStream videoStream = info.VideoStreams.First();
			VideoCodec VC = GetVideoCodecFromString(videoStream.Codec);

			IConversion conversion = FFmpeg.Conversions.New()
				.AddStream(videoStream);

			if (useGPU)
				conversion.UseHardwareAcceleration(HardwareAccelerator.d3d11va, VC, VideoCodec.png);

			if (!IsUncompressed)
				conversion.AddParameter("-vf scale=1:1").AddParameter("-sws-flags sinc");

			conversion.ExtractEveryNthFrame(XthFrame, OutputFileNameBuilder);

			conversion.OnProgress += (sender, args) =>
			{
				var percent = (int)(args.Duration.TotalSeconds * 100 / args.TotalLength.TotalSeconds);
				progress.Report(percent);
			};

			await conversion.Start();
		}

		internal Dictionary<AnalysisMethod, Color> GetColourFromSingleFileRapid(string FilePath)
		{
			Image<Argb32> img = Image.Load<Argb32>(FilePath);
			var retCol = new Dictionary<AnalysisMethod, Color>();
			retCol.Add(AnalysisMethod.FrameAvg, img[0, 0]);
			return retCol;
		}

		internal Dictionary<AnalysisMethod, Color> GetColoursFromSingleFileUsingMethods(string FilePath, int BucketAmount, List<AnalysisMethod> methods)
		{
			var dictionaryRetColours = new Dictionary<AnalysisMethod, Color>();

			// Variables needed
			Image<Argb32> img = Image.Load<Argb32>(FilePath);
			// Avg
			int r, g, b, total;
			r = g = b = total = 0;
			// Most frequent
			Dictionary<Color, int> FrequencyColourDictionary = new Dictionary<Color, int>();
			//Bucket
			List<Color> BucketColours = new List<Color>();
			List<Color>[,,] buckets = new List<Color>[BucketAmount, BucketAmount, BucketAmount];

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
					if (methods.Contains(AnalysisMethod.BucketAvgMinMax) 
						|| methods.Contains(AnalysisMethod.BucketAvgTotal)
						|| methods.Contains(AnalysisMethod.BucketMedian))
						BucketColours.Add(c);
				}
			}

			// Avg
			if (methods.Contains(AnalysisMethod.FrameAvg))
			{
				r /= total;
				g /= total;
				b /= total;
				Color AverageColour = Color.FromRgb((byte)r, (byte)g, (byte)b);
				dictionaryRetColours.Add(AnalysisMethod.FrameAvg, AverageColour);
			}

			// Most frequent
			if (methods.Contains(AnalysisMethod.FrameMostFrequent))
			{
				Color MostFrequentColor = FrequencyColourDictionary.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
				dictionaryRetColours.Add(AnalysisMethod.FrameMostFrequent, MostFrequentColor);
			}

			// Bucket
			if (methods.Contains(AnalysisMethod.BucketAvgMinMax)
				|| methods.Contains(AnalysisMethod.BucketAvgTotal)
				|| methods.Contains(AnalysisMethod.BucketMedian))
			{
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

				if (methods.Contains(AnalysisMethod.BucketAvgMinMax))
					dictionaryRetColours.Add(AnalysisMethod.BucketAvgMinMax, GetAvgMinMaxFromBucket(fullestbucket));

				if (methods.Contains(AnalysisMethod.BucketAvgTotal))
					dictionaryRetColours.Add(AnalysisMethod.BucketAvgTotal, GetAvgTotalFromBucket(fullestbucket));

			}























































				return dictionaryRetColours;
		}

		internal Image CreateBarcodeImageFromColours(List<Color> colours)
		{
			var img = new Image<Argb32>(colours.Count, 1);
			for (int x = 0; x < img.Width; x++)
				img[x, 0] = colours[x];

			return img;
		}

		#endregion

		#region Private methods

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

			var matches = Regex.Matches(ffmpegoutput, @"crop=\d{1,4}:\d{1,4}:\d{1,4}:\d{1,4}");

			if (matches != null)
			{
				if (matches.Count > 1)
					crop = matches[matches.Count - 1].Value;
				else
					crop = matches[0].Value;
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

		private Color GetAvgMinMaxFromBucket(List<Color> bucket)
		{
			//Used for tally
			int r = 0;
			int g = 0;
			int b = 0;

			int total = 0;

			bucket.Sort(new SortColourHSL());

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
