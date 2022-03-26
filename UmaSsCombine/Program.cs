using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace UmaSsCombine
{
	internal class Program
	{
		static Config config;
		static void Main(string[] args)
		{
			config = Config.LoadConfig();
			if(args.Length <= 0) {
				return;
			}

			string outputDir = "";
			try {
				outputDir = Path.GetDirectoryName(args[0]);
				if(args.Length == 1) {
					writeErrMsg(outputDir, "2つ以上の画像ファイルを指定してください");
					return;
				}

				List<InputDetail> inputs = new List<InputDetail>();
				for(int i = 0; i < args.Length; i++) {
					FileInfo fi = new FileInfo(args[i]);
					Mat m = new Mat(args[i], ImreadModes.AnyColor);
					if(i > 0) {
						if(inputs[0].Mat.Width != m.Width || inputs[0].Mat.Height != m.Height) {
							writeErrMsg(outputDir, "異なる解像度の画像が入力されています");
							return;
						}
					}
					inputs.Add(new InputDetail {
						Mat = m,
						TimeStamp = fi.CreationTime,
						FileName = Path.GetFileNameWithoutExtension(fi.Name),
					});
				}

				Mat[] mats = sortInputs(inputs);
				if(mats == null) {
					writeErrMsg(outputDir, "画像の並び替えに失敗しました");
					return;
				}

				var width = mats[0].Width;
				var height = mats.Sum(p => p.Height);
				(int left, int right) = getBoundaryXPos(mats[0]);
				if(width <= 0 || height <= 0 || left <= 0 || right <= 0) {
					writeErrMsg(outputDir, $"境界の取得に失敗しました{Environment.NewLine}幅:{width} 高さ:{height} 左:{left} 右:{right}");
					return;
				}
				using Mat retMat = new Mat(new Size(width, height), mats[0].Type());
				int boundaryY = getBoundaryYPos(mats[0], left);
				if(boundaryY <= 0) {
					writeErrMsg(outputDir, $"1番目の画像の境界(下)の取得に失敗しました");
					return;
				}
				retMat[0, boundaryY, 0, width] = mats[0].Clone(new Rect(0, 0, width, boundaryY));
				int totalY = boundaryY;
				int searchHeight = (int)(mats[0].Height * config.SearchHeightRatio);
				for(int i = 1; i < mats.Length; i++) {
					var ret = TemplateMatch.Search(mats[i], retMat.Clone(new Rect(left, totalY - searchHeight, right - left, searchHeight)),
						new Rect(left, 0, right - left, mats[i].Height), config.MinTemplateMatchScore);
					if(ret == null) {
						writeErrMsg(outputDir, $"{i + 1}番目の画像のテンプレートマッチに失敗しました");
						return;
					}
					else if(ret.MatchScore <= 0) {
						writeErrMsg(outputDir, $"{i}番目と{i + 1}番目の画像の一致箇所が見つかりませんでした");
						return;
					}
					Debug.WriteLine($"{ret.MatchScore} {ret.Rect.X}:{ret.Rect.Y}");
					boundaryY = getBoundaryYPos(mats[i], left);
					if(boundaryY <= 0) {
						writeErrMsg(outputDir, $"{i + 1}番目の画像の境界(下)の取得に失敗しました");
						return;
					}
					retMat[totalY, totalY + boundaryY - ret.Rect.Y + searchHeight, 0, width] =
						mats[i].Clone(new Rect(0, ret.Rect.Y + searchHeight, width, boundaryY - ret.Rect.Y + searchHeight));
					totalY += boundaryY - searchHeight - ret.Rect.Y;
				}

				var filePath = Path.Combine(outputDir, $"{DateTime.Now:yyyyMMddHHmmssfff}.png");
				Cv2.ImWrite(filePath, retMat.Clone(new Rect(0, 0, width, totalY)));
			}
			catch(Exception ex) {
				writeErrMsg(outputDir, $"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
			}
		}

		static Mat[] sortInputs(IEnumerable<InputDetail> inputs)
		{
			if(config.SortTarget == SortTarget.None) {
				return inputs.Select(p => p.Mat).ToArray();
			}
			else if(config.SortTarget == SortTarget.TimeStamp) {
				if(config.SortOrder == SortOrder.Ascending) {
					return inputs.OrderBy(p => p.TimeStamp).Select(p => p.Mat).ToArray();
				}
				else if(config.SortOrder == SortOrder.Descending) {
					return inputs.OrderByDescending(p => p.TimeStamp).Select(p => p.Mat).ToArray();
				}
			}
			else if(config.SortTarget == SortTarget.FileName) {
				if(config.SortOrder == SortOrder.Ascending) {
					return inputs.OrderBy(p => p.FileName).Select(p => p.Mat).ToArray();
				}
				else if(config.SortOrder == SortOrder.Descending) {
					return inputs.OrderByDescending(p => p.FileName).Select(p => p.Mat).ToArray();
				}
			}
			return null;
		}

		static void writeErrMsg(string dir, string message)
		{
			if(Directory.Exists(dir)) {
				Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
				var filePath = Path.Combine(dir, $"{DateTime.Now:yyyyMMddHHmmssfff}_error.txt");
				File.WriteAllText(filePath, message, Encoding.GetEncoding("Shift-Jis"));
			}
		}

		static int getBoundaryYPos(Mat m, int x)
		{
			using Mat copy = m.CvtColor(ColorConversionCodes.BGR2GRAY);
			unsafe {
				byte* b = copy.DataPointer;
				for(int y = (int)(m.Height * config.BoundaryYPosHeightRatio); y < m.Height; y++) {
					if(b[y * m.Width + x] > config.BoundaryYPosHeightThresh) {
						return y;
					}
				}
			}
			return -1;
		}

		static (int left, int right) getBoundaryXPos(Mat m)
		{
			using Mat copy = m.CvtColor(ColorConversionCodes.BGR2GRAY);
			int left = -1;
			int right = -1;
			unsafe {
				byte* b = copy.DataPointer;
				int y = (int)(m.Height * config.BoundaryXPosHeightRatio);
				for(int x = (int)(m.Width * config.BoundaryXPosLeftRatio); x < m.Width * 0.5; x++) {
					if(b[y * m.Width + x] > config.BoundaryXPosLeftThresh) {
						left = x - 1;
						break;
					}
				}

				bool findDarkGray = false;
				for(int x = (int)(m.Width * config.BoundaryXPosRightRatio); x >= m.Width * 0.5; x--) {
					if(b[y * m.Width + x] < config.BoundaryXPosRightGrayThresh && !findDarkGray) {
						findDarkGray = true;
					}
					if(findDarkGray && b[y * m.Width + x] > config.BoundaryXPosRightThresh) {
						right = x;
						break;
					}
				}
			}
			return (left, right);
		}
	}
}
