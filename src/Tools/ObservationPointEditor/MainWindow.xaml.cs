using KyoshinEewViewer.MapControl;
using KyoshinMonitorLib;
using KyoshinMonitorLib.UrlGenerator;
using MessagePack;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ObservationPointEditor
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			map.Map = MessagePackSerializer.Deserialize<TopologyMap>(Properties.Resources.JapanMap, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
			map.Zoom = 5;
			map.CenterLocation = new Location(36.474f, 135.264f);

			//TODO: 設定ファイルからデフォルトの値読み込み
			//SetPoints(ObservationPoint.LoadFromMpk("ShindoObsPoints.mpk.lz4", true));
			gridView.ItemDoubleClicked += () =>
			{
				var point = gridView.SelectedPoint;
				if (point == null) return;
				map.Navigate(new Rect(point.Location.AsPoint() - new Vector(.4, .4), point.Location.AsPoint() + new Vector(.4, .4)), new Duration(TimeSpan.FromSeconds(.25)));
			};
			gridView.SelectedPointChanged += e =>
			{
				if (e.oldValue != null && map.RenderObjects.Cast<RenderObjects.ObservationPointRenderObject>().FirstOrDefault(r => r.ObservationPoint == e.oldValue) is RenderObjects.ObservationPointRenderObject ro)
					ro.IsSelected = false;
				if (e.newValue != null && map.RenderObjects.Cast<RenderObjects.ObservationPointRenderObject>().FirstOrDefault(r => r.ObservationPoint == e.newValue) is RenderObjects.ObservationPointRenderObject rn)
					rn.IsSelected = true;
				imageMap.SelectedPoint = e.newValue;
				map.InvalidateChildVisual();
			};
			imageMap.PointClicked += e =>
			{
				switch (e.button)
				{
					case MouseButton.Middle: // 選択
						{
							var point = imageMap.Points?.FirstOrDefault(p => p.Point == e.location && p != gridView.SelectedPoint);
							gridView.SelectedPoint = point;
						}
						break;
					case MouseButton.Left: // 移動
						{
							if (gridView.SelectedPoint == null)
								break;
							gridView.SelectedPoint.Point = e.location;
							gridView.InvalidateVisual();
							imageMap.InvalidateVisual();
							map.InvalidateChildVisual();
						}
						break;
					case MouseButton.Right: // 削除
						{
							if (gridView.SelectedPoint?.Point != e.location)
								break;
							if (MessageBox.Show("登録された座標を削除しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
							{
								gridView.SelectedPoint.Point = null;
								gridView.InvalidateVisual();
								imageMap.InvalidateVisual();
								map.InvalidateChildVisual();
							}
						}
						break;
				}
			};
			linkcheckMenuItem.Click += async (s, e) =>
			{
				if (gridView.Points == null)
					return;
				linkcheckMenuItem.IsEnabled = false;
				try
				{
					var data = await new AppApi(gridView.Points).GetLinkedRealtimeData(DateTime.Now.AddMinutes(-1), RealtimeDataType.Shindo);
					foreach (var datum in data.Data)
					{
						var lp = datum.ObservationPoint;
						if (lp.Point != null)
						{
							var ro = map.RenderObjects.Cast<RenderObjects.ObservationPointRenderObject>().FirstOrDefault(r => r.ObservationPoint == lp.Point);
							ro.IsLinked = true;
						}
					}
				}
				finally
				{
					Dispatcher.Invoke(() =>
					{
						linkcheckMenuItem.IsEnabled = true;
						map.InvalidateChildVisual();
					});
				}
			};

			loadMenuItem.Click += (s, e) =>
			{
				var dialog = new OpenFileDialog
				{
					Filter = "MessagePack(LZ4)|*.mpk.lz4|MessagePack(未圧縮)|*.mpk|CSV|*.csv|Json|*.json|EqWatch互換|*.dat",
					FilterIndex = 1,
					RestoreDirectory = true
				};
				if (dialog.ShowDialog() == true)
				{
					var filename = dialog.FileName;
					SetPoints(dialog.FilterIndex switch
					{
						1 => ObservationPoint.LoadFromMpk(filename, true),
						2 => ObservationPoint.LoadFromMpk(filename, false),
						3 => ObservationPoint.LoadFromCsv(filename).points,
						4 => ObservationPoint.LoadFromJson(filename),
						5 => ImportEqWatchData(filename),
						_ => null
					});
				}
			};
			saveAsMenuItem.Click += (s, e) =>
			{
				var dialog = new SaveFileDialog
				{
					Filter = "MessagePack(LZ4)|*.mpk.lz4|MessagePack(未圧縮)|*.mpk|CSV|*.csv|Json|*.json|EqWatch互換|*.dat",
					FilterIndex = 1,
					RestoreDirectory = true
				};
				if (dialog.ShowDialog() == true)
				{
					var filename = dialog.FileName;
					switch (dialog.FilterIndex)
					{
						case 1:
							ObservationPoint.SaveToMpk(filename, gridView.Points, true);
							break;
						case 2:
							ObservationPoint.SaveToMpk(filename, gridView.Points, false);
							break;
						case 3:
							ObservationPoint.SaveToCsv(filename, gridView.Points);
							break;
						case 4:
							ObservationPoint.SaveToJson(filename, gridView.Points);
							break;
						case 5:
							ExportEqWatchData(filename);
							break;
					}
				}
			};
			importAndCreateMenuItem.Click += async (s, e) =>
			{
				importAndCreateMenuItem.IsEnabled = false;
				await ImportNiedData();
				Dispatcher.Invoke(() =>
				{
					importAndCreateMenuItem.IsEnabled = true;
				});
			};
		}

		private void SetPoints(ObservationPoint[] points)
		{
			map.RenderObjects = points.Select(p => new RenderObjects.ObservationPointRenderObject(p)).ToArray();
			gridView.Points = points;
			gridView.SelectedPoint = null;
			imageMap.Points = points;
		}

		private ObservationPoint[] ImportEqWatchData(string filename)
		{
			var result = new List<ObservationPoint>();

			var addedCount = 0;
			var addId = 0;
			var replaceCount = 0;
			var errorCount = 0;

			try
			{
				using (var reader = new StreamReader(filename, Encoding.GetEncoding("Shift-JIS")))
				{
					if (!reader.ReadLine().StartsWith("v5 Kyoshin Monitor Base Data for EqWatch"))
					{
						MessageBox.Show("対応していないEqWatchファイルのバージョンです。", null);
						return null;
					}
					while (reader.Peek() >= 0)
					{
						try
						{
							var strings = reader.ReadLine().Split(',');

							var point = result.FirstOrDefault(p => p.Type == strings[2].ToObservationPointType() && p.Name == strings[3] && (strings[4] == "その他" || p.Region.StartsWith(strings[4])));
							//あるとき!
							if (point != null)
							{
								point.IsSuspended = strings[0] == "0";
								if (/*!completionMode || */point.Point == null)
									point.Point = new Point2(int.Parse(strings[9]) + int.Parse(strings[11]), int.Parse(strings[10]) + int.Parse(strings[12]));
								replaceCount++;
							}
							//ないとき…
							else
							{
								while (result.Any(p => p.Code == $"_EQW{addId}"))
									addId++;

								point = new ObservationPoint
								{
									Type = strings[2].ToObservationPointType(),
									Code = $"_EQW{addId}",
									IsSuspended = strings[0] == "0",
									Name = strings[3],
									Region = strings[4],
									Location = new Location(float.Parse(strings[8]), float.Parse(strings[7])),
									Point = new Point2(int.Parse(strings[9]) + int.Parse(strings[11]), int.Parse(strings[10]) + int.Parse(strings[12])),
								};
								result.Add(point);
								addedCount++;
								addId++;
							}
							point.ClassificationId ??= int.Parse(strings[5]);
							point.PrefectureClassificationId ??= int.Parse(strings[6]);
						}
						catch
						{
							errorCount++;
						}
					}
				}
				MessageBox.Show($"レポート\n置換:{replaceCount}件\n追加:{addedCount}件\n失敗:{errorCount}件", "処理終了", MessageBoxButton.OK, MessageBoxImage.Information);
				return result.ToArray();
			}
			catch (Exception ex)
			{
				MessageBox.Show("EqWatchデータのインポートに失敗しました。\n" + ex, null);
				return null;
			}
		}
		private void ExportEqWatchData(string filename)
		{
			if (!gridView.Points?.Any() ?? true)
				return;

			MessageBox.Show("不明なカラム･観測点IDなど非互換の項目などは出力されません。", "注意");

			try
			{
				using (var reader = new StreamWriter(filename, false, Encoding.GetEncoding("Shift-JIS")))
				{
					reader.WriteLine("v5 Kyoshin Monitor Base Data for EqWatch");

					foreach (var point in gridView.Points)
					{
						if (point.Point == null)
							continue;
						reader.WriteLine($"{(point.IsSuspended ? "0" : "1")},1,{point.Type switch { ObservationPointType.KiK_net => 1, ObservationPointType.K_NET => 2, _ => 0 }},{point.Name},{point.Region},{point.ClassificationId ?? 0},{point.PrefectureClassificationId ?? 0},{point.Location.Longitude},{point.Location.Latitude},{point.Point?.X},{point.Point?.Y},0,0,255");
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("EqWatchデータへのエクスポートに失敗しました。\n" + ex, null);
			}
		}

		private async Task ImportNiedData()
		{
			if (gridView.Points?.Any() ?? false && MessageBox.Show("登録されていない地点情報が追加で登録されます。インポートしてもよろしいですか？", "確認", MessageBoxButton.YesNo) == MessageBoxResult.No)
				return;
			var points = new List<ObservationPoint>();
			if (gridView.Points != null)
				points.AddRange(gridView.Points);

			var addedCount = 0;
			var errorCount = 0;
			var updateCount = 0;

			using var client = new HttpClient();

			try
			{
				using (var request = new HttpRequestMessage(HttpMethod.Get, "https://www.kyoshin.bosai.go.jp/kyoshin/pubdata/kik/sitedb/sitepub_kik_sj.csv"))
				{
					request.Headers.TryAddWithoutValidation("Referer", "https://www.kyoshin.bosai.go.jp/kyoshin/db/index.html?");
					using var response = await client.SendAsync(request);
					using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(), Encoding.GetEncoding("Shift-JIS"));
					while (reader.Peek() >= 0)
					{
						try
						{
							var strings = reader.ReadLine().Split(',');

							ObservationPoint point;
							//発見したら情報のアップデートを行う
							if ((point = points.FirstOrDefault(p => p.Type == ObservationPointType.KiK_net && p.Code == strings[0] && p.Name == strings[1] && p.Region == strings[7])) != null)
							{
								point.OldLocation = new Location(float.Parse(strings[9]), float.Parse(strings[10]));
								point.Location = new Location(float.Parse(strings[3]), float.Parse(strings[4]));
								updateCount++;
								continue;
							}

							points.Add(new ObservationPoint
							{
								Type = ObservationPointType.KiK_net,
								Code = strings[0],
								IsSuspended = strings[13] == "suspension",
								Name = strings[1],
								Region = strings[7],
								Location = new Location(float.Parse(strings[3]), float.Parse(strings[4])),
								OldLocation = new Location(float.Parse(strings[9]), float.Parse(strings[10]))
							});
							addedCount++;
						}
						catch
						{
							errorCount++;
						}
					}
				}
				using (var request = new HttpRequestMessage(HttpMethod.Get, "https://www.kyoshin.bosai.go.jp/kyoshin/pubdata/knet/sitedb/sitepub_knet_sj.csv"))
				{
					request.Headers.TryAddWithoutValidation("Referer", "https://www.kyoshin.bosai.go.jp/kyoshin/db/index.html?");
					using var response = await client.SendAsync(request);
					using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(), Encoding.GetEncoding("Shift-JIS"));
					while (reader.Peek() >= 0)
					{
						try
						{
							var strings = reader.ReadLine().Split(',');

							ObservationPoint point;
							//発見したら情報のアップデートを行う
							if ((point = points.FirstOrDefault(p => p.Type == ObservationPointType.K_NET && p.Code == strings[0] && p.Name == strings[1] && p.Region == strings[7])) != null)
							{
								point.OldLocation = new Location(float.Parse(strings[9]), float.Parse(strings[10]));
								point.Location = new Location(float.Parse(strings[3]), float.Parse(strings[4]));
								updateCount++;
								continue;
							}

							points.Add(new ObservationPoint
							{
								Type = ObservationPointType.K_NET,
								Code = strings[0],
								IsSuspended = strings[13] == "suspension",
								Name = strings[1],
								Region = strings[7],
								Location = new Location(float.Parse(strings[3]), float.Parse(strings[4])),
								OldLocation = new Location(float.Parse(strings[9]), float.Parse(strings[10]))
							});
							addedCount++;
						}
						catch
						{
							errorCount++;
						}
					}
				}

				SetPoints(points.ToArray());
				MessageBox.Show($"レポート\n追加:{addedCount}件\n失敗:{errorCount}件\n更新:{updateCount}件", "処理終了", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show("NIED観測点データのインポートに失敗しました。\n" + ex, null);
			}
		}


		private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (map.IsNavigating)
				return;
			var paddedRect = map.PaddedRect;
			var centerPix = map.CenterLocation.ToPixel(map.Zoom);
			var mousePos = e.GetPosition(map);
			var mousePix = new Point(centerPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);
			var mouseLoc = mousePix.ToLocation(map.Zoom);

			map.Zoom += e.Delta / 120 * 0.25;

			var newCenterPix = map.CenterLocation.ToPixel(map.Zoom);
			var goalMousePix = mouseLoc.ToPixel(map.Zoom);

			var newMousePix = new Point(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) - (goalMousePix - newMousePix)).ToLocation(map.Zoom);
		}

		Point _prevPos;
		private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
				_prevPos = Mouse.GetPosition(map);
		}
		private void Grid_MouseMove(object sender, MouseEventArgs e)
		{
			var curPos = Mouse.GetPosition(map);
			if (map.IsNavigating)
				return;
			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				var diff = _prevPos - curPos;
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + diff).ToLocation(map.Zoom);
			}
			_prevPos = curPos;

			var rect = map.PaddedRect;

			var centerPos = map.CenterLocation.ToPixel(map.Zoom);
			var mousePos = e.GetPosition(map);
			var mouseLoc = new Point(centerPos.X + ((rect.Width / 2) - mousePos.X) + rect.Left, centerPos.Y + ((rect.Height / 2) - mousePos.Y) + rect.Top).ToLocation(map.Zoom);

			mousePosition.Text = $"Mouse Lat: {mouseLoc.Latitude:0.0000} / Lng: {mouseLoc.Longitude:0.0000}";
		}
	}
}
