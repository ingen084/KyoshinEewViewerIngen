using System;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

/// <summary>
/// 各ソースで受信した EEW を受け付ける
/// </summary>
public interface IEewController
{
	/// <summary>
	/// 複数のソースで発生したEEWを統合して管理する
	/// </summary>
	/// <param name="eew">発生したEEW</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	void Update(Models.Eew eew, DateTime updatedTime);

	/// <summary>
	/// EEWをキャンセルする
	/// </summary>
	/// <param name="key">キャンセルされた EEW のキー (<see cref="Models.Eew.Key"/>) 訓練以外は地震IDと同じ / null または空の場合は強震モニタの過去のEEWをキャンセル扱いにする</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	void Cancelled(string? key, DateTime updatedTime);

	/// <summary>
	/// 警報地域のみの更新を行う
	/// </summary>
	void UpdateWarning(Models.Eew eew, DateTime updatedTime);

	/// <summary>
	/// 警報電文をキャンセルする
	/// </summary>
	/// <param name="key">キャンセルされた EEW のキー (<see cref="Models.Eew.Key"/>) 訓練以外は地震IDと同じ</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	void WarningCancelled(string key, DateTime updatedTime);
}
