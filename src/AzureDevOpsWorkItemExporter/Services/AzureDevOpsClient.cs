using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AzureDevOpsWorkItemExporter.Configuration;

namespace AzureDevOpsWorkItemExporter.Services;

public sealed class AzureDevOpsClient : IAzureDevOpsClient
{
    public Task<IReadOnlyList<WorkItemNode>> FetchWorkItemsAsync(ConfigRoot config, string patToken, CancellationToken cancellationToken = default)
    {
        var sampleFields = new Dictionary<string, object?>();

        foreach (var field in config.Select)
        {
            sampleFields[field] = $"sample-{field}";
        }

        var sample = new WorkItemNode(123, sampleFields)
        {
            Relations =
            {
                new WorkItemRelation("System.LinkTypes.Hierarchy-Forward", 456)
            }
        };

        IReadOnlyList<WorkItemNode> nodes = new[] { sample };
        return Task.FromResult(nodes);
    }
}
