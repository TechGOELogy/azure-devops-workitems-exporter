using YamlDotNet.Serialization;

namespace AzureDevOpsWorkItemExporter.Configuration;

public sealed class LoggingSettings
{
    [YamlMember(Alias = "verbosity")]
    public string? Verbosity { get; init; }

    [YamlMember(Alias = "location")]
    public string? Location { get; init; }
}
