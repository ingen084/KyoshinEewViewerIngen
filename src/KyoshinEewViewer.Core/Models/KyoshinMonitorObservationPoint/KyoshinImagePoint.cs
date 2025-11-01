using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint.Formatter;
using KyoshinMonitorLib;
using MessagePack;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;


[MessagePackFormatter(typeof(KyoshinImagePointFormatter))]
public record class KyoshinImagePoint(
	[property: JsonPropertyName("center_point")] Point2 Center,
	[property: JsonPropertyName("offset")] Point2 Offset);
