using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace AzureDevOpsWorkItemExporter.Logging;

public static class ConsoleLogger
{
    public static void Log(string severity, string message, object? data = null)
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        var payload = data is null ? string.Empty : $" | {FormatPayload(data)}";
        Console.WriteLine($"[{timestamp}] [{severity.ToUpperInvariant()}] {message}{payload}");
    }

    private static string FormatPayload(object data)
    {
        var props = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var segments = new List<string>(props.Length);

        foreach (var prop in props)
        {
            var value = prop.GetValue(data);
            var formatted = value switch
            {
                null => "null",
                DateTime dt => dt.ToString("o"),
                string str => str,
                IEnumerable enumerable => FormatEnumerable(enumerable),
                _ => value.ToString() ?? string.Empty
            };

            segments.Add($"{prop.Name}={formatted}");
        }

        return string.Join(", ", segments);
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var items = new List<string>();
        foreach (var item in enumerable)
        {
            if (item is null)
            {
                items.Add("null");
                continue;
            }

            items.Add(item.ToString() ?? string.Empty);
        }

        return $"[{string.Join(", ", items)}]";
    }
}
