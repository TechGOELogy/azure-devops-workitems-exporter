using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace AzureDevOpsWorkItemExporter.Configuration;

public sealed class ExportDefinition
{
    [YamlMember(Alias = "link")]
    public string? Link { get; init; }

    [YamlMember(Alias = "type")]
    public List<string> Type { get; init; } = new();

    [YamlMember(Alias = "depth")]
    public ExportDepth Depth { get; init; } = new();

    [YamlMember(Alias = "retry")]
    public int? Retry { get; init; }
}
