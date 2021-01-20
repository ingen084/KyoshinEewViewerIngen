using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Dmdata.ApiResponse
{
	/// <summary>
	/// dmdataへの課金･契約状況を表す
	/// </summary>
	public class BillingResponse : DmdataResponse
	{
		/// <summary>
		/// 時間
		/// </summary>
		[JsonPropertyName("date")]
		public DateTime Date { get; set; }
		/// <summary>
		/// 課金項目
		/// </summary>
		[JsonPropertyName("items")]
		public BillingItem[] Items { get; set; }
		/// <summary>
		/// 請求金額
		/// </summary>
		[JsonPropertyName("amount")]
		public BillingAmount Amount { get; set; }
		/// <summary>
		/// DiMis残高
		/// </summary>
		[JsonPropertyName("unpaid")]
		public int Unpaid { get; set; }

	}
	/// <summary>
	/// 請求金額
	/// </summary>
	public class BillingAmount
	{
		/// <summary>
		/// 今月の請求金額
		/// </summary>
		[JsonPropertyName("total")]
		public int Total { get; set; }
	}
	/// <summary>
	/// 課金項目
	/// </summary>
	public class BillingItem
	{
		/// <summary>
		/// アイテムID
		/// </summary>
		[JsonPropertyName("type")]
		public string Type { get; set; }
		/// <summary>
		/// 表示名
		/// </summary>
		[JsonPropertyName("name")]
		public string Name { get; set; }
		/// <summary>
		/// 今月消費している金額
		/// </summary>
		[JsonPropertyName("subtotal")]
		public int Subtotal { get; set; }
		/// <summary>
		/// 契約期間一覧
		/// </summary>
		[JsonPropertyName("list")]
		public ContractPeriod[] List { get; set; }
	}

	/// <summary>
	/// 契約期間を表す
	/// </summary>
	public class ContractPeriod
	{
		/// <summary>
		/// 契約開始時刻
		/// </summary>
		[JsonPropertyName("start")]
		public DateTime Start { get; set; }
		/// <summary>
		/// 契約終了時刻 nullの場合現在契約中
		/// </summary>
		[JsonPropertyName("end")]
		public DateTime? End { get; set; }
	}
}
