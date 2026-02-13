using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AzureDevOpsWorkItemExporter.Configuration;

namespace AzureDevOpsWorkItemExporter.Services;

public sealed class ExportOrchestrator
{
    private readonly IAzureDevOpsClient _client;
    private readonly Templates.FormatExporterService _exporter;
    private readonly string _outputDirectory;

    public ExportOrchestrator(
        IAzureDevOpsClient client,
        Templates.FormatExporterService exporter,
        string? outputDirectory = null)
    {
        _client = client;
        _exporter = exporter;
        _outputDirectory = outputDirectory ?? Path.Combine(AppContext.BaseDirectory, "export-outputs");
    }

    public record ExecutionResult(
        IReadOnlyDictionary<string, string> SavedPaths,
        IReadOnlyCollection<string> FieldNames,
        int WorkItemCount,
        IReadOnlyCollection<string> Formats);

    public async Task<ExecutionResult> ExecuteAsync(ConfigRoot config, string patToken, CancellationToken cancellationToken = default)
    {
        var nodes = await _client.FetchWorkItemsAsync(config, patToken, cancellationToken);
        var hierarchyDefinition = config.Export;
        if (string.Equals(config.Type, "wiql", StringComparison.OrdinalIgnoreCase))
        {
            hierarchyDefinition = new ExportDefinition
            {
                Link = "workitem",
                Depth = new ExportDepth { Parent = 0, Child = 0 },
                Retry = config.Export.Retry,
                Type = config.Export.Type
            };
        }

        var hierarchy = HierarchyBuilder.BuildHierarchy(nodes, hierarchyDefinition);

        var selectedFields = (config.Select ?? new List<string>())
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .ToList();
        var formats = (config.Export.Type ?? new List<string>())
            .Where(format => !string.IsNullOrWhiteSpace(format))
            .Select(format => format.Trim())
            .ToList();
        var totalItems = CountUniqueNodes(hierarchy);
        var exportMeta = new Templates.ExportMeta
        {
            Title = "Azure DevOps Export",
            Summary = $"Exported {totalItems} work item(s)",
            GeneratedAt = DateTime.UtcNow,
            Organization = config.AzureDevOps.Organization,
            Project = config.AzureDevOps.Project,
            QueryType = config.Type,
            Wiql = config.Wiql,
            Wiid = config.Wiid,
            Link = config.Export.Link,
            DepthParent = config.Export.Depth?.Parent,
            DepthChild = config.Export.Depth?.Child,
            Retry = config.Export.Retry ?? 5,
            Formats = formats,
            RunDirectory = _outputDirectory
        };
        var exportContext = new Templates.ExportContext
        {
            WorkItems = hierarchy,
            SelectedFields = selectedFields,
            ExportMeta = exportMeta
        };
        var formatted = _exporter.ExportFormattedOutputs(exportContext);
        Directory.CreateDirectory(_outputDirectory);

        var savedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fieldNames = nodes
            .SelectMany(node => node.Fields.Keys)
            .Append("System.Id")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var (format, artifact) in formatted)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var extension = GetExtensionForFormat(format);
            var safeFormat = string.IsNullOrWhiteSpace(format) ? "export" : format.Trim();
            var fileName = $"{safeFormat}-{timestamp}{extension}";
            var targetPath = Path.Combine(_outputDirectory, fileName);
            if (artifact.Bytes is not null)
            {
                await File.WriteAllBytesAsync(targetPath, artifact.Bytes, cancellationToken);
            }
            else
            {
                await File.WriteAllTextAsync(targetPath, artifact.Text ?? string.Empty, cancellationToken);
            }
            savedPaths[format] = targetPath;
        }

        return new ExecutionResult(savedPaths, fieldNames, totalItems, formats);
    }

    private static int CountUniqueNodes(IReadOnlyList<WorkItemNode> nodes)
    {
        var visited = new HashSet<int>();
        foreach (var node in nodes)
        {
            Walk(node, visited);
        }

        return visited.Count;
    }

    private static void Walk(WorkItemNode node, HashSet<int> visited)
    {
        if (!visited.Add(node.Id))
        {
            return;
        }

        foreach (var child in node.Children)
        {
            Walk(child, visited);
        }

        foreach (var parent in node.Parents)
        {
            Walk(parent, visited);
        }
    }

    private static string GetExtensionForFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return ".txt";
        }

        var normalized = format.Trim().ToLowerInvariant();
        if (FormatExtensions.TryGetValue(normalized, out var mapped))
        {
            return mapped;
        }

        return $".{normalized}";
    }

    private static Dictionary<string, string> FormatExtensions { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["md"] = ".md",
        ["markdown"] = ".md",
        ["html"] = ".html",
        ["json"] = ".json",
        ["csv"] = ".csv",
        ["pdf"] = ".pdf",
        ["word"] = ".docx",
        ["excel"] = ".xlsx"
    };
}
