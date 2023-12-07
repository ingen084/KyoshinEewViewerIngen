using KyoshinEewViewer.Core.Models;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core;

[JsonSerializable(typeof(KyoshinEewViewerConfiguration))]
[JsonSerializable(typeof(GitHubRelease[]))]
[JsonSerializable(typeof(UpdaterStore))]
public partial class KyoshinEewViewerSerializerContext : JsonSerializerContext
{
}
