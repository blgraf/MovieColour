using System.Text.RegularExpressions;

namespace MovieColour.Helper
{
    internal static partial class RegexHelper
    {
        [GeneratedRegex(@"crop=\d{1,4}:\d{1,4}:\d{1,4}:\d{1,4}")]
        internal static partial Regex CropRegex();

        [GeneratedRegex(@"\d+/\d+")]
        internal static partial Regex FramerateRegex();
    }
}
