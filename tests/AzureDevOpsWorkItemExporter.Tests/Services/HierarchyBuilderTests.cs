using System.Collections.Generic;
using AzureDevOpsWorkItemExporter.Configuration;
using AzureDevOpsWorkItemExporter.Services;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests.Services;

public class HierarchyBuilderTests
{
    [Fact]
    public void BuildHierarchy_PopulatesChildAndParentRelationships()
    {
        var root = new WorkItemNode(1, new Dictionary<string, object?> { ["System.Title"] = "Root" }) { IsSeed = true };
        var child = new WorkItemNode(2, new Dictionary<string, object?> { ["System.Title"] = "Child" });
        var parent = new WorkItemNode(3, new Dictionary<string, object?> { ["System.Title"] = "Parent" });

        root.Relations.Add(new WorkItemRelation("System.LinkTypes.Hierarchy-Forward", child.Id));
        root.Relations.Add(new WorkItemRelation("System.LinkTypes.Hierarchy-Reverse", parent.Id));

        var export = new ExportDefinition
        {
            Link = "both",
            Depth = new ExportDepth { Child = 1, Parent = 1 }
        };

        var result = HierarchyBuilder.BuildHierarchy(new[] { root, child, parent }, export);

        Assert.Single(result);
        Assert.Same(root, result[0]);
        Assert.Single(root.Children);
        Assert.Equal(child, root.Children[0]);
        Assert.Equal<int?>(1, child.Level);
        Assert.Single(root.Parents);
        Assert.Equal(parent, root.Parents[0]);
        Assert.Equal<int?>(-1, parent.Level);
        Assert.Contains(root, child.Parents);
        Assert.Contains(root, parent.Children);
    }
}
