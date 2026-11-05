using DmdataSharp.Exceptions;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// DmdataErrorClassifierのテスト
/// </summary>
public class DmdataErrorClassifierTests
{
	[Fact(DisplayName = "認証エラーコードの判定が正しく動作する")]
	public void IsAuthenticationErrorCode_認証エラーコード_Trueを返す()
	{
		// Arrange & Act & Assert
		Assert.True(DmdataErrorClassifier.IsAuthenticationErrorCode("401"));
		Assert.True(DmdataErrorClassifier.IsAuthenticationErrorCode("401-1"));
		Assert.True(DmdataErrorClassifier.IsAuthenticationErrorCode("401-2"));
		Assert.True(DmdataErrorClassifier.IsAuthenticationErrorCode("401-3"));
	}

	[Fact(DisplayName = "非認証エラーコードの判定が正しく動作する")]
	public void IsAuthenticationErrorCode_非認証エラーコード_Falseを返す()
	{
		// Arrange & Act & Assert
		Assert.False(DmdataErrorClassifier.IsAuthenticationErrorCode("400"));
		Assert.False(DmdataErrorClassifier.IsAuthenticationErrorCode("404"));
		Assert.False(DmdataErrorClassifier.IsAuthenticationErrorCode("500"));
		Assert.False(DmdataErrorClassifier.IsAuthenticationErrorCode(null));
		Assert.False(DmdataErrorClassifier.IsAuthenticationErrorCode(""));
	}

	[Fact(DisplayName = "トークンの更新で認可情報が拒否された場合は認証エラー、認可サーバーの障害は一時的な障害と判定される")]
	public void ClassifyError_DmdataAuthenticationException_認可情報の拒否のみ認証エラー()
	{
		// DmdataSharp がトークンエンドポイントの応答から生成するメッセージ
		string[] rejected = [
			"アクセストークンの更新に失敗しました invalid_grant(refresh token is invalid)",
			"アクセストークンの更新に失敗しました invalid_client(client is invalid)",
			"アクセストークンのスコープが足りていません",
		];
		string[] serverErrors = [
			"アクセストークンの更新に失敗しました ()",
			"アクセストークンの更新に失敗しました server_error(internal error)",
			"レスポンスをパースできませんでした",
		];

		foreach (var message in rejected)
			Assert.Equal(DmdataErrorClassifier.ErrorType.Authentication, DmdataErrorClassifier.ClassifyError(new DmdataAuthenticationException(message)));
		foreach (var message in serverErrors)
			Assert.Equal(DmdataErrorClassifier.ErrorType.ServiceSide, DmdataErrorClassifier.ClassifyError(new DmdataAuthenticationException(message)));
	}

	[Fact(DisplayName = "401メッセージを含むDmdataException_認証エラーと判定される")]
	public void ClassifyError_DmdataException401_認証エラー()
	{
		// Arrange
		var exception = new DmdataException("エラー: 401 Unauthorized");

		// Act
		var result = DmdataErrorClassifier.ClassifyError(exception);

		// Assert
		Assert.Equal(DmdataErrorClassifier.ErrorType.Authentication, result);
	}

	[Fact(DisplayName = "NetworkUnreachableエラー_ローカルネットワークエラーと判定される")]
	public void ClassifyError_NetworkUnreachable_ローカルネットワークエラー()
	{
		// Arrange
		var exception = new HttpRequestException("Network is unreachable");

		// Act
		var result = DmdataErrorClassifier.ClassifyError(exception);

		// Assert
		Assert.Equal(DmdataErrorClassifier.ErrorType.LocalNetwork, result);
	}

	[Fact(DisplayName = "SocketException_ローカルネットワークエラーと判定される")]
	public void ClassifyError_SocketException_ローカルネットワークエラー()
	{
		// Arrange
		var exception = new System.Net.Sockets.SocketException();

		// Act
		var result = DmdataErrorClassifier.ClassifyError(exception);

		// Assert
		Assert.Equal(DmdataErrorClassifier.ErrorType.LocalNetwork, result);
	}

	[Fact(DisplayName = "一般的なHttpRequestException_サービス側エラーと判定される")]
	public void ClassifyError_HttpRequestException_サービス側エラー()
	{
		// Arrange
		var exception = new HttpRequestException("Server error");

		// Act
		var result = DmdataErrorClassifier.ClassifyError(exception);

		// Assert
		Assert.Equal(DmdataErrorClassifier.ErrorType.ServiceSide, result);
	}

	[Fact(DisplayName = "その他の例外_サービス側エラーと判定される")]
	public void ClassifyError_OtherException_サービス側エラー()
	{
		// Arrange
		var exception = new InvalidOperationException("無効な操作");

		// Act
		var result = DmdataErrorClassifier.ClassifyError(exception);

		// Assert
		Assert.Equal(DmdataErrorClassifier.ErrorType.ServiceSide, result);
	}

	[Fact(DisplayName = "DmdataApiTimeoutException_内部例外なし_サービス側エラーと判定される")]
	public void ClassifyError_TimeoutWithoutInnerException_サービス側エラー()
	{
		// Arrange
		var exception = new DmdataApiTimeoutException("タイムアウト");

		// Act
		var result = DmdataErrorClassifier.ClassifyError(exception);

		// Assert
		Assert.Equal(DmdataErrorClassifier.ErrorType.ServiceSide, result);
	}
}
