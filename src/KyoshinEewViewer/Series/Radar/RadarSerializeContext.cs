using KyoshinEewViewer.Series.Radar.Models;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Radar;

[JsonSerializable(typeof(JmaRadarTime[]))]
[JsonSerializable(typeof(GeoJson))]
internal partial class RadarSerializeContext : JsonSerializerContext;
