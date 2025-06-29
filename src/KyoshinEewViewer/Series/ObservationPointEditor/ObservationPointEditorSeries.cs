using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Avalonia;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.ObservationPointEditor.Controls;
using KyoshinEewViewer.Series.ObservationPointEditor.Models;
using KyoshinEewViewer.Series.ObservationPointEditor.View;
using KyoshinEewViewer.Series.ObservationPointEditor.ViewModels;
using KyoshinEewViewer.ViewModels;
using KyoshinMonitorLib;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Series.ObservationPointEditor;

public class ObservationPointEditorSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(
		typeof(ObservationPointEditorSeries), 
		"observation-point-editor", 
		"観測点エディタ", 
		new FontIconSource { Glyph = "\xf044", FontFamily = new(Utils.IconFontName) }, 
		false, 
		"観測点データの編集・管理機能を提供します。"
	);

	private KyoshinEewViewerConfiguration Config { get; }
	public ObservationPointEditorModel Model { get; }
	public KyoshinImageMapViewModel MapViewModel { get; }

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

		// モデルのフィルター変更監視を設定
		Model.WhenAnyValue(x => x.SearchText).Subscribe(_ => Model.ApplyFilter());
		Model.WhenAnyValue(x => x.ShowKiKNet).Subscribe(_ => Model.ApplyFilter());
		Model.WhenAnyValue(x => x.ShowKNet).Subscribe(_ => Model.ApplyFilter());
		Model.WhenAnyValue(x => x.ShowSuspended).Subscribe(_ => Model.ApplyFilter());

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

	public override void Deactivated() 
	{
		MapViewModel.Dispose();
	}

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
		MapViewModel.ObservationPoints = Model.FilteredObservationPoints.ToArray();

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
	private void SetupEventHandlers()
	{
		// 観測点移動イベント
		MapViewModel.ObservationPointMoved += OnObservationPointMoved;
	}


	/// <summary>
	/// 観測点移動イベントハンドラー
	/// </summary>
	private void OnObservationPointMoved(object? sender, ObservationPointMovedEventArgs e)
	{
		// 観測点の位置を更新
		e.ObservationPoint.Point = e.NewPosition;
		Model.UpdateObservationPoint();
	}

	/// <summary>
	/// 削除確認ダイアログを表示する
	/// </summary>
	private async Task ShowDeleteConfirmationDialog(ObservationPoint point)
	{
		var result = await new ContentDialog
		{
			Title = "観測点の削除",
			Content = $"観測点「{point.Name} ({point.Code})」を削除しますか？",
			PrimaryButtonText = "削除",
			SecondaryButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Secondary
		}.ShowAsync(KyoshinEewViewerApp.TopLevelControl as Window);

		if (result == ContentDialogResult.Primary)
		{
			Model.RemoveObservationPoint(point);
		}
	}

	#endregion

	#region ファイル操作メソッド

	public async void LoadFromMessagePack()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
			{
				Title = "MessagePackファイルを開く",
				FileTypeFilter = [
					new("MessagePack") { Patterns = ["*.mpk", "*.mpk.lz4"] },
					new("すべてのファイル") { Patterns = ["*"] }
				],
				AllowMultiple = false,
			});

			if (files?.Count > 0)
			{
				var filePath = files[0].Path.LocalPath;
				var useLz4 = Path.GetExtension(filePath).ToLowerInvariant() == ".lz4";
				var points = ObservationPoint.LoadFromMpk(filePath, useLz4);
				Model.SetObservationPoints(points);
				Model.CurrentFilePath = filePath;
			}
		}
		catch (Exception ex)
		{
			// TODO: エラーハンドリング
			System.Diagnostics.Debug.WriteLine($"MessagePack読み込みエラー: {ex.Message}");
		}
	}

	public async void LoadFromJson()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
			{
				Title = "JSONファイルを開く",
				FileTypeFilter = [
					new("JSON") { Patterns = ["*.json"] },
					new("すべてのファイル") { Patterns = ["*"] }
				],
				AllowMultiple = false,
			});

			if (files?.Count > 0)
			{
				var filePath = files[0].Path.LocalPath;
				var points = ObservationPoint.LoadFromJson(filePath);
				if (points != null)
					Model.SetObservationPoints(points);
				Model.CurrentFilePath = filePath;
			}
		}
		catch (Exception ex)
		{
			// TODO: エラーハンドリング
			System.Diagnostics.Debug.WriteLine($"JSON読み込みエラー: {ex.Message}");
		}
	}

	public async void LoadFromCsv()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions()
			{
				Title = "CSVファイルを開く",
				FileTypeFilter = [
					new("CSV") { Patterns = ["*.csv"] },
					new("すべてのファイル") { Patterns = ["*"] }
				],
				AllowMultiple = false,
			});

			if (files?.Count > 0)
			{
				var filePath = files[0].Path.LocalPath;
				var (points, success, error) = ObservationPoint.LoadFromCsv(filePath);
				Model.SetObservationPoints(points);
				Model.CurrentFilePath = filePath;
			}
		}
		catch (Exception ex)
		{
			// TODO: エラーハンドリング
			System.Diagnostics.Debug.WriteLine($"CSV読み込みエラー: {ex.Message}");
		}
	}

	public async void SaveToMessagePack()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var file = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions()
			{
				Title = "MessagePackファイルに保存",
				FileTypeChoices = [
					new("MessagePack (LZ4圧縮)") { Patterns = ["*.mpk.lz4"] },
					new("MessagePack") { Patterns = ["*.mpk"] }
				],
				DefaultExtension = "mpk.lz4"
			});

			if (file != null)
			{
				var filePath = file.Path.LocalPath;
				var useLz4 = Path.GetExtension(filePath).ToLowerInvariant() == ".lz4";
				ObservationPoint.SaveToMpk(filePath, Model.ObservationPoints, useLz4);
				Model.CurrentFilePath = filePath;
				Model.IsModified = false;
			}
		}
		catch (Exception ex)
		{
			// TODO: エラーハンドリング
			System.Diagnostics.Debug.WriteLine($"MessagePack保存エラー: {ex.Message}");
		}
	}

	public async void SaveToJson()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var file = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions()
			{
				Title = "JSONファイルに保存",
				FileTypeChoices = [
					new("JSON") { Patterns = ["*.json"] }
				],
				DefaultExtension = "json"
			});

			if (file != null)
			{
				var filePath = file.Path.LocalPath;
				ObservationPoint.SaveToJson(filePath, Model.ObservationPoints);
				Model.CurrentFilePath = filePath;
				Model.IsModified = false;
			}
		}
		catch (Exception ex)
		{
			// TODO: エラーハンドリング
			System.Diagnostics.Debug.WriteLine($"JSON保存エラー: {ex.Message}");
		}
	}

	public async void SaveToCsv()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null) return;

			var file = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions()
			{
				Title = "CSVファイルに保存",
				FileTypeChoices = [
					new("CSV") { Patterns = ["*.csv"] }
				],
				DefaultExtension = "csv"
			});

			if (file != null)
			{
				var filePath = file.Path.LocalPath;
				ObservationPoint.SaveToCsv(filePath, Model.ObservationPoints);
				Model.CurrentFilePath = filePath;
				Model.IsModified = false;
			}
		}
		catch (Exception ex)
		{
			// TODO: エラーハンドリング
			System.Diagnostics.Debug.WriteLine($"CSV保存エラー: {ex.Message}");
		}
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

	#endregion
}
