using OpenCvSharp;
using System.Collections.Generic;
using System.Linq;

namespace UmaSsCombine
{
	internal class ImgUtil
	{
		const int Black = 0;
		const int White = 255;
		static Config config;
		static ImgUtil()
		{
			config = Config.LoadConfig();
		}
		public static Rect GetBorder(Mat m)
		{
			var scalar_min = new Scalar(0, 0, 0);
			var scalar_max = new Scalar(255, 255, 245);
			using Mat hsv = m.CvtColor(ColorConversionCodes.BGR2HSV_FULL);
			using Mat inRange = hsv.InRange(scalar_min, scalar_max);
			int x;
			int y;
			Dictionary<int, int> dic = new Dictionary<int, int>();
			bool findWhite = false;
			unsafe {
				byte* b = inRange.DataPointer;
				for(y = (int)(m.Height * 0.5); y < (int)(m.Height * 0.85); y++) {
					findWhite = false;
					for(x = 1; x < m.Width / 2; x++) {
						if(!findWhite && b[y * m.Width + x] == White) {
							findWhite = true;
						}
						else if(findWhite && b[y * m.Width + x] == Black) {
							if(!dic.ContainsKey(x)) {
								dic.Add(x, 0);
							}
							dic[x]++;
							break;
						}
					}
				}
			}
			var borderX = dic.OrderByDescending(p => p.Value).First().Key;
			var borderBottom = 0;
			using Mat m2 = m.Clone(new Rect(borderX, 0, m.Width - borderX * 2, m.Height));

			int borderWidth = m.Width - borderX * 2;
			unsafe {
				byte* b = inRange.DataPointer;
				dic.Clear();
				for(y = (int)(m.Height * 0.7); y < (int)(m.Height * 0.95); y++) {
					findWhite = false;
					for(x = borderX + 3; x < (int)(m.Width - borderX - 3); x++) {
						if(b[y * m.Width + x] == White) {
							findWhite = true;
							break;
						}
					}
					if(!findWhite) {
						borderBottom = y - 1;
						break;
					}
				}
			}
			return new Rect(borderX, 0, borderWidth, borderBottom - 0);
		}

		public static void DeleteScrollBar(Mat[] mats, Rect border)
		{
			var m = mats[0];
			var scalarMin = new Scalar(0, 0, 0);
			var scalarMax = new Scalar(255, 60, 230);
			double scale = m.Width / (double)border.Width;
			using Mat crop = m.Clone(border);

			using Mat hsv = crop.CvtColor(ColorConversionCodes.BGR2HSV_FULL);
			using Mat inRange = hsv.InRange(scalarMin, scalarMax);

			unsafe {
				byte* b = inRange.DataPointer;
				int top = -1;
				int bottom = -1;
				int findCntLimit = (int)(border.Width * 0.99) - (int)(border.Width * 0.98);
				for(int y = inRange.Height - 1; y >= 0; y--) {
					int findBlackCnt = 0;
					for(int x = (int)(border.Width * 0.98); x < (int)(border.Width * 0.99); x++) {
						if(bottom == -1) {
							if(b[y * inRange.Width + x] == White) {

								bottom = y + 10;
								break;
							}
						}
						else if(b[y * inRange.Width + x] == Black) {
							if(++findBlackCnt >= findCntLimit) {
								top = y - 4;
								break;
							}
						}
					}
					if(top > 0) {
						break;
					}
				}
				int width = (int)(inRange.Width * 0.02) + 2;
				int scrollBarLeft = (int)(border.Width * 0.98) + (m.Width - border.Width) / 2;

				for(int i = 0; i < mats.Length; i++) {
					mats[i].Rectangle(new Rect(scrollBarLeft - width / 2, top, width, bottom - top), new Scalar(242, 243, 242), -1);
				}
			}
		}
	}
}
