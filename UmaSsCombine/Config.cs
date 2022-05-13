using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UmaSsCombine
{
	internal class Config
	{
		public double SearchHeightRatio { get; set; } = 0.04d;
		public float MinTemplateMatchScore { get; set; } = 0.5f;
		public SortTarget SortTarget { get; set; } = SortTarget.FileName;
		public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
		public bool DeleteScrollBar { get; set; } = false;
		public bool DeleteSideMargin { get; set; } = false;
		public bool FactorOnly { get; set; } = false;
		public Layout Layout { get; set; } = Layout.Vertical;

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

	internal enum Layout
	{
		Vertical,
		Horizontal,
		Pedigree,
		SimpleVertical,
		SimpleHorizontal,
	}
}
