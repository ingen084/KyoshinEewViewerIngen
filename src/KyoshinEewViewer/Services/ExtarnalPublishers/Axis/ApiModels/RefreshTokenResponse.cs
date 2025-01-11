using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;

public class RefreshTokenResponse
{
	[JsonPropertyName("status")]
	public string Status { get; set; }

	[JsonPropertyName("token")]
	public string? Token { get; set; }
}
