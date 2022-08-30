using KyoshinMonitorLib;

namespace KyoshinEewViewer.Series.Typhoon.Models;

// 予報を含めた台風
public record TyphoonItem(string Id, TyphoonPlace CurrentPlace, TyphoonPlace[] Places);
// 台風の位置
public record TyphoonPlace(Location Center, TyphoonRenderCircle? Strong, TyphoonRenderCircle? Storm);
// 台風の円
public record TyphoonRenderCircle(Location Center, double RangeKilometer, Location RawCenter);
