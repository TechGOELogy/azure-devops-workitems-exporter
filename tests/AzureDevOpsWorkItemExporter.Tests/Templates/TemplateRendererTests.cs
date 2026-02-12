using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AzureDevOpsWorkItemExporter.Services;
using AzureDevOpsWorkItemExporter.Templates;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests.Templates;

public class TemplateRendererTests
{
    [Fact]
    public void Render_DefaultMarkdownTemplate_ReturnsContent()
    {
        var repoRoot = LocateRepoRoot();
        var renderer = new TemplateRenderer(repoRoot);
        var nodes = new List<WorkItemNode>
        {
            new(1, new Dictionary<string, object?> { ["System.Title"] = "Root" })
        };

        var context = new
        {
            work_items = nodes,
            selected_fields = new[] { "System.Title" },
            export_meta = new { title = "Test", summary = "ok", generated_at = DateTime.UtcNow }
        };

        var templatePath = Path.Combine(repoRoot, "src", "AzureDevOpsWorkItemExporter", "template-examples", "markdown-template.scriban");
        var output = renderer.Render(templatePath, context);

        Assert.Contains("## Root", output);
    }

    [Fact]
    public void Render_MissingTemplate_Throws()
    {
        var renderer = new TemplateRenderer(LocateRepoRoot());
        var missingPath = Path.Combine(LocateRepoRoot(), "src", "AzureDevOpsWorkItemExporter", "template-examples", "nonexistent.scriban");
        Assert.Throws<FileNotFoundException>(() => renderer.Render(missingPath, new { }));
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
