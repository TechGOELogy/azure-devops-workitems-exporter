using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AzureDevOpsWorkItemExporter.Configuration;
using AzureDevOpsWorkItemExporter.Services;
using AzureDevOpsWorkItemExporter.Templates;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests.Services;

public class ExportOrchestratorTests
{
    private static readonly string[] DefaultFormats = { "md" };
    private static readonly string[] HtmlFormats = { "html" };

    private static ConfigRoot CreateSampleConfig(IEnumerable<string>? types = null)
    {
        return new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Select = new List<string> { "System.Title" },
            Export = new ExportDefinition
            {
                Link = "workitem",
                Type = (types ?? DefaultFormats).ToList(),
                Retry = 1
            }
        };
    }

    [Fact]
    public async Task Execute_WritesFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var orchestrator = CreateOrchestrator(tempDir);

        var result = await orchestrator.ExecuteAsync(CreateSampleConfig(), "PAT", CancellationToken.None);

        Assert.Single(result.SavedPaths);
        foreach (var path in result.SavedPaths.Values)
        {
            Assert.True(File.Exists(path));
        }
        Assert.Contains("System.Title", result.FieldNames);
    }

    [Fact]
    public async Task Execute_UsesFormatSpecificExtensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var orchestrator = CreateOrchestrator(tempDir);
        var config = CreateSampleConfig(HtmlFormats);

        var result = await orchestrator.ExecuteAsync(config, "PAT", CancellationToken.None);

        Assert.True(result.SavedPaths.TryGetValue("html", out var htmlPath));
        Assert.EndsWith(".html", htmlPath, StringComparison.OrdinalIgnoreCase);
    }

    private static ExportOrchestrator CreateOrchestrator(string? outputDir = null)
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
        return new ExportOrchestrator(new AzureDevOpsClient(), exporter, outputDir);
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
