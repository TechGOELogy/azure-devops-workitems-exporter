using System.Collections.Generic;

namespace AzureDevOpsWorkItemExporter.Services;

public sealed class WorkItemNode
{
    public WorkItemNode(int id, IDictionary<string, object?> fields)
    {
        Id = id;
        Fields = new Dictionary<string, object?>(fields);
    }

    public int Id { get; }

    public Dictionary<string, object?> Fields { get; }

    public int? Level { get; set; }

    public bool IsSeed { get; set; }

    public List<WorkItemNode> Children { get; } = new();
    public List<WorkItemNode> Parents { get; } = new();

    public List<WorkItemRelation> Relations { get; } = new();
}
