using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AzureDevOpsWorkItemExporter.Configuration;

namespace AzureDevOpsWorkItemExporter.Services;

public sealed class AzureDevOpsHttpClient : IAzureDevOpsClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AzureDevOpsHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<WorkItemNode>> FetchWorkItemsAsync(ConfigRoot config, string patToken, CancellationToken cancellationToken = default)
    {
        if (config.AzureDevOps is null)
        {
            throw new ArgumentException("Azure DevOps configuration missing.", nameof(config));
        }

        if (string.IsNullOrWhiteSpace(patToken))
        {
            throw new InvalidOperationException("Personal Access Token (PAT) is required for HTTP access.");
        }

        SetAuthorizationHeader(patToken);

        var normalizedType = config.Type?.Trim().ToLowerInvariant();
        var ids = normalizedType switch
        {
            "wiql" => await QueryWiqlAsync(config, cancellationToken),
            "wiid" => new[] { int.Parse(config.Wiid!, System.Globalization.CultureInfo.InvariantCulture) },
            _ => throw new InvalidOperationException("Unsupported configuration type.")
        };

        var fetchedDetails = new Dictionary<int, WorkItemDetail>();
        await EnsureNodesAsync(config, ids, fetchedDetails, cancellationToken);

        var export = config.Export ?? new ExportDefinition();
        var link = export.Link?.Trim().ToLowerInvariant() ?? "workitem";
        var childDepth = ShouldIncludeChild(link) ? export.Depth?.Child ?? 0 : 0;
        var parentDepth = ShouldIncludeParent(link) ? export.Depth?.Parent ?? 0 : 0;

        if (!string.Equals(normalizedType, "wiql", StringComparison.OrdinalIgnoreCase))
        {
            if (childDepth > 0)
            {
                await TraverseRelationsAsync(config, fetchedDetails, ids, RelationDirection.Child, childDepth, cancellationToken);
            }

            if (parentDepth > 0)
            {
                await TraverseRelationsAsync(config, fetchedDetails, ids, RelationDirection.Parent, parentDepth, cancellationToken);
            }
        }

        var seedIds = ids.ToHashSet();
        return fetchedDetails.Values.Select(detail => MapToNode(detail, seedIds.Contains(detail.Id))).ToList();
    }

    private void SetAuthorizationHeader(string patToken)
    {
        var tokenBytes = Encoding.ASCII.GetBytes($":{patToken}");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(tokenBytes));
    }

    private async Task<int[]> QueryWiqlAsync(ConfigRoot config, CancellationToken cancellationToken)
    {
        var endpoint = BuildWiqlUri(config);
        var payloadBody = JsonSerializer.Serialize(new { query = config.Wiql });
        using var response = await ExecuteWithRetryAsync(
            () => _httpClient.PostAsync(endpoint, new StringContent(payloadBody, Encoding.UTF8, "application/json"), cancellationToken),
            GetRetryCount(config),
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azure DevOps WIQL query failed ({(int)response.StatusCode} {response.ReasonPhrase}): {payload}");
        }

        var wiql = JsonSerializer.Deserialize<WiqlResponse>(payload, JsonOptions);
        return wiql?.WorkItems?.Select(w => w.Id).ToArray() ?? Array.Empty<int>();
    }

    private async Task<List<WorkItemDetail>> FetchWorkItemsDetailsAsync(ConfigRoot config, int[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return new List<WorkItemDetail>();
        }

        var results = new List<WorkItemDetail>();
        foreach (var batchIds in BatchIds(ids))
        {
            var endpoint = BuildWorkItemsUri(config, batchIds);
            using var response = await ExecuteWithRetryAsync(
                () => _httpClient.GetAsync(endpoint, cancellationToken),
                GetRetryCount(config),
                cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Azure DevOps work item fetch failed ({(int)response.StatusCode} {response.ReasonPhrase}): {payload}");
            }

            var batch = JsonSerializer.Deserialize<WorkItemsBatchResponse>(payload, JsonOptions);
            if (batch?.Value != null)
            {
                results.AddRange(batch.Value);
            }
        }

        return results;
    }

    private async Task<Dictionary<int, List<RelationDto>>> FetchRelationsAsync(ConfigRoot config, int[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return new Dictionary<int, List<RelationDto>>();
        }

        var results = new Dictionary<int, List<RelationDto>>();
        foreach (var batchIds in BatchIds(ids))
        {
            var endpoint = BuildWorkItemsRelationsUri(config, batchIds);
            using var response = await ExecuteWithRetryAsync(
                () => _httpClient.GetAsync(endpoint, cancellationToken),
                GetRetryCount(config),
                cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Azure DevOps relation fetch failed ({(int)response.StatusCode} {response.ReasonPhrase}): {payload}");
            }

            var batch = JsonSerializer.Deserialize<WorkItemsRelationsResponse>(payload, JsonOptions);
            if (batch?.Value == null)
            {
                continue;
            }

            foreach (var item in batch.Value)
            {
                results[item.Id] = item.Relations ?? new List<RelationDto>();
            }
        }

        return results;
    }

    private static string BuildWiqlUri(ConfigRoot config)
    {
        var org = Uri.EscapeDataString(config.AzureDevOps!.Organization ?? string.Empty);
        var project = Uri.EscapeDataString(config.AzureDevOps.Project ?? string.Empty);
        return $"{org}/{project}/_apis/wit/wiql?api-version=7.0";
    }

    private static string BuildWorkItemsUri(ConfigRoot config, int[] ids)
    {
        var org = Uri.EscapeDataString(config.AzureDevOps!.Organization ?? string.Empty);
        var project = Uri.EscapeDataString(config.AzureDevOps.Project ?? string.Empty);
        var idsParam = string.Join(",", ids);
        return $"{org}/{project}/_apis/wit/workitems?ids={idsParam}&api-version=7.0";
    }

    private static string BuildWorkItemsRelationsUri(ConfigRoot config, int[] ids)
    {
        var org = Uri.EscapeDataString(config.AzureDevOps!.Organization ?? string.Empty);
        var project = Uri.EscapeDataString(config.AzureDevOps.Project ?? string.Empty);
        var idsParam = string.Join(",", ids);
        return $"{org}/{project}/_apis/wit/workitems?ids={idsParam}&$expand=relations&api-version=7.0";
    }

    private static WorkItemNode MapToNode(WorkItemDetail detail, bool isSeed)
    {
        var node = new WorkItemNode(detail.Id, detail.Fields.ToDictionary(kvp => kvp.Key, kvp => ConvertElement(kvp.Value)));
        node.IsSeed = isSeed;
        node.Fields["System.Id"] = detail.Id;
        foreach (var relation in detail.Relations ?? new List<RelationDto>())
        {
            if (TryExtractTargetId(relation.Url, out var targetId))
            {
                node.Relations.Add(new WorkItemRelation(relation.Rel, targetId));
            }
        }

        return node;
    }

    private static object? ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText(), JsonOptions),
            JsonValueKind.Array => element.Deserialize<List<object?>>(JsonOptions),
            _ => null
        };
    }

    private static bool TryExtractTargetId(string url, out int targetId)
    {
        targetId = 0;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var lastSegment = uri.Segments.LastOrDefault()?.TrimEnd('/');
        return int.TryParse(lastSegment, out targetId);
    }

    private sealed record WiqlResponse(List<WiqlWorkItem>? WorkItems);

    private sealed record WiqlWorkItem(int Id);

    private sealed record WorkItemsBatchResponse(List<WorkItemDetail>? Value);

    private sealed record WorkItemsRelationsResponse(List<WorkItemRelationDetail>? Value);

    private sealed record WorkItemRelationDetail(int Id, List<RelationDto>? Relations);

    private sealed record WorkItemDetail(int Id, Dictionary<string, JsonElement> Fields, List<RelationDto>? Relations);

    private enum RelationDirection
    {
        Child,
        Parent
    }

    private static bool ShouldIncludeChild(string linkValue) => linkValue == "child" || linkValue == "both";

    private static bool ShouldIncludeParent(string linkValue) => linkValue == "parent" || linkValue == "both";

    private static int GetRetryCount(ConfigRoot config) => config.Export?.Retry ?? 5;

    private static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408 || code == 429 || code == 500 || code == 502 || code == 503 || code == 504;
    }

    private static async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> operation,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                var response = await operation();
                if (!IsTransientStatusCode(response.StatusCode) || attempt > maxRetries + 1)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt <= maxRetries + 1)
            {
                // swallow and retry
            }

            if (attempt > maxRetries + 1)
            {
                throw new HttpRequestException("Maximum retry attempts exceeded.");
            }

            var delay = GetBackoffDelay(attempt);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is HttpRequestException || ex is TaskCanceledException;
    }

    private static TimeSpan GetBackoffDelay(int attempt)
    {
        var baseDelayMs = 200;
        var maxDelayMs = 2000;
        var expo = Math.Min(maxDelayMs, baseDelayMs * Math.Pow(2, attempt - 1));
        var jitter = Random.Shared.Next(0, 150);
        return TimeSpan.FromMilliseconds(expo + jitter);
    }

    private static IEnumerable<int[]> BatchIds(int[] ids, int batchSize = 200)
    {
        for (var i = 0; i < ids.Length; i += batchSize)
        {
            var count = Math.Min(batchSize, ids.Length - i);
            var batch = new int[count];
            Array.Copy(ids, i, batch, 0, count);
            yield return batch;
        }
    }

    private async Task EnsureNodesAsync(ConfigRoot config, IEnumerable<int>? ids, Dictionary<int, WorkItemDetail> cache, CancellationToken cancellationToken)
    {
        if (ids is null)
        {
            return;
        }

        var missing = ids.Where(id => !cache.ContainsKey(id)).Distinct().ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        var fetched = await FetchWorkItemsDetailsAsync(config, missing, cancellationToken);
        var relations = await FetchRelationsAsync(config, missing, cancellationToken);

        foreach (var detail in fetched)
        {
            var withRelations = detail with
            {
                Relations = relations.TryGetValue(detail.Id, out var items) ? items : detail.Relations
            };

            cache[withRelations.Id] = withRelations;
        }
    }

    private async Task TraverseRelationsAsync(
        ConfigRoot config,
        Dictionary<int, WorkItemDetail> cache,
        IEnumerable<int> startIds,
        RelationDirection direction,
        int remainingDepth,
        CancellationToken cancellationToken)
    {
        if (remainingDepth <= 0)
        {
            return;
        }

        var queue = new Queue<(int id, int depth)>();
        var scheduledDepth = new Dictionary<int, int>();
        foreach (var id in startIds.Distinct())
        {
            Enqueue(id, remainingDepth);
        }

        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();
            var detail = await EnsureDetailAsync(config, cache, currentId, cancellationToken);
            if (detail is null)
            {
                continue;
            }

            var targets = GetTargetIds(detail, direction);
            if (targets.Length == 0)
            {
                continue;
            }

            var missing = targets.Where(id => !cache.ContainsKey(id)).ToArray();
            if (missing.Length > 0)
            {
                await EnsureNodesAsync(config, missing, cache, cancellationToken);
            }

            foreach (var targetId in targets)
            {
                Enqueue(targetId, depth - 1);
            }
        }

        void Enqueue(int id, int depth)
        {
            if (depth <= 0)
            {
                return;
            }

            if (scheduledDepth.TryGetValue(id, out var previousDepth) && previousDepth >= depth)
            {
                return;
            }

            scheduledDepth[id] = depth;
            queue.Enqueue((id, depth));
        }
    }

    private async Task<WorkItemDetail?> EnsureDetailAsync(
        ConfigRoot config,
        Dictionary<int, WorkItemDetail> cache,
        int currentId,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(currentId, out var detail))
        {
            return detail;
        }

        await EnsureNodesAsync(config, new[] { currentId }, cache, cancellationToken);
        return cache.TryGetValue(currentId, out detail) ? detail : null;
    }

    private static int[] GetTargetIds(WorkItemDetail detail, RelationDirection direction)
    {
        if (detail.Relations is null || detail.Relations.Count == 0)
        {
            return Array.Empty<int>();
        }

        return detail.Relations
            .Where(link => MatchesDirection(link.Rel, direction))
            .Select(link => TryExtractTargetId(link.Url, out var targetId) ? (int?)targetId : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
    }

    private static bool MatchesDirection(string relationType, RelationDirection direction)
    {
        if (string.IsNullOrWhiteSpace(relationType))
        {
            return false;
        }

        return direction switch
        {
            RelationDirection.Child => relationType.Equals("System.LinkTypes.Hierarchy-Forward", StringComparison.OrdinalIgnoreCase),
            RelationDirection.Parent => relationType.Equals("System.LinkTypes.Hierarchy-Reverse", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private sealed record RelationDto(string Rel, string Url);
}
