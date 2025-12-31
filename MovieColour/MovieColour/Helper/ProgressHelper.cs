using System;
using System.Diagnostics;
using System.Windows.Controls;

namespace MovieColour.Helper;

internal static class ProgressHelper
{
    internal static (IProgress<int> progress, Stopwatch stopwatch) CreateProgressHandler(ProgressBar bar, Label label)
    {
        var watch = new Stopwatch();
        var progress = new Progress<int>(percent =>
        {
            bar.Value = percent;
                
            if (percent <= 0)
                return;
                
            // Show final elapsed time
            if (percent >= 100)
            {
                var ts = new TimeSpan(watch.ElapsedTicks);
                label.Content = $"{Strings.Elapsed}: {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
                return;
            }

            var estRemaining = GetEstRemainingSecondsForProgress(new TimeSpan(watch.ElapsedTicks), percent);
            string etaLabel;
            // if ETA is over one minute, show in minutes, else in seconds
            if (estRemaining > 60)
                etaLabel = string.Format(Strings.XMin, (int)(estRemaining / 60));
            else
                etaLabel = string.Format(Strings.XSec, (int)estRemaining);
            label.Content = etaLabel;
                
        });

        watch.Start();
        return (progress, watch);
    }

    /// <summary>
    /// Calculates the estimated remaining time based on the elapsed TimeSpan and percent completed
    /// </summary>
    /// <param name="ts"></param>
    /// <param name="percentCompleted"></param>
    /// <returns></returns>
    private static double GetEstRemainingSecondsForProgress(TimeSpan ts, double percentCompleted)
    {
        var estTotal = ts.TotalSeconds * 100.0 / percentCompleted; // TotalElapsed * 100/percent (turning xx% back into 0.xx)
        var estRemaining = estTotal - ts.TotalSeconds; // TotalEstimated - TotalElapsed
            
        return estRemaining;
    }
}