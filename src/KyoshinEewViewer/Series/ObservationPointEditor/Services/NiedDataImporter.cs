using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Series.ObservationPointEditor.Models;
using KyoshinMonitorLib;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace KyoshinEewViewer.Series.ObservationPointEditor.Services;

/// <summary>
/// NIED観測点データのインポートを行うサービス
/// </summary>
public static class NiedDataImporter
{
	/// <summary>
	/// ローカルファイルからNIEDデータをインポートする（ディレクトリ選択）
	/// </summary>
	public static async Task<(int addedCount, int updateCount, int errorCount)?> ImportFromLocalFiles(
		ObservationPointEditorModel model, 
		Window parent)
	{
		// ディレクトリ選択ダイアログを表示
		var folderPicker = await parent.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
		{
			Title = "NIEDデータ（CSVファイル）が格納されているディレクトリを選択してください",
			AllowMultiple = false
		});

		if (folderPicker == null || folderPicker.Count == 0)
			return null; // キャンセルされた

		var selectedFolder = folderPicker[0].Path.LocalPath;
		return ImportFromDirectory(selectedFolder, model);
	}

	/// <summary>
	/// 指定されたディレクトリからNIEDデータをインポートする
	/// </summary>
	public static (int addedCount, int updateCount, int errorCount) ImportFromDirectory(
		string directoryPath, 
		ObservationPointEditorModel model)
	{
		// Shift-JISエンコーディングプロバイダーを登録
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		
		var kikNetPath = Path.Combine(directoryPath, "sitepub_kik_sj.csv");
		var kNetPath = Path.Combine(directoryPath, "sitepub_knet_sj.csv");

		if (!File.Exists(kikNetPath))
			throw new FileNotFoundException($"sitepub_kik_sj.csvが見つかりません: {kikNetPath}");
		if (!File.Exists(kNetPath))
			throw new FileNotFoundException($"sitepub_knet_sj.csvが見つかりません: {kNetPath}");

		var addedCount = 0;
		var updateCount = 0;
		var errorCount = 0;

		// KiK-netデータのインポート
		using (var reader = new StreamReader(kikNetPath, Encoding.GetEncoding("Shift-JIS")))
		{
			var (added, updated, errors) = ImportCsvData(reader, ObservationPointType.KiK_net, model);
			addedCount += added;
			updateCount += updated;
			errorCount += errors;
		}

		// K-NETデータのインポート
		using (var reader = new StreamReader(kNetPath, Encoding.GetEncoding("Shift-JIS")))
		{
			var (added, updated, errors) = ImportCsvData(reader, ObservationPointType.K_NET, model);
			addedCount += added;
			updateCount += updated;
			errorCount += errors;
		}

		return (addedCount, updateCount, errorCount);
	}


	/// <summary>
	/// CSVデータをインポートする
	/// </summary>
	private static (int addedCount, int updateCount, int errorCount) ImportCsvData(
		TextReader reader,
		ObservationPointType type,
		ObservationPointEditorModel model)
	{
		var addedCount = 0;
		var updateCount = 0;
		var errorCount = 0;
		string? line;

		while ((line = reader.ReadLine()) != null)
		{
			try
			{
				var strings = line.Split(',');
				if (strings.Length < 12)
					continue;

				// 既存の観測点を検索
				var existingPoint = model.ObservationPoints.FirstOrDefault(p =>
					p.Type == type &&
					p.Code == strings[0]);

				if (existingPoint != null)
				{
					// 既存データの更新
					existingPoint.IsSuspended = strings[11] == "suspension";
					existingPoint.Name = strings[1];
					existingPoint.Region = strings[7];
					existingPoint.Location = new KyoshinMonitorLib.Location(
						float.Parse(strings[3]),
						float.Parse(strings[4])
					);
					updateCount++;
				}
				else
				{
					// 新規追加
					var newPoint = new ObservationPoint
					{
						Type = type,
						Code = strings[0],
						IsSuspended = strings[11] == "suspension",
						Name = strings[1],
						Region = strings[7],
						Location = new KyoshinMonitorLib.Location(
							float.Parse(strings[3]), float.Parse(strings[4])),
					};
					model.AddObservationPoint(newPoint);
					addedCount++;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"CSVパースエラー: {ex.Message}");
				errorCount++;
			}
		}

		return (addedCount, updateCount, errorCount);
	}

	/// <summary>
	/// インポート確認ダイアログを表示する
	/// </summary>
	public static async Task<bool> ShowImportConfirmationDialog(Window parent)
	{
		var dialog = new ContentDialog
		{
			Title = "NIED観測点データのインポート",
			Content = "NIED観測点データをインポートします。\n登録されていない地点情報が追加で登録されます。\n\n必要なファイル:\n• sitepub_kik_sj.csv（KiK-netデータ）\n• sitepub_knet_sj.csv（K-NETデータ）\n\n続行しますか？",
			PrimaryButtonText = "続行",
			SecondaryButtonText = "キャンセル",
			DefaultButton = ContentDialogButton.Secondary
		};

		return await dialog.ShowAsync(parent) == ContentDialogResult.Primary;
	}


	/// <summary>
	/// インポート結果ダイアログを表示する
	/// </summary>
	public static async Task ShowImportResultDialog(Window parent, int addedCount, int updateCount, int errorCount) =>
		await new ContentDialog
		{
			Title = "インポート完了",
			Content = $"レポート\n追加: {addedCount}件\n更新: {updateCount}件\nエラー: {errorCount}件",
			CloseButtonText = "OK"
		}.ShowAsync(parent);
}
