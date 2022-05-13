using OpenCvSharp;
using System;
using System.Collections.Generic;
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
				_srcMat.NullCheckDispose();
				srcMatGray.NullCheckDispose();
				targetMatGray.NullCheckDispose();
			}
			return templateMatchResult;
		}

		public static List<MatchResult> SearchMulti(Mat srcMat, Mat targetMat, Rect searchRect, float minScore)
		{
			Rect ret;
			Mat srcMatGray = null;
			Mat targetMatGray = null;
			Mat multiResult = null;
			Mat _srcMat = null;
			List<MatchResult> list = new List<MatchResult>();
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

				for(int i = 0; i < 100; i++) {
					using var result = new Mat();
					Cv2.MatchTemplate(srcMatGray, targetMatGray, result, TemplateMatchModes.CCoeffNormed);
					Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out Point minPoint, out Point maxPoint);

					if(maxVal < minScore) {
						result.NullCheckDispose();
						break;
					}
					float matchScore = result.At<float>(maxPoint.Y, maxPoint.X);
					if(searchRect != Rect.Empty) {
						ret = new Rect(maxPoint.X + searchRect.X, maxPoint.Y + searchRect.Y, targetMat.Width, targetMat.Height);
					}
					else {
						ret = new Rect(maxPoint.X, maxPoint.Y, targetMat.Width, targetMat.Height);
					}
					list.Add(new MatchResult { MatchScore = matchScore, Rect = ret });

					Cv2.Rectangle(srcMatGray, new Rect(maxPoint, targetMat.Size()), Scalar.Black, thickness: -1);
				}
			}
			catch(Exception ex) {
				Debug.WriteLine($"{ex.Message} : {ex.StackTrace}");
			}
			finally {
				_srcMat.NullCheckDispose();
				srcMatGray.NullCheckDispose();
				targetMatGray.NullCheckDispose();
				multiResult.NullCheckDispose();
			}
			return list;
		}

		public class MatchResult
		{
			public float MatchScore { get; set; }
			public Rect Rect { get; set; }
		}
	}
}
