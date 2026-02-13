using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AzureDevOpsWorkItemExporter.Configuration;
using AzureDevOpsWorkItemExporter.Services;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests.Services;

public class AzureDevOpsHttpClientTests
{
    [Fact]
    public async Task FetchWorkItemsAsync_ParsesResponses()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "wiql",
            Wiql = "SELECT [System.Id] FROM WorkItems",
            Select = new List<string> { "System.Title" },
            Export = new ExportDefinition { Link = "workitem", Type = new List<string> { "html" }, Retry = 1 }
        };

        var wiqlJson = JsonSerializer.Serialize(new
        {
            workItems = new[]
            {
                new { id = 12 }
            }
        });

        var workItemsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 12,
                    fields = new Dictionary<string, string> { ["System.Title"] = "Sample" },
                    relations = new[]
                    {
                        new { rel = "System.LinkTypes.Hierarchy-Forward", url = "https://dev.azure.com/org/_apis/wit/workItems/34" }
                    }
                }
            }
        });

        var handler = new SequenceHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(wiqlJson, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(workItemsJson, Encoding.UTF8, "application/json") });

        var relationsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 12,
                    relations = new[]
                    {
                        new { rel = "System.LinkTypes.Hierarchy-Forward", url = "https://dev.azure.com/org/_apis/wit/workItems/34" }
                    }
                }
            }
        });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(relationsJson, Encoding.UTF8, "application/json") });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com/") };
        var client = new AzureDevOpsHttpClient(httpClient);
        var nodes = await client.FetchWorkItemsAsync(config, "PAT", CancellationToken.None);

        Assert.Single(nodes);
        Assert.Equal(12, nodes[0].Id);
        Assert.Equal("Sample", nodes[0].Fields["System.Title"]);
        Assert.Single(nodes[0].Relations);
        Assert.Equal("System.LinkTypes.Hierarchy-Forward", nodes[0].Relations[0].Type);
        Assert.Equal(34, nodes[0].Relations[0].TargetId);

        Assert.Equal("Basic", handler.LastAuthScheme);

        var workItemRequests = handler.RequestUris
            .Where(uri => uri.Contains("_apis/wit/workitems", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(workItemRequests);
        Assert.All(workItemRequests, uri => Assert.DoesNotContain("fields=", uri, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FetchWorkItemsAsync_RetriesTransientFailures()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "wiid",
            Wiid = "12",
            Export = new ExportDefinition { Link = "workitem", Type = new List<string> { "html" }, Retry = 1 }
        };

        var workItemsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 12,
                    fields = new Dictionary<string, string> { ["System.Title"] = "Sample" }
                }
            }
        });

        var relationsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 12,
                    relations = Array.Empty<object>()
                }
            }
        });

        var handler = new SequenceHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("try again", Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(workItemsJson, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(relationsJson, Encoding.UTF8, "application/json") });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com/") };
        var client = new AzureDevOpsHttpClient(httpClient);
        var nodes = await client.FetchWorkItemsAsync(config, "PAT", CancellationToken.None);

        Assert.Single(nodes);
        Assert.Contains(handler.RequestUris, uri => uri.Contains("_apis/wit/workitems", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, handler.RequestUris.Count);
    }

    [Fact]
    public async Task FetchWorkItemsAsync_TraversesChildDepth()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "wiid",
            Wiid = "1",
            Export = new ExportDefinition
            {
                Link = "child",
                Type = new List<string> { "html" },
                Depth = new ExportDepth { Child = 2 },
                Retry = 0
            }
        };

        var rootDetails = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { id = 1, fields = new Dictionary<string, string> { ["System.Title"] = "Root" } }
            }
        });
        var rootRelations = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 1,
                    relations = new[]
                    {
                        new { rel = "System.LinkTypes.Hierarchy-Forward", url = "https://dev.azure.com/org/_apis/wit/workItems/2" }
                    }
                }
            }
        });

        var childDetails = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { id = 2, fields = new Dictionary<string, string> { ["System.Title"] = "Child" } }
            }
        });
        var childRelations = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 2,
                    relations = new[]
                    {
                        new { rel = "System.LinkTypes.Hierarchy-Forward", url = "https://dev.azure.com/org/_apis/wit/workItems/3" }
                    }
                }
            }
        });

        var grandChildDetails = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { id = 3, fields = new Dictionary<string, string> { ["System.Title"] = "Grandchild" } }
            }
        });
        var grandChildRelations = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { id = 3, relations = Array.Empty<object>() }
            }
        });

        var handler = new SequenceHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(rootDetails, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(rootRelations, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(childDetails, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(childRelations, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(grandChildDetails, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(grandChildRelations, Encoding.UTF8, "application/json") });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com/") };
        var client = new AzureDevOpsHttpClient(httpClient);
        var nodes = await client.FetchWorkItemsAsync(config, "PAT", CancellationToken.None);

        Assert.Equal(3, nodes.Count);
        Assert.Contains(nodes, node => node.Id == 3);
    }

    [Fact]
    public async Task FetchWorkItemsAsync_WiqlIgnoresLinkAndDepth()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "wiql",
            Wiql = "SELECT [System.Id] FROM WorkItems",
            Export = new ExportDefinition
            {
                Link = "child",
                Type = new List<string> { "html" },
                Depth = new ExportDepth { Child = 2, Parent = 2 },
                Retry = 0
            }
        };

        var wiqlJson = JsonSerializer.Serialize(new
        {
            workItems = new[]
            {
                new { id = 1 }
            }
        });

        var workItemsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 1,
                    fields = new Dictionary<string, string> { ["System.Title"] = "Root" }
                }
            }
        });

        var relationsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 1,
                    relations = new[]
                    {
                        new { rel = "System.LinkTypes.Hierarchy-Forward", url = "https://dev.azure.com/org/_apis/wit/workItems/2" }
                    }
                }
            }
        });

        var handler = new SequenceHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(wiqlJson, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(workItemsJson, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(relationsJson, Encoding.UTF8, "application/json") });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com/") };
        var client = new AzureDevOpsHttpClient(httpClient);
        var nodes = await client.FetchWorkItemsAsync(config, "PAT", CancellationToken.None);

        Assert.Single(nodes);
        Assert.Equal(1, nodes[0].Id);
        Assert.Equal(3, handler.RequestUris.Count);
    }

    [Fact]
    public async Task FetchWorkItemsAsync_ThrowsWhenPatMissing()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "wiid",
            Wiid = "1",
            Export = new ExportDefinition { Link = "workitem", Type = new List<string> { "html" }, Retry = 0 }
        };

        var client = new AzureDevOpsHttpClient(new HttpClient(new SequenceHandler()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchWorkItemsAsync(config, string.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task FetchWorkItemsAsync_InvalidType_Throws()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "unknown",
            Export = new ExportDefinition { Link = "workitem", Type = new List<string> { "html" }, Retry = 0 }
        };

        var client = new AzureDevOpsHttpClient(new HttpClient(new SequenceHandler()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchWorkItemsAsync(config, "PAT", CancellationToken.None));
    }

    [Fact]
    public async Task FetchWorkItemsAsync_WiqlFailure_ThrowsHttpRequestException()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "wiql",
            Wiql = "SELECT [System.Id] FROM WorkItems",
            Export = new ExportDefinition { Link = "workitem", Type = new List<string> { "html" }, Retry = 0 }
        };

        var handler = new SequenceHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad query", Encoding.UTF8, "application/json") });

        var client = new AzureDevOpsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com/") });
        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchWorkItemsAsync(config, "PAT", CancellationToken.None));
    }

    [Fact]
    public async Task FetchWorkItemsAsync_InvalidRelationUrl_IgnoresRelation()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "wiid",
            Wiid = "1",
            Export = new ExportDefinition { Link = "workitem", Type = new List<string> { "html" }, Retry = 0 }
        };

        var workItemsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 1,
                    fields = new Dictionary<string, object> { ["System.Title"] = "Root" }
                }
            }
        });

        var relationsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 1,
                    relations = new[]
                    {
                        new { rel = "System.LinkTypes.Hierarchy-Forward", url = "not-a-url" }
                    }
                }
            }
        });

        var handler = new SequenceHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(workItemsJson, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(relationsJson, Encoding.UTF8, "application/json") });

        var client = new AzureDevOpsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com/") });
        var nodes = await client.FetchWorkItemsAsync(config, "PAT", CancellationToken.None);

        Assert.Single(nodes);
        Assert.Empty(nodes[0].Relations);
    }

    [Fact]
    public async Task FetchWorkItemsAsync_ConvertsFieldTypes()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "wiid",
            Wiid = "1",
            Export = new ExportDefinition { Link = "workitem", Type = new List<string> { "html" }, Retry = 0 }
        };

        var workItemsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 1,
                    fields = new Dictionary<string, object?>
                    {
                        ["System.Title"] = "Root",
                        ["System.Count"] = 42,
                        ["System.Done"] = true,
                        ["System.Array"] = new[] { "a", "b" },
                        ["System.Object"] = new { nested = "value" }
                    }
                }
            }
        });

        var relationsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { id = 1, relations = Array.Empty<object>() }
            }
        });

        var handler = new SequenceHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(workItemsJson, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(relationsJson, Encoding.UTF8, "application/json") });

        var client = new AzureDevOpsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com/") });
        var nodes = await client.FetchWorkItemsAsync(config, "PAT", CancellationToken.None);

        Assert.Single(nodes);
        Assert.Equal("Root", nodes[0].Fields["System.Title"]);
        Assert.True(nodes[0].Fields["System.Count"] is long or double);
        Assert.IsType<bool>(nodes[0].Fields["System.Done"]);
        Assert.IsType<List<object?>>(nodes[0].Fields["System.Array"]);
        Assert.IsType<Dictionary<string, object?>>(nodes[0].Fields["System.Object"]);
    }

    [Fact]
    public async Task FetchWorkItemsAsync_RetriesOnTransientException()
    {
        var config = new ConfigRoot
        {
            AzureDevOps = new AzureDevOpsConfig
            {
                Organization = "org",
                Project = "proj"
            },
            Type = "wiid",
            Wiid = "1",
            Export = new ExportDefinition { Link = "workitem", Type = new List<string> { "html" }, Retry = 1 }
        };

        var workItemsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new
                {
                    id = 1,
                    fields = new Dictionary<string, string> { ["System.Title"] = "Root" }
                }
            }
        });

        var relationsJson = JsonSerializer.Serialize(new
        {
            value = new[]
            {
                new { id = 1, relations = Array.Empty<object>() }
            }
        });

        var handler = new ThrowingSequenceHandler();
        handler.EnqueueException(new HttpRequestException("transient"));
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(workItemsJson, Encoding.UTF8, "application/json") });
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(relationsJson, Encoding.UTF8, "application/json") });

        var client = new AzureDevOpsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("https://dev.azure.com/") });
        var nodes = await client.FetchWorkItemsAsync(config, "PAT", CancellationToken.None);

        Assert.Single(nodes);
        Assert.Equal(3, handler.RequestCount);
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public string? LastAuthScheme { get; private set; }
        public List<string> RequestUris { get; } = new();

        public void EnqueueResponse(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastAuthScheme = request.Headers.Authorization?.Scheme;
            if (request.RequestUri != null)
            {
                RequestUris.Add(request.RequestUri.ToString());
            }
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No response queued.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class ThrowingSequenceHandler : HttpMessageHandler
    {
        private readonly Queue<object> _steps = new();
        public int RequestCount { get; private set; }

        public void EnqueueResponse(HttpResponseMessage response) => _steps.Enqueue(response);

        public void EnqueueException(Exception exception) => _steps.Enqueue(exception);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_steps.Count == 0)
            {
                throw new InvalidOperationException("No step queued.");
            }

            var step = _steps.Dequeue();
            if (step is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult((HttpResponseMessage)step);
        }
    }
}
