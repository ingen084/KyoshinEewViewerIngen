using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Avalonia;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Series.ObservationPointEditor.Controls;
using KyoshinEewViewer.Series.ObservationPointEditor.Layers;
using KyoshinEewViewer.Series.ObservationPointEditor.Models;
using KyoshinEewViewer.Series.ObservationPointEditor.Services;
using KyoshinEewViewer.Series.ObservationPointEditor.View;
using KyoshinEewViewer.Series.ObservationPointEditor.ViewModels;
using KyoshinEewViewer.ViewModels;
using KyoshinMonitorLib;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KyoshinEewViewer.CustomControl;
using System.Reactive.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.ObservationPointEditor;

public class ObservationPointEditorSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(
		typeof(ObservationPointEditorSeries),
		"observation-point-editor",
		"観測点編集",
		new FontIconSource { Glyph = "\xf044", FontFamily = new(Utils.IconFontName) },
		false,
		"強震観測点データの編集・管理機能を提供します。"
	);

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		PropertyNameCaseInsensitive = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
		Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
		WriteIndented = true
	};

	private KyoshinEewViewerConfiguration Config { get; }
	public ObservationPointEditorModel Model { get; }
	public KyoshinImageMapViewModel MapViewModel { get; }
	public ObservationPointEditorLayer EditorLayer { get; }

	private ObservationPointEditorView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public override ISettingPage[] SettingPages => [];

	public ObservationPointEditorSeries(
		KyoshinEewViewerConfiguration config) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<ObservationPointEditorSeries>();

		Config = config;
		Model = new ObservationPointEditorModel();
		MapViewModel = new KyoshinImageMapViewModel();
		EditorLayer = new ObservationPointEditorLayer(Model);

		// MapDisplayParameterにレイヤーを設定
		MapDisplayParameter = new MapDisplayParameter
		{
			OverlayLayers = [EditorLayer],
			Padding = new Thickness(0, 0, 0, 0)
		};

		// モデルのフィルター変更監視を設定
		Model.WhenAnyValue(x => x.SearchText).Subscribe(_ => Model.ApplyFilter());
		Model.WhenAnyValue(x => x.ShowKiKNet).Subscribe(_ => Model.ApplyFilter());
		Model.WhenAnyValue(x => x.ShowKNet).Subscribe(_ => Model.ApplyFilter());
		Model.WhenAnyValue(x => x.ShowSuspended).Subscribe(_ => Model.ApplyFilter());

		// 選択観測点の変更を監視してメイン地図をナビゲート
		Model.WhenAnyValue(x => x.SelectedObservationPoint)
			.Subscribe(point =>
			{
				if (point == null || point.Location == null)
				{
					// 選択が解除された場合はナビゲーションリクエストをクリア
					MapNavigationRequest = null;
					return;
				}

				const double margin = 0.1;
				var centerLat = point.Location.Latitude;
				var centerLng = point.Location.Longitude;

				var bound = new Rect(
					centerLat - margin, centerLng - margin,
					margin * 2, margin * 2
				);
				var mustBound = new Rect(
					centerLat - margin, centerLng - margin,
					margin * 0.1, margin * 0.1
				);

				MapNavigationRequest = new(bound, mustBound);
			});

		// ModelとMapViewModelのバインディング設定
		SetupModelBindings();

		// LeftBottomRectの変更を監視してMapPaddingを更新
		MapViewModel.WhenAnyValue(x => x.LeftBottomRect)
			.Subscribe(rect =>
			{
				// 左下パネルの幅と高さを取得してMapPaddingに設定
				var leftPadding = rect.Width > 0 ? rect.Width + 10 : 0; // 10はマージン
				var bottomPadding = rect.Height > 0 ? rect.Height + 10 : 0; // 10はマージン
				MapDisplayParameter = MapDisplayParameter with
				{
					Padding = new Thickness(leftPadding, 0, 0, bottomPadding)
				};
			});
	}

	public override void Initialize()
	{
		// 初期化処理（必要に応じて）
	}

	public override void Activating()
	{
		if (_control != null)
			return;

		_control = new ObservationPointEditorView
		{
			DataContext = this
		};

		// イベントハンドラーの設定
		SetupEventHandlers();
	}

	public override void Deactivated() => MapViewModel.Dispose();

	#region バインディング設定

	/// <summary>
	/// ModelとMapViewModelのバインディングを設定する
	/// </summary>
	private void SetupModelBindings()
	{
		// ObservableCollection → Array の変換バインディング
		Model.FilteredObservationPoints.CollectionChanged += (sender, e)
			=> MapViewModel.ObservationPoints = Model.FilteredObservationPoints.ToArray();

		// 初期値の設定
		MapViewModel.ObservationPoints = [];

		// 選択状態の双方向バインディング
		Model.WhenAnyValue(x => x.SelectedObservationPoint)
			.BindTo(MapViewModel, x => x.SelectedObservationPoint);

		// MapViewModelからModelへの選択状態同期
		MapViewModel.WhenAnyValue(x => x.SelectedObservationPoint)
			.Where(x => x != Model.SelectedObservationPoint)
			.Subscribe(x => Model.SelectedObservationPoint = x);
	}

	/// <summary>
	/// イベントハンドラーを設定する
	/// </summary>
	private void SetupEventHandlers() => MapViewModel.ObservationPointMoved += OnObservationPointMoved;


	/// <summary>
	/// 観測点移動イベントハンドラー
	/// </summary>
	private void OnObservationPointMoved(object? sender, ObservationPointMovedEventArgs e)
	{
		// 変更履歴を記録
		var oldPoint = e.CommonObservationPoint.Point;
		var newPoint = e.NewPosition;

		// 観測点の位置を更新
		// 注意: ここでは画像上のピクセル座標を直接Point2として設定しています
		// 実際のCenter/Offset計算は保存時に行われます
		e.CommonObservationPoint.Point = newPoint;
		Model.UpdateObservationPoint();

		// Undo履歴に追加
		Model.RecordChange(e.CommonObservationPoint, oldPoint, newPoint);
	}

	/// <summary>
	/// 最後の変更をUndoする
	/// </summary>
	public void UndoLastChange() => Model.Undo();

	/// <summary>
	/// 最後にUndoした変更をRedoする
	/// </summary>
	public void RedoLastChange() => Model.Redo();


	#endregion

	#region ファイル操作メソッド

	/// <summary>
	/// JSONファイルを開く
	/// </summary>
	public async void OpenFile()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
			{
				Title = "観測点JSONファイルを開く",
				FileTypeFilter = [
					new("JSON") { Patterns = ["*.json"] },
					new("すべてのファイル") { Patterns = ["*"] }
				],
				AllowMultiple = false,
			});

			if (files?.Count > 0)
			{
				var filePath = files[0].Path.LocalPath;
				await LoadFromFile(filePath);
			}
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("ファイル読み込みエラー", $"ファイルの読み込みに失敗しました。\n\n{ex.Message}");
		}
	}

	/// <summary>
	/// JSONファイルに保存
	/// </summary>
	public async void SaveFile()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var file = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions()
			{
				Title = "観測点JSONファイルに保存",
				FileTypeChoices = [
					new("JSON") { Patterns = ["*.json"] }
				],
				DefaultExtension = "json"
			});

			if (file != null)
			{
				var filePath = file.Path.LocalPath;
				await SaveToFile(filePath);
			}
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("ファイル保存エラー", $"ファイルの保存に失敗しました。\n\n{ex.Message}");
		}
	}

	/// <summary>
	/// JSONファイルから観測点データを読み込む
	/// </summary>
	private async Task LoadFromFile(string filePath)
	{
		try
		{
			// CommonObservationPointとしてJSONを読み込む
			var commonPoints = await JsonSerializer.DeserializeAsync<CommonObservationPoint[]>(File.OpenRead(filePath), JsonOptions)
				?? throw new InvalidOperationException("JSONファイルの読み込みに失敗しました。");

			Model.SetObservationPoints(commonPoints);
			Model.CurrentFilePath = filePath;
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("ファイル読み込みエラー", $"ファイルの読み込み中にエラーが発生しました。\n\n{ex.Message}");
		}
	}

	/// <summary>
	/// 観測点データをJSONファイルに保存する
	/// </summary>
	private async Task SaveToFile(string filePath)
	{
		try
		{
			// JSONとして保存
			var json = JsonSerializer.Serialize(Model.ObservationPoints.ToArray(), JsonOptions);
			await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

			Model.CurrentFilePath = filePath;
			Model.IsModified = false;
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("ファイル保存エラー", $"ファイルの保存中にエラーが発生しました。\n\n{ex.Message}");
		}
	}

	/// <summary>
	/// エラーダイアログを表示する
	/// </summary>
	private static async Task ShowErrorDialog(string title, string message)
	{
		if (KyoshinEewViewerApp.TopLevelControl is not Window tlc) return;

		await new ContentDialog
		{
			Title = title,
			Content = message,
			CloseButtonText = "OK"
		}.ShowAsync(tlc);
	}

	#endregion

	#region 編集メソッド

	public void AddObservationPoint()
	{
		var newPoint = Model.CreateNewObservationPoint();
		Model.AddObservationPoint(newPoint);
		Model.SelectedObservationPoint = newPoint;
	}

	public void RemoveObservationPoint()
	{
		if (Model.SelectedObservationPoint != null)
		{
			Model.RemoveObservationPoint(Model.SelectedObservationPoint);
		}
	}

	/// <summary>
	/// 重複観測点を統合する
	/// </summary>
	public async void ConsolidateDuplicateObservationPoints()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl is not Window tlc) return;

			// 確認ダイアログを表示
			var confirmResult = await new ContentDialog
			{
				Title = "重複観測点の統合",
				Content = "観測点コードが同じ重複観測点を統合します。\n強震モニタ座標があるデータを優先して統合します。\n\nこの操作を実行しますか？",
				PrimaryButtonText = "統合実行",
				SecondaryButtonText = "キャンセル",
				DefaultButton = ContentDialogButton.Secondary
			}.ShowAsync(tlc);

			if (confirmResult != ContentDialogResult.Primary)
				return;

			// 統合処理を実行
			var result = Model.ConsolidateDuplicateObservationPoints();

			// 結果を表示
			await ShowConsolidationResultDialog(tlc, result);
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("重複統合エラー", $"重複観測点の統合処理中にエラーが発生しました。\n\n{ex.Message}");
		}
	}

	/// <summary>
	/// 統合結果ダイアログを表示する
	/// </summary>
	private static async Task ShowConsolidationResultDialog(Window parentWindow, DuplicateConsolidationResult result)
	{
		string content;
		string title;

		if (!result.HasDuplicates)
		{
			title = "統合完了";
			content = "重複する観測点は見つかりませんでした。";
		}
		else
		{
			title = "統合完了";
			var summary = $"統合結果:\n" +
						  $"• 重複グループ数: {result.GroupCount}グループ\n" +
						  $"• 削除された観測点数: {result.RemovedCount}件\n\n";

			var details = "統合詳細:\n";
			foreach (var point in result.ConsolidatedPoints.Take(10)) // 最初の10件のみ表示
				details += $"• {point.Code} ({point.Name}) - {point.Reason}\n";

			if (result.ConsolidatedPoints.Count > 10)
				details += $"• ...他{result.ConsolidatedPoints.Count - 10}件\n";

			content = summary + details;
		}

		await new ContentDialog
		{
			Title = title,
			Content = content,
			CloseButtonText = "OK"
		}.ShowAsync(parentWindow);
	}

	#endregion

	#region NIEDデータインポート

	/// <summary>
	/// NIED観測点データをインポートする
	/// </summary>
	public async Task ImportFromNied()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl is not Window tlc) return;

			// 確認ダイアログを表示
			if (!await NiedDataImporter.ShowImportConfirmationDialog(tlc))
				return;

			// ディレクトリ選択とインポート
			var result = await NiedDataImporter.ImportFromLocalFiles(Model, tlc);
			if (result == null)
				return; // キャンセルされた

			var (addedCount, updateCount, errorCount) = result.Value;

			// 結果を表示
			await NiedDataImporter.ShowImportResultDialog(tlc, addedCount, updateCount, errorCount);
		}
		catch (FileNotFoundException ex)
		{
			await ShowErrorDialog("NIEDデータインポートエラー", ex.Message);
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("NIEDデータインポートエラー", $"NIED観測点データのインポートに失敗しました。\n\n{ex.Message}");
		}
	}

	#endregion
}
