using Sharprompt;
using System.IO.Ports;
using System.Text;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Exceptions;

string? port = "";
var ports = SerialPort.GetPortNames();
if (ports.Length > 0)
	port = Prompt.Select("シリアルポートを選択してください", ports);
else
	port = Prompt.Input<string>("シリアルポートを認識できません。手動で入力してください", validators: new[] { Validators.Required() });

using var serial = new SerialPort(port)
{
	BaudRate = 115200,
	DtrEnable = true,
	RtsEnable = true,
};

try
{
	serial.Open();
}
catch (Exception ex)
{
	Console.WriteLine("シリアルポートのオープンに失敗しました。\n" + ex.ToString());
}
try
{
	var buffer = new byte[4096];
	var exit = false;
	Console.CancelKeyPress += (s, e) =>
	{
		if (!exit)
			exit = e.Cancel = true;
	};

	var type = SentenceType.None;
	ushort ubxLength = 0;
	var sentence = new List<byte>(1024);

	while (!exit)
	{
		var count = serial.Read(buffer, 0, buffer.Length);
		for (var i = 0; i < count; i++)
		{
			var c = buffer[i];

			// センテンスの開始を探す
			if (type == SentenceType.None)
			{
				switch (c)
				{
					// NMEA
					case (byte)'$':
						type = SentenceType.Nmea;
						sentence.Clear();
						sentence.Add(c);
						continue;
					// UBX
					case 0xb5:
						sentence.Clear();
						sentence.Add(c);
						break;
					case 0x62 when sentence.Count == 1 && sentence[^1] == 0xb5:
						type = SentenceType.Ubx;
						sentence.Add(c);
						continue;
					default:
						continue;
				}
			}

			if (type == SentenceType.Nmea)
			{
				sentence.Add(c);
				if (c == '\n' && sentence[^2] == '\r')
				{
					// NMEA センテンスの完成
					var nmea = Encoding.ASCII.GetString(sentence.ToArray());
					// チェックサム確認
					var csIndex = nmea.IndexOf('*');
					string[] parts;
					if (csIndex != -1)
					{
						parts = nmea[1..csIndex].Split(',');
						// チェックサムを取得
						var chS = nmea[(csIndex + 1)..].TrimEnd('\r', '\n');
						byte checkSum = 0;
						foreach (var b in nmea[1..csIndex])
							checkSum ^= (byte)b;
						if (chS != checkSum.ToString("X2"))
							Console.WriteLine("NMEA CheckSum Error: " + nmea[1..csIndex]);
						//Console.Write(nmea);
					}
					type = SentenceType.None;
				}
			}
			else if (type == SentenceType.Ubx)
			{
				sentence.Add(c);
				// payload length を読む
				if (sentence.Count == 6)
					ubxLength = BitConverter.ToUInt16(sentence.ToArray(), 4);
				else if (sentence.Count > 6 && sentence.Count >= ubxLength + 6 + 2)
				{
					// UBX センテンスの完成
					byte csA = 0;
					byte csB = 0;
					for (var j = 2; j < sentence.Count - 2; j++)
					{
						csA = (byte)(csA + sentence[j]);
						csB = (byte)(csB + csA);
					}
					if (csA != sentence[^2] || csB != sentence[^1])
					{
						Console.WriteLine($"UBX CheckSum Error: {csA:X2} {sentence[^2]:X2} {csB:X2} {sentence[^1]:X2}");
					}
					else
					{
						if (sentence[2] == 2 && sentence[3] == 0x13 && ubxLength >= 44 && sentence[6] == 5 && sentence[10] == 9) // UBX-RXM-SFRBX, 44 bytes, QZSS
						{
							var data = new byte[sentence[10] * 4];
							for (var j = 0; j < sentence[10]; j++)
							{
								data[j * 4 + 0] = sentence[14 + j * 4 + 3];
								data[j * 4 + 1] = sentence[14 + j * 4 + 2];
								data[j * 4 + 2] = sentence[14 + j * 4 + 1];
								data[j * 4 + 3] = sentence[14 + j * 4 + 0];
							}

							if (data.Length >= 32)
							{
								try
								{
									var report = DCReport.Parse(data[..32]);
									Console.WriteLine($"DCReport({report.MessageType}): " + report);
									if (report is JmaDCReport jmaDCReport)
										Console.WriteLine($"  Dc:{jmaDCReport.DisasterCategoryCode} It:{jmaDCReport.InformationType} Rc:{jmaDCReport.ReportClassification}");
									else if (report is OtherOrganizationDCReport otherDCReport)
										Console.WriteLine($"  Rc:{otherDCReport.ReportClassification} Oc:{otherDCReport.OrganizationCode} Raw:{BitConverter.ToString(otherDCReport.RawData)}");
								}
								catch (DCReportParseException e)
								{
									Console.WriteLine("DCReport Error\n" + e);
								}
							}
						}
					}
					type = SentenceType.None;
				}
			}
		}
	}
}
finally
{
	serial.Close();
}

enum SentenceType
{
	None,
	Nmea,
	Ubx,
}
