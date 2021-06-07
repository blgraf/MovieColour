using SixLabors.ImageSharp;

namespace MovieColour.Helper
{
    internal class AnalysisResult
    {
        internal Color[] BucketAvgMinMaxResult { get; set; }
        internal Color[] BucketAvgTotalResult { get; set; }
        internal Color[] BucketMedianResult { get; set; }
        internal Color[] FrameAvgResult { get; set; }
        internal Color[] FrameMedianResult { get; set; }
        internal Color[] FrameMostFrequentResult { get; set; }

        public AnalysisResult()
        {
            BucketAvgMinMaxResult = null;
            BucketAvgTotalResult = null;
            BucketMedianResult = null;
            FrameAvgResult = null;
            FrameMedianResult = null;
            FrameMostFrequentResult = null;
        }
    }
}
