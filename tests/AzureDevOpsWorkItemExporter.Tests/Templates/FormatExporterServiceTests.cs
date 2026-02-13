using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AzureDevOpsWorkItemExporter.Services;
using AzureDevOpsWorkItemExporter.Templates;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests.Templates;

public class FormatExporterServiceTests
{
    [Fact]
    public void ExportFormattedOutputs_ReturnsMarkdownAndStub()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);
        var templateBase = Path.Combine(repoRoot, "src", "AzureDevOpsWorkItemExporter", "template-examples");
        var templatePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["md"] = Path.Combine(templateBase, "markdown-template.scriban"),
            ["html"] = Path.Combine(templateBase, "html-template.scriban")
        };
        var exporter = new FormatExporterService(renderer, templatePaths);

        var nodes = new List<WorkItemNode>
        {
            new(1, new Dictionary<string, object?> { ["System.Title"] = "Root" })
        };

        var context = new ExportContext
        {
            WorkItems = nodes,
            SelectedFields = new[] { "System.Title" },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "md", "csv" },
                Summary = "Test"
            }
        };
        var output = exporter.ExportFormattedOutputs(context);

        Assert.Contains("md", output.Keys);
        Assert.Contains("csv", output.Keys);
        Assert.Contains("System.Title", output["csv"].Text ?? string.Empty);
        Assert.Contains("Root", output["csv"].Text ?? string.Empty);
        Assert.Contains("## Root", output["md"].Text ?? string.Empty);
    }

    [Fact]
    public void ExportFormattedOutputs_ReturnsJsonRows()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);
        var exporter = new FormatExporterService(renderer);

        var nodes = new List<WorkItemNode>
        {
            new(1, new Dictionary<string, object?> { ["System.Title"] = "Root" })
        };

        var context = new ExportContext
        {
            WorkItems = nodes,
            SelectedFields = new[] { "System.Title" },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "json" },
                Summary = "Test"
            }
        };
        var output = exporter.ExportFormattedOutputs(context);

        Assert.Contains("\"System.Title\"", output["json"].Text ?? string.Empty);
        Assert.Contains("\"Root\"", output["json"].Text ?? string.Empty);
    }

    [Fact]
    public void ExportFormattedOutputs_ReturnsExcelBinary()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);
        var exporter = new FormatExporterService(renderer);

        var nodes = new List<WorkItemNode>
        {
            new(1, new Dictionary<string, object?> { ["System.Title"] = "Root" })
        };

        var context = new ExportContext
        {
            WorkItems = nodes,
            SelectedFields = new[] { "System.Title" },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "excel" }
            }
        };

        var output = exporter.ExportFormattedOutputs(context);

        Assert.NotNull(output["excel"].Bytes);
        Assert.NotEmpty(output["excel"].Bytes!);
    }

    [Fact]
    public void ExportFormattedOutputs_ReturnsWordAndPdfBinary()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);
        var templateBase = Path.Combine(repoRoot, "src", "AzureDevOpsWorkItemExporter", "template-examples");
        var templatePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["word"] = Path.Combine(templateBase, "markdown-template.scriban"),
            ["pdf"] = Path.Combine(templateBase, "markdown-template.scriban")
        };
        var exporter = new FormatExporterService(renderer, templatePaths);

        var nodes = new List<WorkItemNode>
        {
            new(1, new Dictionary<string, object?> { ["System.Title"] = "Root" })
        };

        var context = new ExportContext
        {
            WorkItems = nodes,
            SelectedFields = new[] { "System.Title" },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "word", "pdf" }
            }
        };

        var output = exporter.ExportFormattedOutputs(context);

        Assert.NotNull(output["word"].Bytes);
        Assert.NotEmpty(output["word"].Bytes!);
        Assert.NotNull(output["pdf"].Bytes);
        Assert.NotEmpty(output["pdf"].Bytes!);
    }

    [Fact]
    public void ExportFormattedOutputs_UsesStubForUnknownTemplateFormat()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);
        var exporter = new FormatExporterService(renderer);

        var nodes = new List<WorkItemNode>
        {
            new(1, new Dictionary<string, object?> { ["System.Title"] = "Root" })
        };

        var context = new ExportContext
        {
            WorkItems = nodes,
            SelectedFields = new[] { "System.Title" },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "rtf" }
            }
        };

        var output = exporter.ExportFormattedOutputs(context);

        Assert.Contains("stub", output["rtf"].Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportFormattedOutputs_EscapesCsvFields()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);
        var exporter = new FormatExporterService(renderer);

        var nodes = new List<WorkItemNode>
        {
            new(1, new Dictionary<string, object?> { ["System.Title"] = "Hello, \"World\"" })
        };

        var context = new ExportContext
        {
            WorkItems = nodes,
            SelectedFields = new[] { " System.Title " },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "csv" }
            }
        };

        var output = exporter.ExportFormattedOutputs(context);
        var csv = output["csv"].Text ?? string.Empty;

        Assert.Contains("\"Hello, \"\"World\"\"\"", csv);
        Assert.StartsWith("System.Title", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportFormattedOutputs_FlattensHierarchyForCsv()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);
        var exporter = new FormatExporterService(renderer);

        var root = new WorkItemNode(1, new Dictionary<string, object?> { ["System.Title"] = "Root" });
        var child = new WorkItemNode(2, new Dictionary<string, object?> { ["System.Title"] = "Child" });
        var parent = new WorkItemNode(3, new Dictionary<string, object?> { ["System.Title"] = "Parent" });
        root.Children.Add(child);
        root.Parents.Add(parent);

        var context = new ExportContext
        {
            WorkItems = new List<WorkItemNode> { root },
            SelectedFields = new[] { "System.Title" },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "csv" }
            }
        };

        var output = exporter.ExportFormattedOutputs(context);
        var csv = output["csv"].Text ?? string.Empty;

        var lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
    }

    [Fact]
    public void ExportFormattedOutputs_SkipsDuplicateNodes()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);
        var exporter = new FormatExporterService(renderer);

        var root = new WorkItemNode(1, new Dictionary<string, object?> { ["System.Title"] = "Root" });
        var child = new WorkItemNode(2, new Dictionary<string, object?> { ["System.Title"] = "Child" });
        root.Children.Add(child);
        root.Children.Add(child);

        var context = new ExportContext
        {
            WorkItems = new List<WorkItemNode> { root, root },
            SelectedFields = new[] { "System.Title" },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "csv" }
            }
        };

        var output = exporter.ExportFormattedOutputs(context);
        var csv = output["csv"].Text ?? string.Empty;

        var lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void ExportFormattedOutputs_EmptyTemplate_WordStillRenders()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);

        var tempTemplate = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.scriban");
        File.WriteAllText(tempTemplate, string.Empty);

        var templatePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["word"] = tempTemplate
        };
        var exporter = new FormatExporterService(renderer, templatePaths);

        var nodes = new List<WorkItemNode>
        {
            new(1, new Dictionary<string, object?> { ["System.Title"] = "Root" })
        };

        var context = new ExportContext
        {
            WorkItems = nodes,
            SelectedFields = new[] { "System.Title" },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "word" }
            }
        };

        var output = exporter.ExportFormattedOutputs(context);

        Assert.NotNull(output["word"].Bytes);
        Assert.NotEmpty(output["word"].Bytes!);
    }

    [Fact]
    public void ExportFormattedOutputs_PdfEscapesNonAsciiAndParens()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);

        var tempTemplate = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.scriban");
        File.WriteAllText(tempTemplate, "Résumé (Sample)");

        var templatePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pdf"] = tempTemplate
        };
        var exporter = new FormatExporterService(renderer, templatePaths);

        var nodes = new List<WorkItemNode>
        {
            new(1, new Dictionary<string, object?> { ["System.Title"] = "Root" })
        };

        var context = new ExportContext
        {
            WorkItems = nodes,
            SelectedFields = new[] { "System.Title" },
            ExportMeta = new ExportMeta
            {
                Formats = new[] { "pdf" }
            }
        };

        var output = exporter.ExportFormattedOutputs(context);
        var bytes = output["pdf"].Bytes ?? Array.Empty<byte>();
        var ascii = Encoding.ASCII.GetString(bytes);

        Assert.Contains("\\(", ascii);
        Assert.Contains("?", ascii);
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.GetFiles("*.sln*").Length == 0)
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
