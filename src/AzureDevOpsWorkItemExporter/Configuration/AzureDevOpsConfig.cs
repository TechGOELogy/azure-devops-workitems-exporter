using YamlDotNet.Serialization;

namespace AzureDevOpsWorkItemExporter.Configuration;

public sealed class AzureDevOpsConfig
{
    [YamlMember(Alias = "organization")]
    public string? Organization { get; init; }

    [YamlMember(Alias = "project")]
    public string? Project { get; init; }
}
