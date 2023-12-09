using KyoshinEewViewer.Series.Earthquake.Models;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Earthquake;

[JsonSerializable(typeof(JmaEqdbData))]
internal partial class EarthquakeJsonSerializeContext : JsonSerializerContext
{
}
