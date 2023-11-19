using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace TrTimeTableConverter
{
	class Program
	{
		static void Main()
		{
			Console.Write("trtimetable format file path: ");
			var path = Console.ReadLine();
			var items = new List<TrTimeTableItem>();
			using (var file = new StreamReader(File.OpenRead(path)))
			{
				var regex = new Regex(" +", RegexOptions.Compiled);
				string line;
				while (!string.IsNullOrWhiteSpace(line = file.ReadLine()))
				{
					var arg = regex.Replace(line, " ").Split(' ');
					var depth = int.Parse(arg[4]);
					if (depth % 10 != 0)
						continue;
					items.Add(new TrTimeTableItem((int)(double.Parse(arg[1]) * 1000), (int)(double.Parse(arg[3]) * 1000), depth, int.Parse(arg[5])));
				}
			}

			Console.Write("output file path: ");
			var outPath = Console.ReadLine();

			using (var file = File.OpenWrite(outPath))
				MessagePackSerializer.Serialize(file, items, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

			Console.WriteLine("Complete!");
		}
	}

	[MessagePackObject]
	public class TrTimeTableItem(int pTime, int sTime, int depth, int distance)
	{
		[Key(0)]
		public int PTime { get; } = pTime;

		[Key(1)]
		public int STime { get; } = sTime;

		[Key(2)]
		public int Depth { get; } = depth;

		[Key(3)]
		public int Distance { get; } = distance;
	}
}
