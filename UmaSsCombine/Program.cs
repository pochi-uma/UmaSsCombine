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
			Mat combineMat = null;
			if(args.Length <= 0) {
				return;
			}
			Mat[] mats = null;
			try {
				outputDir = Path.GetDirectoryName(args[0]);
				var filePath = Path.Combine(outputDir, $"{DateTime.Now:yyyyMMddHHmmssfff}.png");
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
						if(config.Layout == Layout.Vertical
							|| config.Layout == Layout.Horizontal
							|| config.Layout == Layout.Pedigree
							) {
							if(inputs[0].Mat.Width != m.Width || inputs[0].Mat.Height != m.Height) {
								writeErrMsg("異なる解像度の画像が入力されています");
								return;
							}
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
				if(config.Layout == Layout.SimpleVertical || config.Layout == Layout.SimpleHorizontal) {
					using Mat simpleMat = ImgUtil.CombineSimple(mats, config.Layout);
					if(simpleMat != null) {
						Cv2.ImWrite(filePath, simpleMat);
					}
					return;
				}
				var borderRect = ImgUtil.GetBorder(mats[0]);
				var height = borderRect.Bottom;
				if(config.DeleteScrollBar) {
					ImgUtil.DeleteScrollBar(mats, borderRect);
				}
				int left = borderRect.X;
				int right = left + (int)(borderRect.Width * 0.95);
				if(width <= 0 || height <= 0 || left <= 0 || right <= 0) {
					writeErrMsg($"境界の取得に失敗しました{Environment.NewLine}幅:{width} 高:{height} 左:{left} 右:{right}");
					return;
				}

				// 結合結果画像
				combineMat = new Mat(new Size(width, mats.Sum(p => p.Height)), mats[0].Type());
				// 一枚目は無条件で結合
				combineMat[0, height, 0, width] = mats[0].Clone(new Rect(0, 0, width, height));
				// 結合画像の実高さ
				int totalY = height;
				// 結合用のテンプレートマッチする高さ(デフォルトは因子一行分程度)
				int searchHeight = (int)(mats[0].Height * config.SearchHeightRatio);

				// 2枚目以降
				for(int i = 1; i < mats.Length; i++) {
					// 結合結果画像の下から、テンプレートマッチする高さ分切り抜き
					using Mat croppedMat = combineMat.Clone(new Rect(left, totalY - searchHeight, right - left, searchHeight));
					// 結合対象とテンプレートマッチし、結合箇所を特定
					var ret = TemplateMatch.Search(mats[i], croppedMat,
						new Rect(left, 0, right - left, height), config.MinTemplateMatchScore);
					if(ret == null) {
						writeErrMsg($"{i + 1}番目の画像のテンプレートマッチに失敗しました");
						return;
					}
					else if(ret.MatchScore <= 0) {
						writeErrMsg($"{i}番目と{i + 1}番目の画像の一致箇所が見つかりませんでした");
						return;
					}
					using Mat combineTargetMat = mats[i].Clone(new Rect(0, ret.Rect.Y, width, height - ret.Rect.Y));
					combineMat[new Rect(0, totalY - searchHeight, width, combineTargetMat.Height)] = combineTargetMat;
					// 結合結果画像の実高さ更新
					totalY += combineTargetMat.Height - searchHeight;
				}
				combineMat = combineMat.CopyAfterDispose(combineMat.Clone(new Rect(0, 0, width, totalY)));

				var factorRects = ImgUtil.GetFactorRect(combineMat, borderRect);
				if(config.FactorOnly || config.Layout == Layout.Horizontal || config.Layout == Layout.Pedigree) {
					if(factorRects.Count != 3) {
						if(factorRects.Count <= 0) {
							writeErrMsg($"因子所持ウマ娘が見つかりませんでした");
						}
						else if(factorRects.Count < 3) {
							writeErrMsg($"因子所持ウマ娘が{factorRects.Count}人しか見つかりませんでした");
						}
						else if(factorRects.Count >= 4) {
							writeErrMsg($"因子所持ウマ娘が{factorRects.Count}人も見つかってしまいました・・・");
						}
						return;
					}
				}

				if(config.Layout == Layout.Vertical) {
					Rect rect = new Rect(0, 0, width, totalY);
					if(config.DeleteSideMargin) {
						rect.X = borderRect.X;
						rect.Width = borderRect.Width;
					}
					if(config.FactorOnly) {
						rect.Y = factorRects[0].Y;
						rect.Height = factorRects[^1].Y + factorRects[^1].Height - factorRects[0].Y;
					}
					Cv2.ImWrite(filePath, combineMat.Clone(rect));
				}
				else if(config.Layout == Layout.Horizontal || config.Layout == Layout.Pedigree) {
					Mat[] factorMats = new Mat[factorRects.Count];
					for(int i = 0; i < factorMats.Length; i++) {
						Rect rect = new Rect(0, factorRects[i].Y, width, factorRects[i].Height);
						if(config.DeleteSideMargin) {
							rect.X = borderRect.X;
							rect.Width = borderRect.Width;
						}
						if(i == 0 && !config.FactorOnly) {
							rect.Y = 0;
							rect.Height = factorRects[i].Height + factorRects[i].Y;
						}
						factorMats[i] = combineMat.Clone(rect);
					}
					if(config.Layout == Layout.Horizontal) {
						int startY = 0;
						Size size = new Size(factorMats.Sum(p => p.Width), factorMats.Max(p => p.Height));
						if(!config.FactorOnly && factorRects.Count > 0) {
							startY = factorRects[0].Y;
							size.Height = Math.Max(size.Height, factorMats.Skip(1).Max(p => p.Height) + startY);
						}
						using Mat retMat = new Mat(size, MatType.CV_8UC4, new Scalar(255, 255, 255, 0));
						int x = 0;
						for(int i = 0; i < factorMats.Length; i++) {
							if(factorMats[i].Channels() == 3) {
								using Mat tmpMat = factorMats[i].CvtColor(ColorConversionCodes.BGR2BGRA);
								retMat[new Rect(x, i == 0 ? 0 : startY, factorMats[i].Width, factorMats[i].Height)] = tmpMat;
							}
							else {
								retMat[new Rect(x, i == 0 ? 0 : startY, factorMats[i].Width, factorMats[i].Height)] = factorMats[i];
							}
							x += factorMats[i].Width;
						}
						Cv2.ImWrite(filePath, retMat);
					}
					else if(config.Layout == Layout.Pedigree) {
						var parentsMats = new[] { factorMats[0],
							ImgUtil.CombineSimple(new[] { factorMats[1], factorMats[2] }, Layout.SimpleVertical) };
						Size size = new Size(parentsMats.Sum(p => p.Width), parentsMats.Max(p => p.Height));
						using Mat retMat = new Mat(size, MatType.CV_8UC4, new Scalar(255, 255, 255, 0));
						int x = 0;
						for(int i = 0; i < parentsMats.Length; i++) {
							int startY = retMat.Height == parentsMats[i].Height ? 0
								: (retMat.Height - parentsMats[i].Height) / 2;
							if(factorMats[i].Channels() == 3) {
								using Mat tmpMat = parentsMats[i].CvtColor(ColorConversionCodes.BGR2BGRA);
								retMat[new Rect(x, startY, parentsMats[i].Width, parentsMats[i].Height)] = tmpMat;
							}
							else {
								retMat[new Rect(x, startY, parentsMats[i].Width, parentsMats[i].Height)] = parentsMats[i];
							}
							x += parentsMats[i].Width;
						}
						Cv2.ImWrite(filePath, retMat);
					}
					foreach(var v in factorMats) {
						v.NullCheckDispose();
					}
				}
			}
			catch(Exception ex) {
				writeErrMsg($"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
			}
			finally {
				if(mats != null) {
					foreach(var m in mats) {
						m.NullCheckDispose();
					}
				}
				combineMat.NullCheckDispose();
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
	}
}
