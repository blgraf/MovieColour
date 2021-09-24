using MovieColour.Helper;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static MovieColour.Helper.Enums;

namespace MovieColour
{
	internal class MovieColourHelper
	{
		internal IProgress<int> _Progress { get; set; }
		internal string InputFile { get; set; }

		private static ImageHelper imageHelper = new ImageHelper();

		internal async Task ConvertMovieAsync (string workingScale, string tmpPath, bool useGPU)
		{
			await imageHelper.ConvertToScale(InputFile, workingScale, tmpPath, useGPU, _Progress);
		}

		//ExtractEveryXthFrame(string FilePath, int XthFrame, Func<string, string> OutputFileNameBuilder, bool useGPU, IProgress<int> progress, bool IsUncompressed = true)

		internal async Task ExtractEveryXthFrameAsync(int X, bool useGPU)
		{
			var fi = new FileInfo(InputFile);
			string folderName = fi.DirectoryName + "\\" + fi.Name + "_frames";
			Directory.CreateDirectory(folderName);

			Func<string, string> OutputFileNameBuilder = (number) =>
			{
				string ret = folderName + "\\";
				if (number.Length > 1)
					ret += number[1..] + ".png";
				else
					ret += number + ".png";
				return ret;
			};

			await imageHelper.ExtractEveryXthFrame(InputFile, X, OutputFileNameBuilder, useGPU, _Progress);
		}

		internal async Task<Dictionary<AnalysisMethod, Color[]>> AnalyseFramesUsingMethods(string[] files, bool IsRapid, int X, int bucketCount = 0, List<AnalysisMethod> methods = null)
		{
			var pColours = new Dictionary<AnalysisMethod, Color>[files.Length / X];
			var ParallelColours = new Color[files.Length / X][];

			await Task.Run(() => {
				var totalCount = files.Length;
				var tmpCount = 0;

				Parallel.For(0, files.Length, (i) =>
				{
					// analyse
					// If is rapid the frame extraction doesn't work properly. So we skip frames and only analyse every x-th file
					bool skipFrame = IsRapid && i % X != 0;
					if (!skipFrame)
					{
						if (IsRapid)
							pColours[i / X] = imageHelper.GetColourFromSingleFileRapid(files[i]);
						else
							pColours[i] = imageHelper.GetColoursFromSingleFileUsingMethods(files[i], bucketCount, methods);
					}
					// report
					tmpCount++;
					_Progress.Report((int)Math.Ceiling(tmpCount * 100f / totalCount));
				});
			});

			var retDic = new Dictionary<AnalysisMethod, Color[]>();
			
			foreach (var method in methods)
			{
				var col = new Color[pColours.Length];
				for (int i = 0; i < pColours.Length; i++)
				{
					col[i] = pColours[i][method];
				}
				retDic.Add(method, col);
			}

			//return pColours;
			return retDic;

		}
























	}
}
