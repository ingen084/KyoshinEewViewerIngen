using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Models
{
	public class Eew
	{
		public EewSource Source { get; set; }

		/// <summary>
		/// 地震ID
		/// </summary>
		public string Id { get; set; }
		/// <summary>
		/// キャンセル報か
		/// </summary>
		public bool IsCancelled { get; set; }
		/// <summary>
		/// 受信時刻
		/// </summary>
		public DateTime ReceiveTime { get; set; }
		/// <summary>
		/// このソフトで更新した時刻
		/// </summary>
		public DateTime UpdatedTime { get; set; }
		/// <summary>
		/// 最大震度
		/// </summary>
		public JmaIntensity Intensity { get; set; }
		/// <summary>
		/// 地震の発生時間
		/// </summary>
		public DateTime OccurrenceTime { get; set; }
		/// <summary>
		/// 震央地名
		/// </summary>
		public string Place { get; set; }
		/// <summary>
		/// 震央座標
		/// </summary>
		public Location Location { get; set; }
		/// <summary>
		/// マグニチュード
		/// </summary>
		public float Magnitude { get; set; }
		/// <summary>
		/// 震源の深さ
		/// </summary>
		public int Depth { get; set; }
		/// <summary>
		/// 報数
		/// </summary>
		public int Count { get; set; }
		/// <summary>
		/// 警報状態か
		/// </summary>
		public bool IsWarning { get; set; }
		/// <summary>
		/// 最終報か
		/// </summary>
		public bool IsFinal { get; set; }

		/// <summary>
		/// PLUM報か
		/// </summary>
		public bool IsPLUM => Depth == 10 && Magnitude == 1.0;
		public string WarningString => IsWarning ? "Warning" : "";
		public string Title => $"緊急地震速報({(IsFinal ? "最終" : $"第{(IsCancelled ? "--" : Count.ToString("d2"))}")}報) {ReceiveTime:HH:mm:ss}受信";
		public string PlaceString => IsCancelled ? "キャンセルor受信範囲外" : (Place ?? "不明(未受信)");
	}

	public enum EewSource
	{
		NIED,
		TheLast10Second,
		SignalNowProfessional,
	}
}