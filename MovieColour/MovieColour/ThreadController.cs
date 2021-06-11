using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieColour
{
	internal class ThreadController
	{
		private string[] files;
		private int id;
		private bool IsUncompressed;
		private ImageHelper helper;
		private int X;
		private int BucketAmount;

		private ExCallback callback;

		internal ThreadController(int id, string[] files, bool IsUncompressed, int xframe,int BucketAmount, ExCallback callback)
		{
			this.id = id;
			this.files = files;
			this.callback = callback;
			this.IsUncompressed = IsUncompressed;
			this.X = xframe;
			this.BucketAmount = BucketAmount;

			this.helper = new ImageHelper();
		}

		internal void Proc()
		{
			List<Color[]> c = helper.GetColoursFromFiles(files, IsUncompressed, (int)Math.Ceiling((double)files.Count() / 15), X, BucketAmount, id);
			if (callback != null)
			{
				Logger.WriteLogMessage("Finished analysing images.", id);
				callback(id, c);
			}
		}

	}

	internal delegate void ExCallback(int id, List<Color[]> colours);
}
