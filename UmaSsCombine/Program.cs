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
		static string outputDir = "";
		/// <summary>
		/// 入口
		/// </summary>
		/// <param name="args">結合対象画像Path</param>
		static void Main(string[] args)
		{
			config = Config.LoadConfig();
			if(args.Length <= 0) {
				return;
			}
			Mat[] mats = null;
			try {
				outputDir = Path.GetDirectoryName(args[0]);
				if(args.Length == 1) {
					writeErrMsg("2つ以上の画像ファイルを指定してください");
					return;
				}

				// 画像読み込み
				List<InputDetail> inputs = new List<InputDetail>();
				for(int i = 0; i < args.Length; i++) {
					FileInfo fi = new FileInfo(args[i]);
					Mat m = new Mat(args[i], ImreadModes.AnyColor);
					if(i > 0) {
						if(inputs[0].Mat.Width != m.Width || inputs[0].Mat.Height != m.Height) {
							writeErrMsg("異なる解像度の画像が入力されています");
							return;
						}
					}
					inputs.Add(new InputDetail {
						Mat = m,
						TimeStamp = fi.CreationTime,
						FileName = Path.GetFileNameWithoutExtension(fi.Name),
					});
				}

				// Configに従いソート
				mats = sortInputs(inputs);
				if(mats == null) {
					writeErrMsg("画像の並び替えに失敗しました");
					return;
				}

				// 1枚目を基準画像とする
				var width = mats[0].Width;
				var height = mats.Sum(p => p.Height);
				// 左右の基点を取得
				(int left, int right) = getBoundaryXPos(mats[0]);
				Debug.WriteLine($"Left:{left} Right:{right}");
				if(width <= 0 || height <= 0 || left <= 0 || right <= 0) {
					writeErrMsg($"境界の取得に失敗しました{Environment.NewLine}幅:{width} 高さ:{height} 左:{left} 右:{right}");
					return;
				}
				
				// 結合結果画像
				using Mat retMat = new Mat(new Size(width, height), mats[0].Type());
				// 結合対象の最下段取得
				int boundaryY = getBoundaryYPos(mats[0], left);
				Debug.WriteLine($"0 Bottom:{boundaryY}");
				if(boundaryY <= 0) {
					writeErrMsg($"1番目の画像の境界(下)の取得に失敗しました");
					return;
				}
				// 一枚目は無条件で結合
				retMat[0, boundaryY, 0, width] = mats[0].Clone(new Rect(0, 0, width, boundaryY));
				// 結合画像の実高さ
				int totalY = boundaryY;
				// 結合用のテンプレートマッチする高さ(デフォルトは因子一行分程度)
				int searchHeight = (int)(mats[0].Height * config.SearchHeightRatio);
				Debug.WriteLine($"SearchHeight:{searchHeight}");
				// 2枚目以降
				for(int i = 1; i < mats.Length; i++) {
					// 結合結果画像の下から、テンプレートマッチする高さ分切り抜き
					using Mat croppedMat = retMat.Clone(new Rect(left, totalY - searchHeight, right - left, searchHeight));
					// 結合対象とテンプレートマッチし、結合箇所を特定
					var ret = TemplateMatch.Search(mats[i], croppedMat,
						new Rect(left, 0, right - left, mats[i].Height), config.MinTemplateMatchScore); ;
					if(ret == null) {
						writeErrMsg($"{i + 1}番目の画像のテンプレートマッチに失敗しました");
						return;
					}
					else if(ret.MatchScore <= 0) {
						writeErrMsg($"{i}番目と{i + 1}番目の画像の一致箇所が見つかりませんでした");
						return;
					}
					Debug.WriteLine($"{ret.MatchScore} {ret.Rect.X}:{ret.Rect.Y}");
					// 結合対象の最下段取得
					boundaryY = getBoundaryYPos(mats[i], left);
					Debug.WriteLine($"{i} Bottom:{boundaryY}");
					if(boundaryY <= 0) {
						writeErrMsg($"{i + 1}番目の画像の境界(下)の取得に失敗しました");
						return;
					}
					else if(boundaryY < ret.Rect.Y + searchHeight) {
						writeErrMsg($"{i}番目と{i + 1}番目の画像の一致箇所が見つかりませんでした");
						return;
					}
					// 結合
					using Mat combineMat = mats[i].Clone(new Rect(0, ret.Rect.Y, width, boundaryY - ret.Rect.Y));
					retMat[new Rect(0, totalY - searchHeight, width, combineMat.Height)] = combineMat;
					// 結合結果画像の実高さ更新
					totalY += combineMat.Height - searchHeight;
					Debug.WriteLine($"TotalY:{totalY}");
				}
				// 結合結果書き出し
				var filePath = Path.Combine(outputDir, $"{DateTime.Now:yyyyMMddHHmmssfff}.png");
				Cv2.ImWrite(filePath, retMat.Clone(new Rect(0, 0, width, totalY)));
			}
			catch(Exception ex) {
				writeErrMsg($"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
			}
			finally {
				if(mats != null) {
					foreach(var m in mats) {
						if(m != null) {
							m.Dispose();
						}
					}
				}
			}
		}

		/// <summary>
		/// 入力画像のソート処理
		/// </summary>
		/// <param name="inputs">入力情報(ファイル名・タイムスタンプ・画像)</param>
		/// <returns></returns>
		static Mat[] sortInputs(IEnumerable<InputDetail> inputs)
		{
			// ソート指定なし = そのまま
			if(config.SortTarget == SortTarget.None) {
				return inputs.Select(p => p.Mat).ToArray();
			}
			// タイムスタンプ順
			else if(config.SortTarget == SortTarget.TimeStamp) {
				// 昇順
				if(config.SortOrder == SortOrder.Ascending) {
					return inputs.OrderBy(p => p.TimeStamp).Select(p => p.Mat).ToArray();
				}
				// 降順
				else if(config.SortOrder == SortOrder.Descending) {
					return inputs.OrderByDescending(p => p.TimeStamp).Select(p => p.Mat).ToArray();
				}
			}
			// ファイル名順
			else if(config.SortTarget == SortTarget.FileName) {
				// 昇順
				if(config.SortOrder == SortOrder.Ascending) {
					return inputs.OrderBy(p => p.FileName).Select(p => p.Mat).ToArray();
				}
				// 降順
				else if(config.SortOrder == SortOrder.Descending) {
					return inputs.OrderByDescending(p => p.FileName).Select(p => p.Mat).ToArray();
				}
			}
			return null;
		}

		static void writeErrMsg(string message)
		{
			if(Directory.Exists(outputDir)) {
				Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
				var filePath = Path.Combine(outputDir, $"{DateTime.Now:yyyyMMddHHmmssfff}_error.txt");
				File.WriteAllText(filePath, message, Encoding.GetEncoding("Shift-Jis"));
			}
		}

		/// <summary>
		/// 結合対象の最下段を取得する
		/// </summary>
		/// <param name="m">画像</param>
		/// <param name="x">検索対象のX座標</param>
		/// <returns></returns>
		static int getBoundaryYPos(Mat m, int x)
		{
			// グレースケール化
			using Mat copy = m.CvtColor(ColorConversionCodes.BGR2GRAY);
			unsafe {
				byte* b = copy.DataPointer;
				// 上から順にグレースケール化した値を見ていき、閾値超えたら最下段と判断
				for(int y = (int)(m.Height * config.BoundaryYPosHeightRatio); y < m.Height; y++) {
					Debug.WriteLine($"YPos {x}:{y} {b[y * m.Width + x]}");
					if(b[y * m.Width + x] > config.BoundaryYPosHeightThresh) {
						return y;
					}
				}
			}
			return -1;
		}

		/// <summary>
		/// 左右の基点を取得する
		/// </summary>
		/// <param name="m">画像</param>
		/// <returns></returns>
		static (int left, int right) getBoundaryXPos(Mat m)
		{
			// グレースケール化
			using Mat copy = m.CvtColor(ColorConversionCodes.BGR2GRAY);
			int left = -1;
			int right = -1;
			unsafe {
				byte* b = copy.DataPointer;
				int y = (int)(m.Height * config.BoundaryXPosHeightRatio);
				int leftFindCount = 0;
				int leftFindCountThresh = (int)(m.Width * config.BoundaryXPosLeftFindCountRatio);
				// 左側のX座標検索
				for(int x = (int)Math.Max(config.BoundaryXPosLeftMargin, m.Width * config.BoundaryXPosLeftRatio); x < m.Width * 0.5; x++) {
					Debug.WriteLine($"XLeftPos {x}:{y} {b[y * m.Width + x]}");
					if(b[y * m.Width + x] > config.BoundaryXPosLeftMinThresh && b[y * m.Width + x] < config.BoundaryXPosLeftMaxThresh) {
						if(++leftFindCount >= leftFindCountThresh) {
							left = x;
							break;
						}
					}
					else {
						leftFindCount = 0;
					}
				}
				if(left == -1) {
					return (-1, -1);
				}

				// スクロールバー検知フラグ
				bool findDarkGray = false;
				// 右側のX座標検索
				for(int x = Math.Min(m.Width - 1, m.Width - left + leftFindCountThresh); x >= m.Width * 0.5; x--) {
					Debug.WriteLine($"XRightPos {x}:{y} {b[y * m.Width + x]}");
					if(b[y * m.Width + x] < config.BoundaryXPosRightGrayThresh && !findDarkGray) {
						Debug.WriteLine($"Find gray {x}:{y}");
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
