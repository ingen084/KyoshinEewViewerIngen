using KyoshinEewViewer.TravelTimeTable.Models;
using MessagePack;
using System.Text.RegularExpressions;

namespace TrTimeTableConverter;

class Program
{
	static void Main()
	{
		Console.Write("trtimetable format file path: ");
		var path = Console.ReadLine();
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			Console.WriteLine("ファイルが見つかりません");
			return;
		}

		var items = new List<TravelTimeEntry>();
		using (var file = new StreamReader(File.OpenRead(path)))
		{
			var regex = new Regex(" +", RegexOptions.Compiled);
			string? line;
			while (!string.IsNullOrWhiteSpace(line = file.ReadLine()))
			{
				var arg = regex.Replace(line, " ").Split(' ');
				if (arg.Length < 6)
					continue;

				var depth = int.Parse(arg[4]);
				// 10km刻みのみ使用
				if (depth % 10 != 0)
					continue;

				items.Add(new TravelTimeEntry(
					distanceKm: int.Parse(arg[5]),
					depthKm: depth,
					pTimeMs: (int)(double.Parse(arg[1]) * 1000),
					sTimeMs: (int)(double.Parse(arg[3]) * 1000)));
			}
		}

		Console.WriteLine($"読み込んだエントリ数: {items.Count}");

		Console.Write("output file path: ");
		var outPath = Console.ReadLine();
		if (string.IsNullOrWhiteSpace(outPath))
		{
			Console.WriteLine("出力パスが指定されていません");
			return;
		}

		using (var file = File.OpenWrite(outPath))
			MessagePackSerializer.Serialize(file, items.ToArray(), MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

		Console.WriteLine($"Complete! 出力: {outPath}");
	}
}
