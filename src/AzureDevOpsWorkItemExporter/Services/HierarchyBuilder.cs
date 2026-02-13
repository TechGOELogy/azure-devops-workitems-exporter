using System;
using System.Collections.Generic;
using System.Linq;
using AzureDevOpsWorkItemExporter.Configuration;

namespace AzureDevOpsWorkItemExporter.Services;

public static class HierarchyBuilder
{
    private static readonly string ChildLink = "System.LinkTypes.Hierarchy-Forward";
    private static readonly string ParentLink = "System.LinkTypes.Hierarchy-Reverse";

    public static IReadOnlyList<WorkItemNode> BuildHierarchy(IReadOnlyList<WorkItemNode> nodes, ExportDefinition exportDefinition)
    {
        if (nodes.Count == 0)
        {
            return nodes;
        }

        var nodesById = nodes.ToDictionary(n => n.Id);
        ResetNodes(nodes);
        PopulateRelations(nodes, nodesById);

        var (parentDepth, childDepth) = ResolveDepths(exportDefinition);
        var seedNodes = ResolveSeedNodes(nodes);
        AssignLevels(seedNodes, parentDepth, childDepth);

        return seedNodes;
    }

    private static void ResetNodes(IReadOnlyList<WorkItemNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.Children.Clear();
            node.Parents.Clear();
            node.Level = null;
        }
    }

    private static void PopulateRelations(IEnumerable<WorkItemNode> nodes, IReadOnlyDictionary<int, WorkItemNode> nodesById)
    {
        foreach (var node in nodes)
        {
            foreach (var relation in node.Relations)
            {
                if (!nodesById.TryGetValue(relation.TargetId, out var target))
                {
                    continue;
                }

                if (IsChildRelation(relation.Type))
                {
                    AddRelation(node.Children, target);
                    AddRelation(target.Parents, node);
                }
                else if (IsParentRelation(relation.Type))
                {
                    AddRelation(node.Parents, target);
                    AddRelation(target.Children, node);
                }
            }
        }
    }

    private static void AddRelation(List<WorkItemNode> list, WorkItemNode target)
    {
        if (!list.Contains(target))
        {
            list.Add(target);
        }
    }

    private static (int ParentDepth, int ChildDepth) ResolveDepths(ExportDefinition exportDefinition)
    {
        var link = exportDefinition.Link?.Trim().ToLowerInvariant() ?? "workitem";
        var parentDepth = ShouldIncludeParent(link) ? exportDefinition.Depth.Parent ?? 0 : 0;
        var childDepth = ShouldIncludeChild(link) ? exportDefinition.Depth.Child ?? 0 : 0;
        return (parentDepth, childDepth);
    }

    private static List<WorkItemNode> ResolveSeedNodes(IReadOnlyList<WorkItemNode> nodes)
    {
        var seedNodes = nodes.Where(n => n.IsSeed).ToList();
        return seedNodes.Count == 0 ? nodes.ToList() : seedNodes;
    }

    private static void AssignLevels(IEnumerable<WorkItemNode> seedNodes, int parentDepth, int childDepth)
    {
        foreach (var seed in seedNodes)
        {
            seed.Level = 0;

            if (parentDepth > 0)
            {
                AssignParentLevels(seed, parentDepth, new HashSet<int>());
            }

            if (childDepth > 0)
            {
                AssignChildLevels(seed, childDepth, new HashSet<int>());
            }
        }
    }

    private static void AssignChildLevels(WorkItemNode node, int remainingDepth, HashSet<int> visited)
    {
        if (remainingDepth <= 0)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            var desiredLevel = (node.Level ?? 0) + 1;
            if (child.Level is null || child.Level > desiredLevel)
            {
                child.Level = desiredLevel;
            }

            if (visited.Add(child.Id))
            {
                AssignChildLevels(child, remainingDepth - 1, visited);
            }
        }
    }

    private static void AssignParentLevels(WorkItemNode node, int remainingDepth, HashSet<int> visited)
    {
        if (remainingDepth <= 0)
        {
            return;
        }

        foreach (var parent in node.Parents)
        {
            var desiredLevel = (node.Level ?? 0) - 1;
            if (parent.Level is null || parent.Level > desiredLevel)
            {
                parent.Level = desiredLevel;
            }

            if (visited.Add(parent.Id))
            {
                AssignParentLevels(parent, remainingDepth - 1, visited);
            }
        }
    }

    private static bool IsChildRelation(string relationType) =>
        string.Equals(relationType, ChildLink, StringComparison.OrdinalIgnoreCase);

    private static bool IsParentRelation(string relationType) =>
        string.Equals(relationType, ParentLink, StringComparison.OrdinalIgnoreCase);

    private static bool ShouldIncludeChild(string linkValue) =>
        linkValue == "child" || linkValue == "both";

    private static bool ShouldIncludeParent(string linkValue) =>
        linkValue == "parent" || linkValue == "both";
}
