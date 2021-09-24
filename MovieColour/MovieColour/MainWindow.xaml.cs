using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using static MovieColour.Helper.Enums;
using MovieColour.Helper;
using SixLabors.ImageSharp.Processing;

namespace MovieColour
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		private List<string> files;

		public MainWindow()
		{
			InitializeComponent();

			SetDefaultValues();

		}

		private void BtnLaunchGitHubSite_OnClick(object sender, RoutedEventArgs e)
		{
			Process.Start("explorer.exe", "http://github.com/blgraf/MovieColour");
		}

		private void BtnChooseInputFile_OnClick(object sender, RoutedEventArgs e)
		{
			files = new List<string>();
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Multiselect = true;
			ofd.Filter = "Video files (*.mkv;*.mp4)|*.mkv;*.mp4|All files (*.*)|*.*";
			// opens the last folder a file was selected from
			ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
			// opens "My Computer"
			//ofd.InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
			if (ofd.ShowDialog() == true)
			{
				foreach (string file in ofd.FileNames)
					files.Add(file);
				if (files.Count == 1)
					this.TxtBxInputFile.Text = Path.GetFileName(files[0]);
				else if (files.Count > 1)
					this.TxtBxInputFile.Text = "[Multiple files]";
			}
		}

		private async void BtnStart_OnClick(object sender, RoutedEventArgs e)
		{
			try
			{
				this.BtnStart.IsEnabled = false;

				List<AnalysisMethod> methods = GetAnalysisMethods();

				if (methods.Count == 0)
					throw new Exception("Must select at least one analysis method");

				var enableConversion = (bool)ChkBoxEnableConversion.IsChecked && ChkBoxEnableConversion.IsEnabled;
				var enableExtraction = (bool)ChkBoxEnableExtraction.IsChecked;


				foreach (string file in files)
				{
					FileInfo fi = new FileInfo(file);

					var filename = fi.Name;
					var foldername = fi.DirectoryName;
					var tmpfile = Path.Combine(foldername, "tmp.mkv");

					if (enableConversion)
						await ConvertMovie(fi.FullName, tmpfile);

					if (enableExtraction)
					{
						if (enableConversion)
						{
							fi = new FileInfo(tmpfile);
							await ExtractFrames(tmpfile);
						}
						else
							await ExtractFrames(fi.FullName);
					}

					string[] files = GetFiles(fi.FullName);

					Dictionary<AnalysisMethod, Color[]> DicColours = await AnalyseFrames(files, methods);

					CreateImages(new FileInfo(file), DicColours);

					// Deletion of temporary files
					if ((bool)this.ChkBoxDeleteByProducts.IsChecked)
					{
						if (enableConversion)
							File.Delete(tmpfile);
						Directory.Delete(fi.DirectoryName + "\\" + fi.Name + "_frames", true);
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(Application.Current.MainWindow, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			this.BtnStart.IsEnabled = true;
		}


		private void BrnResetToDefault_OnClick(object sender, RoutedEventArgs e)
		{
			SetDefaultValues();
		}

		private void ChkBoxRapid_OnClick(object sender, RoutedEventArgs e)
		{
			bool isRapid = (bool)ChkBoxRapid.IsChecked;

			ChkBoxBuckets.IsEnabled = !isRapid;
			ChkBoxBucketsTotal.IsEnabled = !isRapid;
			ChkBoxBucketsMinMax.IsEnabled = !isRapid;
			ChkBoxBucketsMedian.IsEnabled = !isRapid;
			ChkBoxMostFrequent.IsEnabled = !isRapid;
			ChkBoxMedian.IsEnabled = !isRapid;

			ChkBoxEnableConversion.IsEnabled = !isRapid;
		}

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
			this.TxtBxWorkingScale.Text = "360:-2";
			this.TxtBxOutputResolutionX.Text = "5120";
			this.TxtBxOutputResolutionY.Text = "1440";

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
			this.ChkBoxEnableExtraction.IsChecked = true;
			this.ChkBoxRapid.IsChecked = false;
			this.ChkBoxGPU.IsChecked = false;
			this.ChkBoxDeleteByProducts.IsChecked = true;
			this.TxtBxIncreaseBrightness.Text = "0";
		}

		private async Task ConvertMovie(string inputfile, string tmpfile)
		{
			Progress<int> progress;
			Stopwatch watch = new Stopwatch();
			MovieColourHelper movieColourHelper = new MovieColourHelper();

			progress = new Progress<int>(percent =>
			{
				ProgressBarConversion.Value = percent;
				var ts = new TimeSpan(watch.ElapsedTicks);
				LblProgressConversionTime.Content = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
			});

			movieColourHelper._Progress = progress;
			movieColourHelper.InputFile = inputfile;

			watch.Start();

			await movieColourHelper.ConvertMovieAsync(TxtBxWorkingScale.Text, tmpfile, (bool)ChkBoxGPU.IsChecked);

			watch.Stop();

			var ts = new TimeSpan(watch.ElapsedTicks);
			LblProgressConversionTime.Content = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
			watch.Reset();
		}

		private async Task ExtractFrames(string inputfile)
		{
			Progress<int> progress;
			Stopwatch watch = new Stopwatch();
			MovieColourHelper movieColourHelper = new MovieColourHelper();

			progress = new Progress<int>(percent =>
			{
				ProgressBarExtraction.Value = percent;
				var ts = new TimeSpan(watch.ElapsedTicks);
				LblProgressExtractionTime.Content = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
			});

			movieColourHelper._Progress = progress;
			movieColourHelper.InputFile = inputfile;

			watch.Start();

			await movieColourHelper.ExtractEveryXthFrameAsync(int.Parse(TxtBxFrameCount.Text), (bool)ChkBoxGPU.IsChecked);

			watch.Stop();

			var ts = new TimeSpan(watch.ElapsedTicks);
			LblProgressExtractionTime.Content = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
			watch.Reset();
		}

		private async Task<Dictionary<AnalysisMethod, Color[]>> AnalyseFrames(string[] files, List<AnalysisMethod> methods)
		{
			Progress<int> progress;
			Stopwatch watch = new Stopwatch();
			MovieColourHelper movieColourHelper = new MovieColourHelper();

			progress = new Progress<int>(percent =>
			{
				ProgressBarAnalysis.Value = percent;
				var ts = new TimeSpan(watch.ElapsedTicks);
				LblProgressAnalysisTime.Content = $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
			});

			movieColourHelper._Progress = progress;

			watch.Start();

			int x = int.Parse(TxtBxFrameCount.Text);

			if ((bool)ChkBoxRapid.IsChecked)
			{
				return await movieColourHelper.AnalyseFramesUsingMethods(files, (bool)ChkBoxRapid.IsChecked, x);
			}
			else
			{
				int b = int.Parse(TxtBxBucketcount.Text);

				return await movieColourHelper.AnalyseFramesUsingMethods(files, (bool)ChkBoxRapid.IsChecked, x, b, methods);
			}
		}

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

		private string[] GetFiles(string inputFile)
		{
			var fi = new FileInfo(inputFile);
			var folderName = fi.DirectoryName + "\\" + fi.Name + "_frames";
			var di = new DirectoryInfo(folderName);
			return di.GetFiles().OrderBy(n => Regex.Replace(n.Name, @"\d+", n => n.Value.PadLeft(6, '0'))).Select(f => f.FullName).ToArray();
		}

		private void CreateImages(FileInfo moviefile, Dictionary<AnalysisMethod, Color[]> DicColours)
		{
			foreach (var kvp in DicColours)
			{
				var imgHelper = new ImageHelper();
				var img = imgHelper.CreateBarcodeImageFromColours(kvp.Value.ToList());

				string filename = moviefile.DirectoryName + "\\" + moviefile.Name.Substring(0, moviefile.Name.Length-4) + "-" + int.Parse(TxtBxFrameCount.Text);
				filename += "-" + ((bool)this.ChkBoxRapid.IsChecked ? "c" : "ff");

				if (kvp.Key == AnalysisMethod.FrameAvg)
					filename += "-AvgC";
				else if (kvp.Key == AnalysisMethod.FrameMostFrequent)
					filename += "-FrqC";

				string filenameFullsized = filename + "-FULLSIZED.png";
				filename += ".png";

				img.SaveAsPng(filenameFullsized);

				img.Mutate(x => x.Resize(int.Parse(TxtBxOutputResolutionX.Text), int.Parse(TxtBxOutputResolutionY.Text)));

				img.SaveAsPng(filename);


			}



		}
	}
}
