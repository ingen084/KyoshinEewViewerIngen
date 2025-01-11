using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Voicevox.Model;
public class Speaker
{
	[JsonPropertyName("name")]
	public string? Name { get; set; }
	[JsonPropertyName("styles")]
	public SpeakerStyle[]? Styles { get; set; }

	[JsonExtensionData]
	public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
