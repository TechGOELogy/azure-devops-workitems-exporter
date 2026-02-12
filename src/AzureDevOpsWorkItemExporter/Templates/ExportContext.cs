using System;
using System.Collections.Generic;
using AzureDevOpsWorkItemExporter.Services;

namespace AzureDevOpsWorkItemExporter.Templates;

public sealed record ExportContext
{
    public IReadOnlyList<WorkItemNode> WorkItems { get; init; } = Array.Empty<WorkItemNode>();

    public IReadOnlyList<string> SelectedFields { get; init; } = Array.Empty<string>();

    public ExportMeta ExportMeta { get; init; } = new();
}

public sealed record ExportMeta
{
    public string Title { get; init; } = "Azure DevOps Export";

    public string Summary { get; init; } = string.Empty;

    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    public string? Organization { get; init; }

    public string? Project { get; init; }

    public string? QueryType { get; init; }

    public string? Wiql { get; init; }

    public string? Wiid { get; init; }

    public string? Link { get; init; }

    public int? DepthParent { get; init; }

    public int? DepthChild { get; init; }

    public int Retry { get; init; } = 5;

    public IReadOnlyList<string> Formats { get; init; } = Array.Empty<string>();

    public string? RunDirectory { get; init; }

    public string? AdditionalNotes { get; init; }
}
