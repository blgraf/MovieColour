using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieColour.Helper
{
	internal class Enums
	{
		internal enum AnalysisMethod
		{
			BucketAvgMinMax,
			BucketAvgTotal,
			BucketMedian,
			FrameAvg,
			FrameMedian,
			FrameMostFrequent
		}
	}
}
