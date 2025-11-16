using DmdataSharp.Exceptions;
using System;
using System.Net.Http;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

/// <summary>
/// Dmdataのエラーを分類し、適切な復旧戦略を決定する
/// </summary>
public static class DmdataErrorClassifier
{
	/// <summary>
	/// エラーの種類
	/// </summary>
	public enum ErrorType
	{
		/// <summary>
		/// 認証エラー - 認証情報を失効させる必要がある
		/// </summary>
		Authentication,

		/// <summary>
		/// ローカルネットワークエラー - フォールバックせず再接続を待つ
		/// </summary>
		LocalNetwork,

		/// <summary>
		/// サービス側エラー - フォールバックまたは一時障害として扱う
		/// </summary>
		ServiceSide
	}

	/// <summary>
	/// 例外からエラー種別を判定する
	/// </summary>
	/// <param name="ex">判定する例外</param>
	/// <returns>エラー種別</returns>
	public static ErrorType ClassifyError(Exception ex)
	{
		if (IsAuthenticationError(ex))
			return ErrorType.Authentication;

		if (IsNetworkError(ex))
			return ErrorType.LocalNetwork;

		return ErrorType.ServiceSide;
	}

	/// <summary>
	/// エラーコードからエラー種別を判定する
	/// </summary>
	/// <param name="errorCode">判定するエラーコード</param>
	/// <returns>認証エラーの場合true</returns>
	public static bool IsAuthenticationErrorCode(string? errorCode)
	{
		return errorCode switch
		{
			"401" or "401-1" or "401-2" or "401-3" => true,
			_ => false
		};
	}

	/// <summary>
	/// 認証エラーかどうかを判定する
	/// </summary>
	private static bool IsAuthenticationError(Exception ex)
	{
		return ex switch
		{
			DmdataAuthenticationException => true,
			DmdataException dmdataEx when dmdataEx.Message.Contains("401") => true,
			_ => false
		};
	}

	/// <summary>
	/// ローカルネットワークの問題かどうかを判定する
	/// </summary>
	/// <param name="ex">判定する例外</param>
	/// <returns>ローカルネットワーク問題の場合true、サービス側の問題の場合false</returns>
	private static bool IsNetworkError(Exception ex)
	{
		// HttpRequestExceptionの場合、メッセージから判定
		if (ex is HttpRequestException httpEx)
		{
			var message = httpEx.Message.ToLower();

			// 明確なネットワーク到達不能エラー
			if (message.Contains("network is unreachable"))
				return true;

			// それ以外はサービス側の問題として扱う
			return false;
		}

		// DmdataApiTimeoutExceptionの場合、内部例外を再帰的に確認
		if (ex is DmdataApiTimeoutException timeoutEx)
		{
			// タイムアウトの原因を調べる
			if (timeoutEx.InnerException != null)
				return IsNetworkError(timeoutEx.InnerException);

			// 内部例外がない場合、メッセージから判定
			var message = timeoutEx.Message.ToLower();
			if (message.Contains("network") || message.Contains("connection"))
				return true;

			// 判定できない場合はサービス側の問題として扱う(フォールバック実行)
			return false;
		}

		// SocketExceptionは明確にネットワーク問題
		if (ex is System.Net.Sockets.SocketException)
			return true;

		// その他の例外はサービス側の問題として扱う
		return false;
	}
}
