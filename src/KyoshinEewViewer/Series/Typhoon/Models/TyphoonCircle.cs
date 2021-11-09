using KyoshinMonitorLib;

namespace KyoshinEewViewer.Series.Typhoon.Models;

public record TyphoonCircle(Location Center, double RangeKilometer, Location RawCenter);
