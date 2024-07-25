using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Voicevox.Model;

public class AudioQuery
{
    [JsonPropertyName("accent_phrases")]
    public AccentPhrases[]? AccentPhrases { get; set; }
    [JsonPropertyName("speedScale")]
    public float SpeedScale { get; set; }
    [JsonPropertyName("pitchScale")]
    public float PitchScale { get; set; }
    [JsonPropertyName("intonationScale")]
    public float IntonationScale { get; set; }
    [JsonPropertyName("volumeScale")]
    public float VolumeScale { get; set; }
    [JsonPropertyName("prePhonemeLength")]
    public float PrePhonemeLength { get; set; }
    [JsonPropertyName("postPhonemeLength")]
    public float PostPhonemeLength { get; set; }
    [JsonPropertyName("outputSamplingRate")]
    public int OutputSamplingRate { get; set; }
    [JsonPropertyName("outputStereo")]
    public bool OutputStereo { get; set; }
    [JsonPropertyName("kana")]
    public string? Kana { get; set; }
}

public class AccentPhrases
{
    [JsonPropertyName("moras")]
    public Mora[]? Moras { get; set; }
    [JsonPropertyName("accent")]
    public int Accent { get; set; }
    [JsonPropertyName("pause_mora")]
    public Mora? PauseMora { get; set; }
    [JsonPropertyName("is_interrogative")]
    public bool IsInterrogative { get; set; }
}

public class Mora
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    [JsonPropertyName("consonant")]
    public string? Consonant { get; set; }
    [JsonPropertyName("consonant_length")]
    public float? ConsonantLength { get; set; }
    [JsonPropertyName("vowel")]
    public string? Vowel { get; set; }
    [JsonPropertyName("vowel_length")]
    public float VowelLength { get; set; }
    [JsonPropertyName("pitch")]
    public float Pitch { get; set; }
}
