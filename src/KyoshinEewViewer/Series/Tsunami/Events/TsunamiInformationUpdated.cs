using KyoshinEewViewer.Series.Tsunami.Models;

namespace KyoshinEewViewer.Series.Tsunami.Events;

public record class TsunamiInformationUpdated(TsunamiInfo? Current, TsunamiInfo? New);
