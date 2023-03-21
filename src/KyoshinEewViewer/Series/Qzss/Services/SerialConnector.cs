using HarfBuzzSharp;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Exceptions;
using KyoshinEewViewer.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Qzss.Services;

public class SerialConnector : ReactiveObject
{
	private bool isConnected;
	public bool IsConnected
	{
		get => isConnected;
		private set => this.RaiseAndSetIfChanged(ref isConnected, value);
	}

	private bool IsClosing { get; set; }
	private Task ReceiveTask { get; }

	public SerialConnector()
	{
		MessageBus.Current.Listen<ApplicationClosing>().Subscribe(s => IsClosing = true);
		ReceiveTask = Task.Run(() =>
		{
			var buffer = new byte[4096];
			while (!IsClosing)
			{
				if (ConfigurationService.Current.Qzss.SerialPort == null)
				{
					Thread.Sleep(1000);
					continue;
				}
				using var serial = new SerialPort();
				try
				{
					serial.Open();
					var type = SentenceType.None;
					ushort ubxLength = 0;
					var sentence = new List<byte>(1024);

					while (!IsClosing)
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
				catch (Exception ex)
				{
					Debug.WriteLine(ex);
				}
			}
		});
	}
}

enum SentenceType
{
	None,
	Nmea,
	Ubx,
}
