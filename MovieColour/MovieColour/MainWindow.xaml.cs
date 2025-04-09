using Microsoft.Win32;
using MovieColour.Helper;
using MovieColour.Views;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static MovieColour.Helper.Enums;

namespace MovieColour
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private List<FileInfo> files;
        internal CancellationTokenSource cancellationTokenSource = new();

        public MainWindow()
        {
            InitializeComponent();

            RchTxtBxLog.TextChanged += RchTextBxLog_TextChanged;

            SetDefaultValues();

            Log.Logger = new LoggerConfiguration()
                .WriteTo.RichTextBox(RchTxtBxLog)
                .MinimumLevel.Information()
                .CreateLogger();

            Loaded += MainWindow_Loaded;
        }

        #region EventHandlers

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CheckFfmpegAvailability();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource.Cancel();
            this.BtnStart.IsEnabled = true;
            this.BtnCancel.IsEnabled = false;
        }

        private void BrnResetToDefault_OnClick(object sender, RoutedEventArgs e)
        {
            SetDefaultValues();
            Log.Logger.Information(Strings.RestoredDefaultValues);
        }

        private void CkBxShowOutputLog_OnClick(object sender, RoutedEventArgs e)
        {
            if (this.StackPanelLog.Visibility == Visibility.Collapsed)
            {
                this.StackPanelLog.Visibility = Visibility.Visible;
                this.Height += 300;
            }
            else
            {
                this.Height -= 300;
                this.StackPanelLog.Visibility = Visibility.Collapsed;
            }
        }

        private void RchTextBxLog_TextChanged(object sender, EventArgs e)
        {
            RchTxtBxLog.ScrollToEnd();
        }

        private void ChkBoxSaveLogToFile_OnClick(object sender, RoutedEventArgs e)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(@".\log.txt",
                    outputTemplate: "{Timestamp:[yyyy-MM-dd HH:mm:ss]} [{Level:u3}] | {Message:lj}{NewLine}{Exception}",
                    encoding: System.Text.Encoding.UTF8)
                .WriteTo.RichTextBox(RchTxtBxLog)
                .MinimumLevel.Verbose()
                .CreateLogger();
        }

        private void BtnLaunchGitHubSite_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", "http://github.com/blgraf/MovieColour");
        }

        private void BtnChooseInputFile_OnClick(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = $"{Strings.FilterVideoFiles} (*.mkv;*.mp4)|*.mkv;*.mp4|{Strings.FilterAllFiles} (*.*)|*.*",
                // opens the last folder a file was selected from
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
                // opens "My Computer"
                //InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"
            };

            if (ofd.ShowDialog() == true)
            {
                files = [];
                foreach (string file in ofd.FileNames)
                {
                    files.Add(new FileInfo(file));
                }
                if (files.Count == 1)
                    this.TxtBxInputFile.Text = files[0].Name;
                else if (files.Count > 1)
                    this.TxtBxInputFile.Text = $"[{Strings.MultipleFilesSelected}]";
            }

            Log.Logger.Information(Strings.SelectedFiles);
            foreach (var fi in files)
                Log.Logger.Information(fi.Name);
        }

        private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            this.BtnCancel.IsEnabled = true;

            this.BtnStart.IsEnabled = false;
            this.BtnCancel.Visibility = Visibility.Visible;

            List<AnalysisMethod> methods = GetAnalysisMethods();

            try
            {
                if (methods.Count == 0)
                    throw new Exception(Strings.ErrorMustSelectAnalysisMethod); // ToDo #12

                var enableConversion = (bool)ChkBoxEnableConversion.IsChecked;

                foreach (FileInfo fileInfo in files)
                {
                    Log.Logger.Information(Strings.ProcessingFile, fileInfo.Name);
                    this.ProgressBarAnalysis.Value = 0;
                    this.ProgressBarConversion.Value = 0;
                    this.ProgressBarExtraction.Value = 0;
                    this.LblProgressConversionTime.Content = new TimeSpan();
                    this.LblProgressExtractionTime.Content = new TimeSpan();
                    this.LblProgressAnalysisTime.Content = new TimeSpan();

                    if (!int.TryParse(TxtBxWorkingScale.Text, out int scale))
                        throw new Exception("non-int scale"); // ToDo #12

                    var tmpfile = Path.Combine(fileInfo.DirectoryName, $"tmp-{fileInfo.Name[..25]}-{scale}p-{DateTime.Now:yy-MM-dd-HH-mm-ss}.mkv");

                    var fi = fileInfo;

                    if (enableConversion)
                    {
                        await ConvertMovie(fi.FullName, tmpfile, scale);
                        fi = new FileInfo(tmpfile);
                    }

                    // Step 0. Get fps
                    // Step 1. Get 1st frame to know byte[] size
                    // Step 2. Get frames and splite them into byte[]s. Calculated based on Step 1 to be within byte[] int index limits
                    // Step 3. Analyse byte[]s
                    // Step 4. Repeat 2-3 until all frames are analysed
                    // Step 5. Combine results
                    // Step 6. Create images

                    var fps = ImageHelper.GetFps(fi.FullName);
                    var singleFrameSize = ImageHelper.GetSingleFrameAsByteArray(fi.FullName).Length;
                    int framesPerBatch = int.MaxValue / singleFrameSize;

                    Log.Logger.Information(Strings.BatchSizeFrames, framesPerBatch);

                    var allResults = new List<AnalysisResult>();

                    int counter = 0;
                    double offset = 0d;
                    var hasMoreFrames = true;
                    var sw = new Stopwatch();
                    var sw2 = new Stopwatch();
                    sw2.Start();
                    do
                    {
                        sw.Start();
                        offset = counter * framesPerBatch / fps;
                        counter++;

                        var frames = ImageHelper.GetXFrames(fi.FullName, offset, framesPerBatch, singleFrameSize);
                        
                        if (frames.Length < framesPerBatch)
                            hasMoreFrames = false;

                        var intermediateResult = await AnalyseFrames(frames, methods);
                        allResults.Add(intermediateResult);
                        var batchSec = Math.Truncate(sw.Elapsed.TotalSeconds * 100) / 100;
                        Log.Logger.Information(Strings.BatchXTookYs, counter, batchSec);
                        sw.Restart();
                    }
                    while (hasMoreFrames);

                    var finalResult = new AnalysisResult
                    {
                        //BucketAvgMinMaxResult = allResults.SelectMany(x => x.BucketAvgMinMaxResult).ToArray(),
                        //BucketAvgTotalResult = allResults.SelectMany(x => x.BucketAvgTotalResult).ToArray(),
                        //BucketMedianResult = allResults.SelectMany(x => x.BucketMedianResult).ToArray(),
                        FrameAvgResult = allResults.SelectMany(x => x.FrameAvgResult).ToArray(),
                        //FrameMedianResult = allResults.SelectMany(x => x.FrameMedianResult).ToArray(),
                        FrameMostFrequentResult = allResults.SelectMany(x => x.FrameMostFrequentResult).ToArray()
                    };

                    var (min, sec) = GetMinSecFromTimeSpan(sw2.Elapsed);
                    var avgSec = Math.Truncate((sw2.Elapsed.TotalSeconds / counter) * 100) / 100;
                    Log.Logger.Information(Strings.AllBatchProcessTime, min, sec);
                    Log.Logger.Information(Strings.AvgTimePerBatch,  avgSec);

                    CreateImages(fi, finalResult);

                    //Deletion of temporary files
                    if ((bool)this.ChkBoxDeleteByProducts.IsChecked && enableConversion)
                    {
                        Log.Logger.Information(Strings.DeletingTmpMovieFile);
                        _ = AsyncHelper.DeleteFileAsync(tmpfile);
                    }
                }
            }
            catch (Exception ex)
            {
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    if (ex.Message.Equals("The operation was canceled."))
                    {
                        Log.Logger.Information(Strings.CancellationMessage);
                    }
                    else
                    {
                        Log.Logger.Error("{Exeption}", ex.Message);

                        var dialog = new GenericDialog(ex.Message);
                        dialog.Owner = this;
                        dialog.ShowDialog();
                    }
                }
                else
                {
                    Log.Logger.Error("{Exeption}", ex.Message);

                    var dialog = new GenericDialog(ex.Message);
                    dialog.Owner = this;
                    dialog.ShowDialog();
                }
            }
            Log.Logger.Information(Strings.AllFilesProcessed);
            this.BtnStart.IsEnabled = true;
        }

        #endregion

        /// <summary>
        /// Set default values for the UI elements
        /// </summary>
        private void SetDefaultValues()
        {
            this.ProgressBarConversion.Value = 0;
            this.ProgressBarExtraction.Value = 0;
            this.ProgressBarAnalysis.Value = 0;

            this.LblProgressConversionTime.Content = new TimeSpan();
            this.LblProgressExtractionTime.Content = new TimeSpan();
            this.LblProgressAnalysisTime.Content = new TimeSpan();

            this.TxtBxInputFile.IsReadOnly = true;
            this.TxtBxFrameCount.Text = "1";
            this.TxtBxWorkingScale.Text = "360";
            this.TxtBxOutputResolutionX.Text = "7680";
            this.TxtBxOutputResolutionY.Text = "4320";

            this.BtnCancel.IsEnabled = false;

            this.ChkBoxBuckets.IsEnabled = false;
            this.ChkBoxBuckets.IsChecked = false;
            this.ChkBoxBucketsTotal.IsEnabled = false;
            this.ChkBoxBucketsTotal.IsChecked = false;
            this.ChkBoxBucketsMinMax.IsEnabled = false;
            this.ChkBoxBucketsMinMax.IsChecked = false;
            this.ChkBoxBucketsMedian.IsEnabled = false;
            this.ChkBoxAvg.IsChecked = true;
            this.ChkBoxMostFrequent.IsChecked = true;
            this.ChkBoxMedian.IsEnabled = false;

            this.TxtBxBucketcount.IsEnabled = false;
            this.TxtBxBucketcount.Text = "3";
            this.ChkBoxEnableConversion.IsChecked = true;
            this.ChkBoxGPU.IsChecked = false;
            this.ChkBoxDeleteByProducts.IsChecked = true;
            this.TxtBxIncreaseBrightness.Text = "0";
        }

        /// <summary>
        /// Convert the movie to a temporary file
        /// </summary>
        /// <param name="inputfile"></param>
        /// <param name="tmpfile"></param>
        /// <returns></returns>
        private async Task ConvertMovie(string inputfile, string tmpfile, int scale)
        {
            Log.Logger.Information(Strings.ConversionStarted);

            Progress<int> progress;
            var watch = new Stopwatch();
            var movieColourHelper = new MovieColourHelper();

            progress = new Progress<int>(percent =>
            {
                ProgressBarConversion.Value = percent;
                var ts = new TimeSpan(watch.ElapsedTicks);
                if (percent > 0)
                {
                    var eta = (int)(ts.TotalSeconds * 10 / percent / 6);
                    LblProgressConversionTime.Content = string.Format(Strings.EstRemaining, eta);
                }
            }); // ToDo #4

            movieColourHelper.Progress = progress;
            movieColourHelper.InputFile = inputfile;

            watch.Start();

            movieColourHelper.ConvertMovieAsync(scale, tmpfile, (bool)ChkBoxGPU.IsChecked);

            watch.Stop();

            var ts = new TimeSpan(watch.ElapsedTicks);
            LblProgressConversionTime.Content = $"{Strings.Elapsed}: {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            watch.Reset();

            Log.Logger.Information(Strings.ConversionFinished);
        }

        /// <summary>
        /// Wrapper and progress reporting for the analysis of the frames
        /// </summary>
        /// <param name="files"></param>
        /// <param name="methods"></param>
        /// <returns></returns>
        private async Task<AnalysisResult> AnalyseFrames(byte[][] files, List<AnalysisMethod> methods)
        {
            Log.Logger.Information(Strings.AnalysisStarted);
            Progress<int> progress;
            var watch = new Stopwatch();
            var movieColourHelper = new MovieColourHelper();

            progress = new Progress<int>(percent =>
            {
                ProgressBarAnalysis.Value = percent;
                var ts = new TimeSpan(watch.ElapsedTicks);
                if (percent > 0)
                {
                    var eta = (int)(ts.TotalSeconds * 10 / percent / 6);
                    LblProgressAnalysisTime.Content = String.Format(Strings.EstRemaining, eta);
                }
            });

            movieColourHelper.Progress = progress;

            watch.Start();

            AnalysisResult ret;

            int b = int.Parse(TxtBxBucketcount.Text);

            ret = await movieColourHelper.AnalyseFramesUsingMethods(files, b, methods);

            watch.Stop();

            var ts = new TimeSpan(watch.ElapsedTicks);
            LblProgressAnalysisTime.Content = $"{Strings.Elapsed}: {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            watch.Reset();

            Log.Logger.Information(Strings.AnalysisFinished);
            return ret;
        }

        /// <summary>
        /// Create images from the analysis results for each method
        /// </summary>
        /// <param name="moviefile"></param>
        /// <param name="analysisResult"></param>
        private void CreateImages(FileInfo moviefile, AnalysisResult analysisResult)
        {
            Log.Logger.Information(Strings.CreatingImages);

            //CreateImageByMethodFromColourArray(moviefile, AnalysisMethod.BucketAvgTotal, DicColours.BucketAvgTotalResult);
            //CreateImageByMethodFromColourArray(moviefile, AnalysisMethod.BucketAvgMinMax, DicColours.BucketAvgMinMaxResult);
            //CreateImageByMethodFromColourArray(moviefile, AnalysisMethod.BucketMedian, DicColours.BucketMedianResult);
            CreateImageByMethodFromColourArray(moviefile, AnalysisMethod.FrameAvg, analysisResult.FrameAvgResult);
            //CreateImageByMethodFromColourArray(moviefile, AnalysisMethod.FrameMedian, DicColours.FrameMedianResult);
            CreateImageByMethodFromColourArray(moviefile, AnalysisMethod.FrameMostFrequent, analysisResult.FrameMostFrequentResult);
        }

        /// <summary>
        /// Creates an image from a colour array. The given method affects the filename
        /// The final name is <moviefile>-<framecount>-<method>.png (with a prefix for the fullsized image)
        /// The final image is resized to the given resolution and has its brightness increased by the given value
        /// </summary>
        /// <param name="movieFile"></param>
        /// <param name="method"></param>
        /// <param name="colours"></param>
        private void CreateImageByMethodFromColourArray(FileInfo movieFile, AnalysisMethod method, Color[] colours)
        {
            var img = ImageHelper.CreateBarcodeImageFromColours(colours);

            if (int.TryParse(this.TxtBxIncreaseBrightness.Text.Trim(), out int brightnessIncrease))
            {
                if (brightnessIncrease != 0)
                    img.Mutate(x =>
                        x.Brightness(
                            1 + (brightnessIncrease / 100f)
                        )
                    );
            }

            // Get filename based on file & method
            var filenameBuilder = new StringBuilder();
            filenameBuilder.Append(movieFile.DirectoryName);
            filenameBuilder.Append('\\');
            filenameBuilder.Append(movieFile.Name[..^4]);
            filenameBuilder.Append(int.Parse(TxtBxFrameCount.Text));

            var fullsizedNameBuilder = new StringBuilder(filenameBuilder.ToString());
            fullsizedNameBuilder.Append("_FULLSIZED");

            string methodString;
            switch (method)
            {
                case AnalysisMethod.FrameAvg:
                    methodString = "-Average";
                    break;
                case AnalysisMethod.FrameMostFrequent:
                    methodString = "-MostFrequent";
                    break;
                default:
                    methodString = $"-{method}";
                    break;
            }

            var pngSuffix = ".png";

            var filenameFullsized = string.Concat(fullsizedNameBuilder.ToString(), methodString, pngSuffix);
            var filenameNormal = string.Concat(filenameBuilder.ToString(), methodString, pngSuffix);

            // Save fullsized & normal image
            img.SaveAsPng(filenameFullsized);

            img.Mutate(x => x.Resize(int.Parse(TxtBxOutputResolutionX.Text), int.Parse(TxtBxOutputResolutionY.Text)));

            img.SaveAsPng(filenameNormal);
        }

        /// <summary>
        /// Maps the selected checkboxes to the AnalysisMethod enum
        /// </summary>
        /// <returns></returns>
        private List<AnalysisMethod> GetAnalysisMethods()
        {
            var list = new List<AnalysisMethod>();

            if ((bool)ChkBoxBucketsTotal.IsChecked)
                list.Add(AnalysisMethod.BucketAvgTotal);

            if ((bool)ChkBoxBucketsMinMax.IsChecked)
                list.Add(AnalysisMethod.BucketAvgMinMax);

            if ((bool)ChkBoxBucketsMedian.IsChecked)
                list.Add(AnalysisMethod.BucketMedian);

            if ((bool)ChkBoxAvg.IsChecked)
                list.Add(AnalysisMethod.FrameAvg);

            if ((bool)ChkBoxMostFrequent.IsChecked)
                list.Add(AnalysisMethod.FrameMostFrequent);

            if ((bool)ChkBoxMedian.IsChecked)
                list.Add(AnalysisMethod.FrameMedian);

            return list;
        }

        private static (int min, int sec) GetMinSecFromTimeSpan(TimeSpan ts)
        {
            var totalSeconds = ts.TotalSeconds;
            var minutes = (int)(totalSeconds / 60);
            int seconds = (int)(Math.Ceiling(totalSeconds) % 60);
            return (minutes, seconds);
        }

        /// <summary>
        /// Checks if FFmpeg and FFprobe are available frmot the command line
        /// Displays an error if not
        /// </summary>
        private void CheckFfmpegAvailability()
        {
            if (!CmdHelper.IsExecutableAvailable(FfCmds.Ffmpeg) || !CmdHelper.IsExecutableAvailable(FfCmds.Ffprobe))
            {
                Log.Logger.Error(Strings.ErrFfmpegNotAvailablePlsInstall);

                var dialog = new GenericDialog(Strings.ErrFfmpegNotAvailablePlsInstall);
                dialog.Owner = this;
                dialog.ShowDialog();

                this.BtnStart.IsEnabled = false;
            }
        }
    }
}
