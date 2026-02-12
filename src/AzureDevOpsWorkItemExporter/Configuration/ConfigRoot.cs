using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace AzureDevOpsWorkItemExporter.Configuration;

public sealed class ConfigRoot
{
    [YamlMember(Alias = "azure-devops")]
    public AzureDevOpsConfig AzureDevOps { get; init; } = new();

    [YamlMember(Alias = "type")]
    public string? Type { get; init; }

    [YamlMember(Alias = "wiql")]
    public string? Wiql { get; init; }

    [YamlMember(Alias = "wiid")]
    public string? Wiid { get; init; }

    [YamlMember(Alias = "select")]
    public List<string> Select { get; init; } = new();

    [YamlMember(Alias = "export")]
    public ExportDefinition Export { get; init; } = new();

    [YamlMember(Alias = "logging")]
    public LoggingSettings Logging { get; init; } = new();

    [YamlMember(Alias = "templates")]
    public Dictionary<string, string> Templates { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
