using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.ColorSpaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieColour.Helper
{
	public class SortColourHSL : Comparer<Color>
	{
		public override int Compare(Color x, Color y)
		{
			var c1 = x.ToPixel<Argb32>();
			var c2 = y.ToPixel<Argb32>();

			Hsl c1hsl = Argb32ToHSL(c1);



			return 1;
		}

		private Hsl Argb32ToHSL(Argb32 pixel)
		{
			Hsl ret = new Hsl();

			double r = (double)pixel.R / 255d;
			double g = (double)pixel.G / 255d;
			double b = (double)pixel.B / 255d;

			var max = new double[] { r, g, b }.Max();
			var min = new double[] { r, g, b }.Min();



			return new Hsl();
		}

		
	}
}
