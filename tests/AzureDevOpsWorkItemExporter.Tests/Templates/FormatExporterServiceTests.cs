using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.sln*").Any())
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
