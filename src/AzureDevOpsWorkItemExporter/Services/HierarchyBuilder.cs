using System;
using System.Collections.Generic;
using System.Linq;
using AzureDevOpsWorkItemExporter.Configuration;

namespace AzureDevOpsWorkItemExporter.Services;

public sealed class HierarchyBuilder
{
    private static readonly string ChildLink = "System.LinkTypes.Hierarchy-Forward";
    private static readonly string ParentLink = "System.LinkTypes.Hierarchy-Reverse";

    public IReadOnlyList<WorkItemNode> BuildHierarchy(IReadOnlyList<WorkItemNode> nodes, ExportDefinition exportDefinition)
    {
        if (nodes.Count == 0)
        {
            return nodes;
        }

        var nodesById = nodes.ToDictionary(n => n.Id);
        foreach (var node in nodes)
        {
            node.Children.Clear();
            node.Parents.Clear();
            node.Level = null;
        }

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
                    if (!node.Children.Contains(target))
                    {
                        node.Children.Add(target);
                    }

                    if (!target.Parents.Contains(node))
                    {
                        target.Parents.Add(node);
                    }
                }
                else if (IsParentRelation(relation.Type))
                {
                    if (!node.Parents.Contains(target))
                    {
                        node.Parents.Add(target);
                    }

                    if (!target.Children.Contains(node))
                    {
                        target.Children.Add(node);
                    }
                }
            }
        }

        var link = exportDefinition.Link?.Trim().ToLowerInvariant() ?? "workitem";
        var parentDepth = ShouldIncludeParent(link) ? exportDefinition.Depth.Parent ?? 0 : 0;
        var childDepth = ShouldIncludeChild(link) ? exportDefinition.Depth.Child ?? 0 : 0;

        var seedNodes = nodes.Where(n => n.IsSeed).ToList();
        if (seedNodes.Count == 0)
        {
            seedNodes = nodes.ToList();
        }

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

        return seedNodes;
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
