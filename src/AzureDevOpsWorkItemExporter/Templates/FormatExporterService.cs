using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AzureDevOpsWorkItemExporter.Logging;
using AzureDevOpsWorkItemExporter.Services;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AzureDevOpsWorkItemExporter.Templates;

public sealed class FormatExporterService
{
    private readonly TemplateRenderer _renderer;
    private readonly IReadOnlyDictionary<string, string> _templatePaths;

    private static IReadOnlyDictionary<string, string> DefaultTemplates { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["md"] = "template-examples/markdown-template.scriban",
            ["markdown"] = "template-examples/markdown-template.scriban",
            ["html"] = "template-examples/html-template.scriban"
        };

    public FormatExporterService(TemplateRenderer renderer, IReadOnlyDictionary<string, string>? templatePaths = null)
    {
        _renderer = renderer;
        _templatePaths = templatePaths ?? DefaultTemplates;
    }

    public IReadOnlyDictionary<string, ExportArtifact> ExportFormattedOutputs(ExportContext context)
    {
        var results = new Dictionary<string, ExportArtifact>(StringComparer.OrdinalIgnoreCase);
        var selectedFieldList = context.SelectedFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .ToList();
        var templateContext = new
        {
            work_items = context.WorkItems,
            selected_fields = selectedFieldList,
            export_meta = context.ExportMeta
        };

        foreach (var format in context.ExportMeta.Formats)
        {
            var trimmedFormat = format.Trim();
            var normalizedKey = trimmedFormat.ToLowerInvariant();
            var label = string.IsNullOrEmpty(trimmedFormat) ? "UNKNOWN" : trimmedFormat.ToUpperInvariant();
            ConsoleLogger.Log("info", $"{label} export started", new { Format = trimmedFormat });

            ExportArtifact artifact;
            if (normalizedKey == "csv")
            {
                artifact = ExportArtifact.FromText(BuildCsv(context.WorkItems, selectedFieldList));
            }
            else if (normalizedKey == "json")
            {
                artifact = ExportArtifact.FromText(BuildJson(context.WorkItems, selectedFieldList));
            }
            else if (normalizedKey == "excel")
            {
                artifact = ExportArtifact.FromBytes(BuildExcel(context.WorkItems, selectedFieldList));
            }
            else
            {
                var templatePath = ResolveTemplatePath(normalizedKey);
                if (templatePath != null)
                {
                    var rendered = _renderer.Render(templatePath, templateContext);
                    if (normalizedKey == "word")
                    {
                        artifact = ExportArtifact.FromBytes(BuildWord(rendered));
                    }
                    else if (normalizedKey == "pdf")
                    {
                        artifact = ExportArtifact.FromBytes(BuildPdf(rendered));
                    }
                    else
                    {
                        artifact = ExportArtifact.FromText(rendered);
                    }
                }
                else
                {
                    artifact = ExportArtifact.FromText($"[stub] {format} export will be implemented later.");
                }
            }

            results[format] = artifact;
            ConsoleLogger.Log("info", $"{label} export completed", new { Format = trimmedFormat });
        }

        return results;
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

        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
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

    private static byte[] BuildWord(string content)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            foreach (var line in NormalizeLines(content))
            {
                body.AppendChild(new Paragraph(new Run(new Text(line))));
            }

            mainPart.Document.Save();
        }
        return stream.ToArray();
    }

    private static byte[] BuildPdf(string content)
    {
        using var stream = new MemoryStream();
        var lines = NormalizeLines(content).ToList();
        var maxLines = 45;
        var visibleLines = lines.Take(maxLines).ToList();

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 12 Tf");
        contentBuilder.AppendLine("14 TL");
        contentBuilder.AppendLine("40 760 Td");
        foreach (var line in visibleLines)
        {
            contentBuilder.Append('(');
            contentBuilder.Append(EscapePdf(line));
            contentBuilder.AppendLine(") Tj");
            contentBuilder.AppendLine("T*");
        }
        contentBuilder.AppendLine("ET");

        var contentBytes = Encoding.ASCII.GetBytes(contentBuilder.ToString());

        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.WriteLine("%PDF-1.4");
        writer.Flush();

        var offsets = new List<long> { 0 };

        WriteObject(writer, stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>", null);
        WriteObject(writer, stream, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", null);
        WriteObject(writer, stream, offsets, 3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>", null);
        WriteObject(writer, stream, offsets, 4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", null);
        WriteObject(writer, stream, offsets, 5, $"<< /Length {contentBytes.Length} >>", contentBytes);

        var xrefStart = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {offsets.Count}");
        writer.WriteLine("0000000000 65535 f ");
        for (var i = 1; i < offsets.Count; i++)
        {
            writer.WriteLine($"{offsets[i]:0000000000} 00000 n ");
        }

        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {offsets.Count} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefStart);
        writer.WriteLine("%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static IEnumerable<string> NormalizeLines(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            yield return string.Empty;
            yield break;
        }

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            yield return line;
        }
    }

    private static void WriteObject(StreamWriter writer, Stream stream, List<long> offsets, int id, string body, byte[]? streamBytes)
    {
        offsets.Add(stream.Position);
        writer.WriteLine($"{id} 0 obj");
        writer.WriteLine(body);
        if (streamBytes is not null)
        {
            writer.WriteLine("stream");
            writer.Flush();
            stream.Write(streamBytes, 0, streamBytes.Length);
            writer.WriteLine();
            writer.WriteLine("endstream");
        }
        writer.WriteLine("endobj");
        writer.Flush();
    }

    private static string EscapePdf(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch > 127)
            {
                sanitized.Append('?');
                continue;
            }

            sanitized.Append(ch switch
            {
                '(' => "\\(",
                ')' => "\\)",
                '\\' => "\\\\",
                _ => ch.ToString()
            });
        }

        return sanitized.ToString();
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
