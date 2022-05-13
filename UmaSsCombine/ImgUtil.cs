using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UmaSsCombine
{
	internal class ImgUtil
	{
		const int Black = 0;
		const int White = 255;

		public static Mat ballMat;
		public static Mat tabMat;

		static ImgUtil()
		{
			ballMat = new Mat(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
				"images", "ball.png"), ImreadModes.AnyColor);
			tabMat = new Mat(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
				"images", "tab.png"), ImreadModes.AnyColor);
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

		public static List<Rect> GetFactorRect(Mat m, Rect border)
		{
			List<Rect> rects = new List<Rect>();

			using var cropMat = m.Clone(new Rect(border.X, 0, border.Width, m.Height));
			var scale = 600 / (double)cropMat.Width;
			using var scaleMat = cropMat.ResizeFromScale(scale);
			var tabRet = TemplateMatch.Search(scaleMat, tabMat, new Rect(0, 0, scaleMat.Width, (int)(scaleMat.Height * 0.5)), 0.50f);
			if(tabRet == null || tabRet.MatchScore == 0) {
				return rects;
			}

			//var tmpMat = scaleMat.Clone();
			//tmpMat.Rectangle(new Rect(0, tmpY, scaleMat.Width, 1), Scalar.Red, 3);
			//Cv2.ImShow("tmp", tmpMat);
			//Cv2.WaitKey();

			var list = TemplateMatch.SearchMulti(scaleMat, ballMat, new Rect(100, tabRet.Rect.Y, 50, scaleMat.Height - tabRet.Rect.Y), 0.65f).OrderBy(p => p.Rect.Y).ToList();
			if(list.Count < 3) {
				return rects;
			}
			List<int> diffs = new List<int>();
			for(int i = 1; i < list.Count; i++) {
				diffs.Add(list[i].Rect.Y - list[i - 1].Rect.Y);
			}
			var avg = diffs.Average();

			int lastIdx = -1;
			int firstIdx = 0;
			int x = 0;
			int y = 0;
			int width = 0;
			int height = 0;
			for(int i = 1; i < list.Count; i++) {
				if(list[i].Rect.Y - list[i - 1].Rect.Y > avg) {
					firstIdx = lastIdx == -1 ? 0 : lastIdx;
					x = (int)(list[firstIdx].Rect.X / scale);
					y = (int)((list[firstIdx].Rect.Y - 26) / scale);
					width = (int)(list[firstIdx].Rect.Width / scale);
					height = Math.Min((int)((list[i - 1].Rect.Y + 35) / scale - y), m.Height - y);
					rects.Add(new Rect(x, y, width, height));
					lastIdx = i;
				}
			}
			firstIdx = lastIdx == -1 ? 0 : lastIdx;
			x = (int)(list[firstIdx].Rect.X / scale);
			y = (int)((list[firstIdx].Rect.Y - 26) / scale);
			width = (int)(list[firstIdx].Rect.Width / scale);
			height = Math.Min((int)((list[^1].Rect.Y + 35) / scale - y), m.Height - y);
			rects.Add(new Rect(x, y, width, height));
			return rects;
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

		public static Mat CombineSimple(Mat[] mats, Layout layout)
		{
			int totalWidth;
			int totalHeight;
			int avgHeight = 0;
			int avgWidth = 0;
			int x = 0;
			int y = 0;
			if(mats.Length <= 0) {
				return null;
			}
			else if(mats.Length == 1) {
				return mats[0];
			}

			switch(layout) {
				case Layout.SimpleVertical:
					totalWidth = mats.Max(p => p.Width);
					totalHeight = mats.Sum(p => p.Height);
					break;
				case Layout.SimpleHorizontal:
					totalWidth = mats.Sum(p => p.Width);
					totalHeight = mats.Max(p => p.Height);
					break;
				default:
					return null;
			}
			Mat retMat = new Mat(new Size(totalWidth, totalHeight), MatType.CV_8UC4, new Scalar(255, 255, 255, 0));
			foreach(var m in mats) {
				if(m.Channels() == 3) {
					using Mat tmpMat = m.CvtColor(ColorConversionCodes.BGR2BGRA);
					retMat[new Rect(x, y, tmpMat.Width, tmpMat.Height)] = tmpMat;
				}
				else {
					retMat[new Rect(x, y, m.Width, m.Height)] = m;
				}
				switch(layout) {
					case Layout.SimpleVertical:
						y += m.Height;
						break;
					case Layout.SimpleHorizontal:
						x += m.Width;
						break;
				}
			}
			return retMat;
		}

		internal enum CombineDirection
		{
			Vertical,
			Horizontal,
		}
	}

	internal static class MatExtention
	{
		public static Mat ResizeFromScale(this Mat m, double scale, InterpolationFlags interpolation = InterpolationFlags.Lanczos4)
		{
			return m.Resize(new Size((int)(m.Width * scale), (int)(m.Height * scale)), 0, 0, interpolation);
		}

		public static void NullCheckDispose(this Mat m)
		{
			if(m != null && m.IsEnabledDispose) {
				m.Dispose();
			}
		}

		public static Mat CopyAfterDispose(this Mat src, Mat dst)
		{
			src.NullCheckDispose();
			return dst;
		}
	}
}
