using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

[JsonSerializable(typeof(KyoshinMonitorLib.ApiResult.WebApi.Eew))]
public partial class KyoshinMonitorJsonSerializeContext : JsonSerializerContext
{
}
