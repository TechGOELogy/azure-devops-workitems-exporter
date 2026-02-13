using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AzureDevOpsWorkItemExporter.Logging;
using AzureDevOpsWorkItemExporter.Services;
using ClosedXML.Excel;

namespace AzureDevOpsWorkItemExporter.Templates;

public sealed class FormatExporterService
{
    private readonly TemplateRenderer _renderer;
    private readonly IReadOnlyDictionary<string, string> _templatePaths;
    private readonly IHtmlToDocxConverter _docxConverter;
    private readonly IHtmlToPdfRenderer _pdfRenderer;

    private static readonly Dictionary<string, string> DefaultTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["md"] = "template-examples/markdown-template.scriban",
        ["markdown"] = "template-examples/markdown-template.scriban",
        ["html"] = "template-examples/html-template.scriban"
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FormatExporterService(
        TemplateRenderer renderer,
        IReadOnlyDictionary<string, string>? templatePaths = null,
        IHtmlToDocxConverter? docxConverter = null,
        IHtmlToPdfRenderer? pdfRenderer = null)
    {
        _renderer = renderer;
        _templatePaths = templatePaths ?? DefaultTemplates;
        _docxConverter = docxConverter ?? new HtmlToDocxConverter();
        _pdfRenderer = pdfRenderer ?? new PuppeteerPdfRenderer();
    }

    public IReadOnlyDictionary<string, ExportArtifact> ExportFormattedOutputs(ExportContext context)
    {
        var results = new Dictionary<string, ExportArtifact>(StringComparer.OrdinalIgnoreCase);
        var selectedFieldList = NormalizeSelectedFields(context.SelectedFields);
        var templateContext = BuildTemplateContext(context, selectedFieldList);

        foreach (var format in context.ExportMeta.Formats)
        {
            var trimmedFormat = format.Trim();
            var normalizedKey = trimmedFormat.ToLowerInvariant();
            var label = string.IsNullOrEmpty(trimmedFormat) ? "UNKNOWN" : trimmedFormat.ToUpperInvariant();
            ConsoleLogger.Log("info", $"{label} export started", new { Format = trimmedFormat });

            var artifact = BuildArtifact(
                normalizedKey,
                format,
                context.WorkItems,
                selectedFieldList,
                templateContext);

            results[format] = artifact;
            ConsoleLogger.Log("info", $"{label} export completed", new { Format = trimmedFormat });
        }

        return results;
    }

    private static List<string> NormalizeSelectedFields(IEnumerable<string> selectedFields)
    {
        return selectedFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .ToList();
    }

    private static object BuildTemplateContext(ExportContext context, List<string> selectedFields)
    {
        return new
        {
            work_items = context.WorkItems,
            selected_fields = selectedFields,
            export_meta = context.ExportMeta
        };
    }

    private ExportArtifact BuildArtifact(
        string normalizedKey,
        string format,
        IReadOnlyList<WorkItemNode> nodes,
        IReadOnlyList<string> selectedFields,
        object templateContext)
    {
        return normalizedKey switch
        {
            "csv" => ExportArtifact.FromText(BuildCsv(nodes, selectedFields)),
            "json" => ExportArtifact.FromText(BuildJson(nodes, selectedFields)),
            "excel" => ExportArtifact.FromBytes(BuildExcel(nodes, selectedFields)),
            _ => BuildTemplateArtifact(normalizedKey, format, templateContext)
        };
    }

    private ExportArtifact BuildTemplateArtifact(string normalizedKey, string format, object templateContext)
    {
        var templatePath = ResolveTemplatePath(normalizedKey);
        if (templatePath is null)
        {
            return ExportArtifact.FromText($"[stub] {format} export will be implemented later.");
        }

        var rendered = _renderer.Render(templatePath, templateContext);
        return normalizedKey switch
        {
            "word" => ExportArtifact.FromBytes(_docxConverter.Convert(rendered)),
            "pdf" => ExportArtifact.FromBytes(_pdfRenderer.Render(rendered)),
            _ => ExportArtifact.FromText(rendered)
        };
    }

    private static string BuildCsv(IReadOnlyList<WorkItemNode> nodes, IReadOnlyList<string> selectedFields)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", selectedFields.Select(EscapeCsv)));

        foreach (var node in FlattenNodes(nodes))
        {
            var row = selectedFields.Select(field =>
                node.Fields.TryGetValue(field, out var value) ? EscapeCsv(ValueToString(value)) : string.Empty);
            builder.AppendLine(string.Join(",", row));
        }

        return builder.ToString();
    }

    private static string BuildJson(IReadOnlyList<WorkItemNode> nodes, IReadOnlyList<string> selectedFields)
    {
        var rows = FlattenNodes(nodes)
            .Select(node =>
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in selectedFields)
                {
                    row[field] = node.Fields.TryGetValue(field, out var value) ? value : null;
                }

                return row;
            })
            .ToList();

        return JsonSerializer.Serialize(rows, JsonOptions);
    }

    private static byte[] BuildExcel(IReadOnlyList<WorkItemNode> nodes, IReadOnlyList<string> selectedFields)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("WorkItems");

        for (var col = 0; col < selectedFields.Count; col++)
        {
            worksheet.Cell(1, col + 1).Value = selectedFields[col];
        }

        var rowIndex = 2;
        foreach (var node in FlattenNodes(nodes))
        {
            for (var col = 0; col < selectedFields.Count; col++)
            {
                var field = selectedFields[col];
                var value = node.Fields.TryGetValue(field, out var raw) ? ValueToString(raw) : string.Empty;
                worksheet.Cell(rowIndex, col + 1).Value = value;
            }
            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static IEnumerable<WorkItemNode> FlattenNodes(IReadOnlyList<WorkItemNode> nodes)
    {
        var visited = new HashSet<int>();
        var ordered = nodes
            .OrderBy(node => node.Level ?? 0)
            .ThenBy(node => node.Id)
            .ToList();

        foreach (var node in ordered)
        {
            if (!visited.Add(node.Id))
            {
                continue;
            }

            yield return node;

            foreach (var child in Walk(node.Children, visited))
            {
                yield return child;
            }

            foreach (var parent in Walk(node.Parents, visited))
            {
                yield return parent;
            }
        }
    }

    private static IEnumerable<WorkItemNode> Walk(IEnumerable<WorkItemNode> nodes, HashSet<int> visited)
    {
        foreach (var node in nodes)
        {
            if (!visited.Add(node.Id))
            {
                continue;
            }

            yield return node;

            foreach (var child in Walk(node.Children, visited))
            {
                yield return child;
            }
        }
    }

    private static string ValueToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private string? ResolveTemplatePath(string formatKey)
    {
        if (_templatePaths.TryGetValue(formatKey, out var configured) && !string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return DefaultTemplates.TryGetValue(formatKey, out var fallback) ? fallback : null;
    }
}
