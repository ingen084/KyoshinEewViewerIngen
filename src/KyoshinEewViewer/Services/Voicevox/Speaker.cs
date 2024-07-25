namespace KyoshinEewViewer.Services.Voicevox;

public abstract record Speaker(string Name);
public record SingleStyleSpeaker(string Name, int SpeakerId) : Speaker(Name);
public record MultiStyleSpeaker(string Name, SingleStyleSpeaker[] Styles) : Speaker(Name);
