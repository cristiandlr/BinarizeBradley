using System;
using System.IO;

namespace BinarizeBradley
{
	class Program
	{
		static void Main(string[] args)
		{
			var curdir = Directory.GetCurrentDirectory();
			DirectoryInfo d = new DirectoryInfo(curdir);
			FileInfo[] Files = d.GetFiles("*.jpg");

			var param = new ImageConversionParams();
			param.OutputResolution = 200;
			param.BrightnessDiffLimit = 0.18f;
			param.WorkingPath = Environment.GetEnvironmentVariable("TEMP");

			var im = new ImageConversion(param);

			//string str = "";
			var now = DateTime.Now;
			foreach (FileInfo file in Files)
			{
				Console.WriteLine("Binarizing file " + file.Name);
				var convertedFilePath = im.BinarizeBradleyCCITT4(file.Name);
			}

			var dif = DateTime.Now - now;
			Console.WriteLine("Output directory is: " + param.WorkingPath);
			Console.WriteLine("Time elapsed:" + dif.ToString() + Environment.NewLine + "...Press any key to close.");
			Console.ReadKey();
		}
	}
}
