using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.ObservationPointEditor.Models;
using KyoshinEewViewer.Series.ObservationPointEditor.View;
using KyoshinMonitorLib;
using ReactiveUI;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace KyoshinEewViewer.Series.ObservationPointEditor;

public class ObservationPointEditorSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(
		typeof(ObservationPointEditorSeries), 
		"observation-point-editor", 
		"観測点エディタ", 
		new FontIconSource { Glyph = "\xe70f", FontFamily = new(Utils.IconFontName) }, 
		false, 
		"観測点データの編集・管理機能を提供します。"
	);

	private KyoshinEewViewerConfiguration Config { get; }
	public ObservationPointEditorModel Model { get; }

	private ObservationPointEditorView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public override ISettingPage[] SettingPages => [];

	public ObservationPointEditorSeries(
		KyoshinEewViewerConfiguration config) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<ObservationPointEditorSeries>();

		Config = config;
		Model = new ObservationPointEditorModel();

		// モデルのプロパティ変更監視を設定
		Model.WhenAnyValue(x => x.SearchText, x => x.ShowKiKNet, x => x.ShowKNet, x => x.ShowSuspended)
			.Subscribe(_ => Model.ApplyFilter());
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
	}

	public override void Deactivated() { }

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
				
				// TODO: 成功・エラー数の表示
				System.Diagnostics.Debug.WriteLine($"CSV読み込み完了: 成功 {success}件, エラー {error}件");
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