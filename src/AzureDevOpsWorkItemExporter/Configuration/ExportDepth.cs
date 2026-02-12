using YamlDotNet.Serialization;

namespace AzureDevOpsWorkItemExporter.Configuration;

public sealed class ExportDepth
{
    [YamlMember(Alias = "parent")]
    public int? Parent { get; init; }

    [YamlMember(Alias = "child")]
    public int? Child { get; init; }
}
