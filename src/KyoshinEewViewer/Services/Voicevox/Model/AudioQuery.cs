using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Voicevox.Model;

public class AudioQuery
{
    [JsonPropertyName("speedScale")]
    public float SpeedScale { get; set; }
    [JsonPropertyName("pitchScale")]
    public float PitchScale { get; set; }
    [JsonPropertyName("intonationScale")]
    public float IntonationScale { get; set; }
    [JsonPropertyName("volumeScale")]
    public float VolumeScale { get; set; }
    [JsonPropertyName("pauseLengthScale")]
    public float PauseLengthScale { get; set; }

	[JsonExtensionData]
	public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
