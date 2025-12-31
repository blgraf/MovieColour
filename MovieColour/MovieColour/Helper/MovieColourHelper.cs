using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static MovieColour.Helper.Enums;

namespace MovieColour.Helper
{
    internal class MovieColourHelper
	{
		internal IProgress<int> Progress { get; set; }

		/// <summary>
		/// Analyse frames using the specified methods
		/// Currently using Parallel.For
		/// </summary>
		/// <param name="framesBytes"></param>
		/// <param name="bucketCount"></param>
		/// <param name="methods"></param>
		/// <param name="ct"></param>
		/// <returns></returns>
		internal async Task<AnalysisResult> AnalyseFramesUsingMethods(byte[][] framesBytes, int bucketCount = 0, List<AnalysisMethod> methods = null, CancellationToken ct = default)
		{
			var frameAnalysisResults = new FrameAnalysisResult[framesBytes.Length];

			await Task.Run(() => {
				var totalCount = framesBytes.Length;
				var done = 0;
				var sw = Stopwatch.StartNew();

				Parallel.For(0, totalCount, new ParallelOptions { CancellationToken = ct }, i =>
                {
                    frameAnalysisResults[i] = ImageHelper.GetColoursFromByteArrayUsingMethods(framesBytes[i], bucketCount, methods);
                    
                    // thread-safe increment
                    var current = Interlocked.Increment(ref done);
                    // throttle: ~2 updates/sec, and always report 100% at the end
                    if (sw.ElapsedMilliseconds >= 500 || current == totalCount)
                    {
	                    // report
	                    Progress?.Report((int)Math.Ceiling(current * 100f / totalCount));
	                    sw.Restart();
                    }
				});
			}, ct).ConfigureAwait(false);

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
