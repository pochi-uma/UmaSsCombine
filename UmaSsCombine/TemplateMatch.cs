using OpenCvSharp;
using System;
using System.Diagnostics;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;

namespace UmaSsCombine
{
	public class TemplateMatch
	{
		public static MatchResult Search(Mat srcMat, Mat targetMat, Rect searchRect, float minScore)
		{
			Rect ret = Rect.Empty;

			Mat srcMatGray = null;
			Mat targetMatGray = null;
			Mat _srcMat = null;
			MatchResult templateMatchResult = null;
			try {
				if(searchRect != Rect.Empty) {
					_srcMat = srcMat.Clone(searchRect);
				}
				else {
					_srcMat = srcMat.Clone();
				}
				if(_srcMat.Type() != MatType.CV_8UC1) {
					srcMatGray = _srcMat.CvtColor(ColorConversionCodes.BGR2GRAY);
				}
				else {
					srcMatGray = srcMat.Clone();
				}
				if(targetMat.Type() != MatType.CV_8UC1) {
					targetMatGray = targetMat.CvtColor(ColorConversionCodes.BGR2GRAY);
				}
				else {
					targetMatGray = targetMat.Clone();
				}

				using var result = new Mat();
				Cv2.MatchTemplate(srcMatGray, targetMatGray, result, TemplateMatchModes.CCoeffNormed);
				Cv2.MinMaxLoc(result, out Point minPoint, out Point maxPoint);
				float matchScore = result.At<float>(maxPoint.Y, maxPoint.X);

				if(matchScore < minScore) {
					return new MatchResult { MatchScore = 0, Rect = ret };
				}

				if(searchRect != Rect.Empty) {
					Rect rect = new Rect(maxPoint.X + searchRect.X, maxPoint.Y + searchRect.Y, targetMat.Width, targetMat.Height);
					ret = rect;
				}
				else {
					ret = new Rect(maxPoint.X, maxPoint.Y, targetMat.Width, targetMat.Height);
				}
				templateMatchResult = new MatchResult { MatchScore = matchScore, Rect = ret };
			}
			catch(Exception ex) {
				Debug.WriteLine($"Error: {ex.Message} : {ex.StackTrace}");
			}
			finally {
				Displose(_srcMat);
				Displose(srcMatGray);
				Displose(targetMatGray);
			}
			return templateMatchResult;
		}

		private static void Displose<T>(T m) where T : IDisposable
		{
			if(m != null)
				m.Dispose();
		}

		public class MatchResult
		{
			public float MatchScore { get; set; }
			public Rect Rect { get; set; }
		}
	}
}
