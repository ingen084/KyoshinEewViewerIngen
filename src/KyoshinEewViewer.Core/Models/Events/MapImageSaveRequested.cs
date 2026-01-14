namespace KyoshinEewViewer.Core.Models.Events;

/// <summary>
/// 地図画像の保存リクエストイベント
/// </summary>
public class MapImageSaveRequested
{
	/// <summary>
	/// 保存先のファイルパス（nullの場合はファイル選択ダイアログを表示）
	/// </summary>
	public string? TargetPath { get; init; }
}
