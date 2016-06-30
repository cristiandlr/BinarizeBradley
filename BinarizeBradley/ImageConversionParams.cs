using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinarizeBradley
{
	public class ImageConversionParams
	{
		/// <summary>
		/// Working directory
		/// </summary>
		public string WorkingPath { get; set; }
		/// <summary>
		/// Output image resolution
		/// </summary>
		public float OutputResolution { get; set; }
		/// <summary>
		/// Brightness threshold for image conversion. Set a value &gt; 0.0 and &lt; 1.0
		/// 0.15 works reasonably good for most images.
		/// </summary>
		public float BrightnessDiffLimit { get; set; }
	}
}
