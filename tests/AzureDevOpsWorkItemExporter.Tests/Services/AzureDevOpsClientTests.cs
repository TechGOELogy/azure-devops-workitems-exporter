using System.Threading.Tasks;
using AzureDevOpsWorkItemExporter.Configuration;
using AzureDevOpsWorkItemExporter.Services;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests.Services;

public class AzureDevOpsClientTests
{
    [Fact]
    public async Task FetchWorkItems_ReturnsSampleNodes()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Select = new System.Collections.Generic.List<string> { "System.Title" }
        };

        var client = new AzureDevOpsClient();
        var nodes = await client.FetchWorkItemsAsync(config, "PAT");

        Assert.Single(nodes);
        Assert.Equal(123, nodes[0].Id);
        Assert.Contains("System.Title", nodes[0].Fields.Keys);
    }
}
