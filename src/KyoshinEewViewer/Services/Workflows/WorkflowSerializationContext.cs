using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Workflows;

[JsonSourceGenerationOptions(WriteIndented = true, IgnoreReadOnlyFields = true, IgnoreReadOnlyProperties = true)]
[JsonSerializable(typeof(Workflow[]))]
[JsonSerializable(typeof(WorkflowAction))]
[JsonSerializable(typeof(WorkflowTrigger))]
internal partial class WorkflowSerializationContext : JsonSerializerContext;
