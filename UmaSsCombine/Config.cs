using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UmaSsCombine
{
	internal class Config
	{
		public double BoundaryYPosHeightRatio { get; set; } = 0.65d;
		public int BoundaryYPosHeightThresh { get; set; } = 247;
		public double BoundaryXPosHeightRatio { get; set; } = 0.7d;
		public double BoundaryXPosLeftRatio { get; set; } = 0.03d;
		public int BoundaryXPosLeftMinThresh { get; set; } = 240;
		public int BoundaryXPosLeftMaxThresh { get; set; } = 250;
		public int BoundaryXPosLeftMargin { get; set; } = 9;
		public double BoundaryXPosLeftFindCountRatio { get; set; } = 0.01d;
		public int BoundaryXPosRightThresh { get; set; } = 240;
		public int BoundaryXPosRightGrayThresh { get; set; } = 215;
		public double SearchHeightRatio { get; set; } = 0.04d;
		public float MinTemplateMatchScore { get; set; } = 0.5f;
		public SortTarget SortTarget { get; set; } = SortTarget.FileName;
		public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
		public bool DeleteScrollBar { get; set; } = false;
		public bool DeleteSideMargin { get; set; } = false;
		public static Config LoadConfig()
		{
			var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
			if(File.Exists(path)) {
				return JsonSerializer.Deserialize<Config>(File.ReadAllText(path),
					new JsonSerializerOptions {
						AllowTrailingCommas = true,
						PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
						Converters = {
							new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
						},
					});
			}
			else {
				Config config = new Config();
				File.WriteAllText(path, JsonSerializer.Serialize(config, new JsonSerializerOptions {
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
					Converters = {
						new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
					},
					WriteIndented = true,
				}));
				return config;
			}
		}
	}

	internal enum SortTarget
	{
		None,
		FileName,
		TimeStamp
	}

	internal enum SortOrder
	{
		Ascending,
		Descending
	}
}
