using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AzureDevOpsWorkItemExporter.Configuration;

public sealed class ConfigLoader
{
    private readonly IDeserializer _deserializer;

    public ConfigLoader()
    {
        _deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public ConfigRoot Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Configuration path cannot be empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Configuration file not found.", path);
        }

        var yaml = File.ReadAllText(path);
        var config = _deserializer.Deserialize<ConfigRoot>(yaml);
        Validate(config);
        return config!;
    }

    private static void Validate(ConfigRoot? config)
    {
        var errors = new List<string>();

        if (config is null)
        {
            errors.Add("Configuration root is missing or malformed.");
            ThrowIfErrors(errors);
            return;
        }

        ValidateAzureDevOps(config.AzureDevOps, errors);
        ValidateType(config, errors);
        ValidateExport(config, errors);

        ThrowIfErrors(errors);
    }

    private static void ValidateAzureDevOps(AzureDevOpsConfig? azureDevOps, List<string> errors)
    {
        if (azureDevOps is null)
        {
            errors.Add("Azure DevOps settings (azure-devops) are required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(azureDevOps.Organization))
        {
            errors.Add("azure-devops.organization is required.");
        }

        if (string.IsNullOrWhiteSpace(azureDevOps.Project))
        {
            errors.Add("azure-devops.project is required.");
        }
    }

    private static void ValidateType(ConfigRoot config, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(config.Type))
        {
            errors.Add("type must be set to 'wiql' or 'wiid'.");
            return;
        }

        var normalizedType = config.Type.Trim().ToLowerInvariant();
        if (normalizedType != "wiql" && normalizedType != "wiid")
        {
            errors.Add("type must be either 'wiql' or 'wiid'.");
            return;
        }

        if (normalizedType == "wiql" && string.IsNullOrWhiteSpace(config.Wiql))
        {
            errors.Add("wiql is required when type is 'wiql'.");
        }
        else if (normalizedType == "wiid" && string.IsNullOrWhiteSpace(config.Wiid))
        {
            errors.Add("wiid is required when type is 'wiid'.");
        }
    }

    private static void ValidateExport(ConfigRoot config, List<string> errors)
    {
        if (config.Export is null)
        {
            errors.Add("export section is required.");
            return;
        }

        ValidateExportLink(config.Export.Link, errors);
        ValidateExportType(config.Export.Type, config.Select, errors);
        ValidateExportDepth(config.Export.Depth, errors);
        ValidateRetry(config.Export.Retry, errors);
    }

    private static void ValidateExportLink(string? link, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            errors.Add("export.link is required.");
            return;
        }

        var allowedLinks = new[] { "workitem", "child", "parent", "both" };
        if (!allowedLinks.Contains(link.Trim().ToLowerInvariant()))
        {
            errors.Add("export.link must be one of workitem, child, parent, or both.");
        }
    }

    private static void ValidateExportType(IReadOnlyList<string>? types, IReadOnlyList<string>? select, List<string> errors)
    {
        if (types is null || types.Count == 0)
        {
            errors.Add("export.type must include at least one format.");
            return;
        }

        if (types.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("export.type entries must be non-empty strings.");
            return;
        }

        var requiresSelect = types.Any(format =>
            format.Equals("csv", StringComparison.OrdinalIgnoreCase) ||
            format.Equals("json", StringComparison.OrdinalIgnoreCase) ||
            format.Equals("excel", StringComparison.OrdinalIgnoreCase));

        if (!requiresSelect)
        {
            return;
        }

        if (select is null || select.Count == 0 || select.All(string.IsNullOrWhiteSpace))
        {
            errors.Add("select must include at least one field when exporting csv, json, or excel.");
        }
    }

    private static void ValidateExportDepth(ExportDepth? depth, List<string> errors)
    {
        if (depth?.Parent is < 0)
        {
            errors.Add("export.depth.parent must be >= 0.");
        }

        if (depth?.Child is < 0)
        {
            errors.Add("export.depth.child must be >= 0.");
        }
    }

    private static void ValidateRetry(int? retry, List<string> errors)
    {
        if (retry.HasValue && retry < 0)
        {
            errors.Add("export.retry cannot be negative.");
        }
    }

    private static void ThrowIfErrors(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }
    }
}
