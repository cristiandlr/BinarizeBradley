using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace BinarizeBradley
{
	/// <summary>
	/// Provides image conversion functionality from 24-bit color images to 1bpp TIFF CCITT4 format using Binarize Bradley's algorithm.
	/// Author: Cristian Ronquillo
	/// </summary>
	public class ImageConversion
	{
		ImageConversionParams _params = null;

		public ImageConversion(ImageConversionParams param)
		{
			_params = param;
		}

		/// <summary>
		/// Convert a Color Image to CCITT4 Fax Group TIFF using Bradley Adaptive Thresholding
		/// </summary>
		/// <param name="pathToImage">The full path to image file</param>
		/// <returns>The full path to TEMP folder or string.Empty in case image is in 1bpp format</returns>
		public string BinarizeBradleyCCITT4(string pathToImage)
		{
			using (var image = Image.FromFile(pathToImage)) // Load the image file
			{
				//var format = image.RawFormat;
				if (image.PixelFormat == PixelFormat.Format1bppIndexed) //Image is already in 1bpp pixel format
					return string.Empty;

				var tiffExtension = "TIFF";
				var encoder = GetCodecInfo(tiffExtension);

				if (encoder == null)
					throw new Exception("Image format not found.");

				//var frameInfo = GetFrameFromImageFile(pathToImage); //frameInfo.Format.BitsPerPixel
				var prop = image.PropertyItems;
				var encoderParams = new EncoderParameters(1);
				var myEncoder1 = System.Drawing.Imaging.Encoder.Compression;
				//var myEncoder2 = System.Drawing.Imaging.Encoder.ColorDepth;
				encoderParams.Param[0] = new EncoderParameter(myEncoder1, (long)(EncoderValue.CompressionCCITT4));
				//encoderParams.Param[1] = new EncoderParameter(myEncoder2, 1L); //No longed needed. CompressionCCITT4 is always 1bpp

				//using (Bitmap tempBitmap = new Bitmap(new Bitmap(image)))
				Bitmap tempBitmap = ColorToGrayscale((Bitmap)image);
				try
				{
					tempBitmap.SetResolution(_params.OutputResolution, _params.OutputResolution);

					var pathToTemp = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "BinarizeBradley");
					if (!Directory.Exists(pathToTemp))
						Directory.CreateDirectory(pathToTemp);

					var tmpImgFile = Path.Combine(pathToTemp, Path.GetFileName(pathToImage)) + "." + tiffExtension;
					if (File.Exists(tmpImgFile))
						File.Delete(tmpImgFile);

					BradleyAdaptiveThresholding(ref tempBitmap, _params.BrightnessDiffLimit);
					tempBitmap.Save(tmpImgFile, encoder, encoderParams);

					return tmpImgFile;
				}
				finally
				{
					tempBitmap.Dispose();
				}
			}
		}

		/// <summary>
		/// Converts a 24 or 32-bit bitmap into 8-bit grayscale bitmap. 
		/// Note: I found this method somewhere in StackOverflow...
		/// </summary>
		private static Bitmap ColorToGrayscale(Bitmap bmp)
		{
			int w = bmp.Width,
				h = bmp.Height,
				r, 
				ic, oc, //ic is the input column and oc is the output column
				bmpStride, outputStride, bytesPerPixel;

			PixelFormat pfIn = bmp.PixelFormat;
			ColorPalette palette;
			Bitmap output;
			BitmapData bmpData, outputData;

			//Create the new bitmap
			output = new Bitmap(w, h, PixelFormat.Format8bppIndexed);

			//Build a grayscale color Palette
			palette = output.Palette;
			for (int i = 0; i < 256; i++)
			{
				Color tmp = Color.FromArgb(255, i, i, i);
				palette.Entries[i] = Color.FromArgb(255, i, i, i);
			}
			output.Palette = palette;

			//No need to convert formats if already in 8 bit
			if (pfIn == PixelFormat.Format8bppIndexed)
			{
				output = (Bitmap)bmp.Clone();

				//Make sure the palette is a grayscale palette and not some other
				//8-bit indexed palette
				output.Palette = palette;

				return output;
			}

			//Get the number of bytes per pixel
			switch (pfIn)
			{
				case PixelFormat.Format24bppRgb: bytesPerPixel = 3; break;
				case PixelFormat.Format32bppArgb: bytesPerPixel = 4; break;
				case PixelFormat.Format32bppRgb: bytesPerPixel = 4; break;
				default: throw new InvalidOperationException("Image format not supported");
			}

			//Lock the images
			bmpData = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, pfIn);
			outputData = output.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
			bmpStride = bmpData.Stride;
			outputStride = outputData.Stride;

			//Traverse each pixel of the image
			unsafe
			{
				byte* bmpPtr = (byte*)bmpData.Scan0.ToPointer(),
				outputPtr = (byte*)outputData.Scan0.ToPointer();

				if (bytesPerPixel == 3)
				{
					//Formula luminance = .299*R + .587*G + .114*B
					for (r = 0; r < h; r++)
						for (ic = oc = 0; oc < w; ic += 3, ++oc)
							outputPtr[r * outputStride + oc] = (byte)(int)
								(0.2126f * bmpPtr[r * bmpStride + ic] +      //0.299f 
								 0.7152f * bmpPtr[r * bmpStride + ic + 1] +  //0.587f 
								 0.0722f * bmpPtr[r * bmpStride + ic + 2]);  //0.114f 
				}
				else //bytesPerPixel == 4
				{
					for (r = 0; r < h; r++)
						for (ic = oc = 0; oc < w; ic += 4, ++oc)
							outputPtr[r * outputStride + oc] = (byte)(int)
								((bmpPtr[r * bmpStride + ic] / 255.0f) *
								(0.2126f * bmpPtr[r * bmpStride + ic + 1] +  //0.299f 
								 0.7152f * bmpPtr[r * bmpStride + ic + 2] +  //0.587f 
								 0.0722f * bmpPtr[r * bmpStride + ic + 3])); //0.114f 
				}
			}

			//Unlock the images
			bmp.UnlockBits(bmpData);
			output.UnlockBits(outputData);

			return output;
		}

		private ImageCodecInfo GetCodecInfo(string type)
		{
			ImageCodecInfo[] info = ImageCodecInfo.GetImageEncoders();

			for (int i = 0; i < info.Length; i++)
			{
				if (info[i].FormatDescription.Equals(type))
				{
					return info[i];
				}
			}

			return null;

		}
		
		private static byte[] BufferFromImage(ref Bitmap bitmap)
		{
			// Lock the bitmap's bits.  
			Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
			BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);

			byte[] grayValues = null;

			try
			{
				// Get the address of the first line.
				IntPtr ptr = bmpData.Scan0;

				// Declare an array to hold the bytes of the bitmap.
				int bytes = bmpData.Width * bitmap.Height;
				grayValues = new byte[bytes];

				// Copy the gray values into the array.
				Marshal.Copy(ptr, grayValues, 0, bytes);
			}
			finally
			{
				bitmap.UnlockBits(bmpData);
			}

			return grayValues;

			//using (MemoryStream memoryStream = new MemoryStream())
			//{
			//	bitmap.Save(memoryStream, ImageFormat.Bmp);
			//	buffer = memoryStream.ToArray();
			//}
		}

		/// <summary>
		/// Convert a byte array to Bitmap
		/// </summary>
		/// <param name="bytes">bytes of the image</param>
		/// <param name="w">width</param>
		/// <param name="h">height</param>
		/// <param name="ch">number of channels (i.e. 3 = 24 bit RGB)</param>
		/// <param name="bitmap">output Bitmap, used to clone Image properties</param>
		/// <returns></returns>
		public static void ImageFromBuffer(byte[] bytes, int w, int h, int ch, ref Bitmap bitmap)
		{
			//bitmap = new Bitmap(w, h, PixelFormat.Format8bppIndexed);
			BitmapData bmData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, bitmap.PixelFormat);
			try
			{
				IntPtr pNative = bmData.Scan0;
				Marshal.Copy(bytes, 0, pNative, w * h * ch);
			}
			finally
			{
				bitmap.UnlockBits(bmData);
			}

			//return bitmap;
			// using (var ms = new MemoryStream(imageData)) { bitm = new Bitmap(ms); }
		}

		/// <summary>
		/// Given i,j index from matrix M, provides the corresponding 1-dimensional index of a Vector representation of M
		/// </summary>
		/// <param name="i"></param>
		/// <param name="j"></param>
		/// <param name="matrixWidth"></param>
		/// <returns></returns>
		private static int Idx2dTo1d(int i, int j, int matrixWidth)
		{
			return matrixWidth * j + i;
		}

		/// <summary>
		/// Given sub-matrix indexes (x1,y1),(x2,y2) and matrix (M) dimensions (w,h),
		/// Set (x1,y1),(x2,y2) to be inside bounds of M for X axis
		/// </summary>
		/// <param name="x1"></param>
		/// <param name="x2"></param>
		/// <param name="w"></param>
		private static void ValidateSIndexesX(ref int x1, ref int x2, int w)
		{
			if (x1 < 0)
				x1 = 0;
			else if (x2 >= w)
				x2 = w - 1;
		}

		/// <summary>
		/// Given sub-matrix indexes (x1,y1),(x2,y2) and matrix (M) dimensions (w,h),
		/// Set (x1,y1),(x2,y2) to be inside bounds of M for Y axis
		/// </summary>
		/// <param name="y1"></param>
		/// <param name="y2"></param>
		/// <param name="h"></param>
		private static void ValidateSIndexesY(ref int y1, ref int y2, int h)
		{
			if (y1 < 0)
				y1 = 0;
			else if (y2 >= h)
				y2 = h - 1;
		}

		/// <summary>
		/// C# implementation of the algorithm Adaptive Thresholding Using the Integral Image described here: 
		/// http://people.scs.carleton.ca/~roth/iit-publications-iti/docs/gerh-50002.pdf
		/// </summary>
		/// <param name="bitmap">8-bit grayscale bitmap</param>
		/// <param name="brightnessDiffLimit">Pixel Brightness Difference Limit (0.0, 1.0)</param>
		/// <returns></returns>
		private static void BradleyAdaptiveThresholding(ref Bitmap bitmap, float brightnessDiffLimit)
		{
			var w = bitmap.Width;
			var h = bitmap.Height;
			var input = BufferFromImage(ref bitmap);
			var output = new byte[input.LongLength];
			var intImg = new long[input.LongLength]; //Integral Images
			long sum = 0;
			int s = w / 14; //s x s sub-matrix;
			int halfS = s / 2;
			float t = 1 - brightnessDiffLimit; // 18% Pixel Brightness Difference Limit

			// Create the integral image
			for (int i = 0; i < w; i++)
			{
				sum = 0;
				for (int j = 0; j < h; j++)
				{
					int idx1d = Idx2dTo1d(i, j, w); //calc 1-dimensional index

					sum = sum + input[idx1d];
					if (i == 0)
						intImg[idx1d] = sum;
					else
						intImg[idx1d] = intImg[Idx2dTo1d(i - 1, j, w)] + sum;
				}
			}

			// Perform thresholding
			int x1, x2, y1, y2;
			for (int i = 0; i < w; i++)
			{
				x1 = i - halfS;
				x2 = i + halfS;
				ValidateSIndexesX(ref x1, ref x2, w);

				for (int j = 0; j < h; j++)
				{
					y1 = j - halfS;
					y2 = j + halfS;
					ValidateSIndexesY(ref y1, ref y2, h);

					int count = (x2 - x1) * (y2 - y1);

					int x1M1 = Math.Abs(x1 - 1);
					int y1M1 = Math.Abs(y1 - 1);

					sum = intImg[Idx2dTo1d(x2, y2, w)]
						- intImg[Idx2dTo1d(x2, y1M1, w)]
						- intImg[Idx2dTo1d(x1M1, y2, w)]
						+ intImg[Idx2dTo1d(x1M1, y1M1, w)];

					int idx1d = Idx2dTo1d(i, j, w); //calc 1-dimensional index

					if (input[idx1d] * count <= sum * t)
						output[idx1d] = 0;
					else
						output[idx1d] = 255;
				}
			}

			ImageFromBuffer(output, w, h, 1, ref bitmap);
		}

	}
}
