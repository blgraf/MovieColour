using SixLabors.ImageSharp;

namespace MovieColour.Helper
{
    internal class FrameAnalysisResult
    {
        internal Color BucketAvgMinMaxResult { get; set; }
        internal Color BucketAvgTotalResult { get; set; }
        internal Color BucketMedianResult { get; set; }
        internal Color FrameAvgResult { get; set; }
        internal Color FrameMedianResult { get; set; }
        internal Color FrameMostFrequentResult { get; set; }
    }
}
