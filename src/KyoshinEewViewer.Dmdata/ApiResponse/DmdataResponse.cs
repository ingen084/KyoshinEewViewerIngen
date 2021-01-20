using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Dmdata.ApiResponse
{
	/// <summary>
	/// dmdataのレスポンスを表す基底クラス
	/// </summary>
	public abstract class DmdataResponse
	{
		/// <summary>
		/// API処理ID
		/// </summary>
		[JsonPropertyName("responseId")]
		public string ResponseId { get; set; }
		/// <summary>
		/// API処理時刻
		/// </summary>
		[JsonPropertyName("responseTime")]
		public DateTime ResponseTime { get; set; }
		/// <summary>
		/// API結果
		/// <para>"ok" or "error"</para>
		/// </summary>
		[JsonPropertyName("status")]
		public string Status { get; set; }

		/// <summary>
		/// エラー内容
		/// </summary>
		[JsonPropertyName("error")]
		public DmdataError Error { get; set; }
	}

	/// <summary>
	/// dmdataのエラー内容
	/// </summary>
	public class DmdataError
	{
		/// <summary>
		/// エラーメッセージ
		/// </summary>
		[JsonPropertyName("message")]
		public string Message { get; set; }
		/// <summary>
		/// HTTPステータスコード
		/// </summary>
		[JsonPropertyName("code")]
		public int Code { get; set; }
	}
}
