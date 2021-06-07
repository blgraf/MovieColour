using MovieColour.Helper;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static MovieColour.Helper.Enums;

namespace MovieColour
{
    internal class MovieColourHelper
	{
		internal IProgress<int> Progress { get; set; }
		internal string InputFile { get; set; }

        private static readonly ImageHelper imageHelper = new();

		/// <summary>
		/// Wrapper for ImageHelper.GetFramesFromMovie. Needed? ToDo
		/// </summary>
		/// <param name="workingScale"></param>
		/// <param name="tmpPath"></param>
		/// <param name="useGPU"></param>
		/// <returns></returns>
		internal async Task ConvertMovieAsync (string workingScale, string tmpPath, bool useGPU)
		{
			await imageHelper.ConvertToScale(InputFile, workingScale, tmpPath, useGPU, Progress);
		}

		/// <summary>
		/// Analyse frames using the specified methods
		/// Currently using Parallel.For
		/// </summary>
		/// <param name="framesBytes"></param>
		/// <param name="bucketCount"></param>
		/// <param name="methods"></param>
		/// <returns></returns>
		internal async Task<AnalysisResult> AnalyseFramesUsingMethods(byte[][] framesBytes, int bucketCount = 0, List<AnalysisMethod> methods = null)
		{
			var frameAnalysisResults = new FrameAnalysisResult[framesBytes.Length];

			await Task.Run(() => {
				var totalCount = framesBytes.Length;
				var tmpCount = 0;

				Parallel.For(0, framesBytes.Length, (i) =>
                {
                    frameAnalysisResults[i] = ImageHelper.GetColoursFromByteArrayUsingMethods(framesBytes[i], bucketCount, methods);

                    // report
                    tmpCount++;
					Progress.Report((int)Math.Ceiling(tmpCount * 100f / totalCount));
				});
			});

            var analysisResult = new AnalysisResult
            {
                //BucketAvgMinMaxResult = frameAnalysisResults.Select(x => x.BucketAvgMinMaxResult).ToArray(),
                //BucketAvgTotalResult = frameAnalysisResults.Select(x => x.BucketAvgTotalResult).ToArray(),
                //BucketMedianResult = frameAnalysisResults.Select(x => x.BucketMedianResult).ToArray(),
                FrameAvgResult = frameAnalysisResults.Select(x => x.FrameAvgResult).ToArray(),
                //FrameMedianResult = frameAnalysisResults.Select(x => x.FrameMedianResult).ToArray(),
                FrameMostFrequentResult = frameAnalysisResults.Select(x => x.FrameMostFrequentResult).ToArray()
            };

			return analysisResult;
        }

	}
}
