using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AzureDevOpsWorkItemExporter.Configuration;

namespace AzureDevOpsWorkItemExporter.Services;

public interface IAzureDevOpsClient
{
    Task<IReadOnlyList<WorkItemNode>> FetchWorkItemsAsync(ConfigRoot config, string patToken, CancellationToken cancellationToken = default);
}
