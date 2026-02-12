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
        }
        else
        {
            if (config.AzureDevOps is null)
            {
                errors.Add("Azure DevOps settings (azure-devops) are required.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(config.AzureDevOps.Organization))
                {
                    errors.Add("azure-devops.organization is required.");
                }
                if (string.IsNullOrWhiteSpace(config.AzureDevOps.Project))
                {
                    errors.Add("azure-devops.project is required.");
                }
            }

            if (string.IsNullOrWhiteSpace(config.Type))
            {
                errors.Add("type must be set to 'wiql' or 'wiid'.");
            }
            else
            {
                var normalizedType = config.Type.Trim().ToLowerInvariant();
                if (normalizedType != "wiql" && normalizedType != "wiid")
                {
                    errors.Add("type must be either 'wiql' or 'wiid'.");
                }
                else if (normalizedType == "wiql" && string.IsNullOrWhiteSpace(config.Wiql))
                {
                    errors.Add("wiql is required when type is 'wiql'.");
                }
                else if (normalizedType == "wiid" && string.IsNullOrWhiteSpace(config.Wiid))
                {
                    errors.Add("wiid is required when type is 'wiid'.");
                }
            }

            if (config.Export is null)
            {
                errors.Add("export section is required.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(config.Export.Link))
                {
                    errors.Add("export.link is required.");
                }
                else
                {
                    var allowedLinks = new[] { "workitem", "child", "parent", "both" };
                    if (!allowedLinks.Contains(config.Export.Link.Trim().ToLowerInvariant()))
                    {
                        errors.Add("export.link must be one of workitem, child, parent, or both.");
                    }
                }

                if (config.Export.Type is null || config.Export.Type.Count == 0)
                {
                    errors.Add("export.type must include at least one format.");
                }
                else if (config.Export.Type.Any(string.IsNullOrWhiteSpace))
                {
                    errors.Add("export.type entries must be non-empty strings.");
                }
                else
                {
                    var requiresSelect = config.Export.Type.Any(format =>
                        format.Equals("csv", StringComparison.OrdinalIgnoreCase) ||
                        format.Equals("json", StringComparison.OrdinalIgnoreCase) ||
                        format.Equals("excel", StringComparison.OrdinalIgnoreCase));

                    if (requiresSelect && (config.Select is null || config.Select.Count == 0 || config.Select.All(string.IsNullOrWhiteSpace)))
                    {
                        errors.Add("select must include at least one field when exporting csv, json, or excel.");
                    }
                }

                if (config.Export.Depth?.Parent is < 0)
                {
                    errors.Add("export.depth.parent must be >= 0.");
                }
                if (config.Export.Depth?.Child is < 0)
                {
                    errors.Add("export.depth.child must be >= 0.");
                }

                if (config.Export.Retry.HasValue && config.Export.Retry < 0)
                {
                    errors.Add("export.retry cannot be negative.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }
    }
}
