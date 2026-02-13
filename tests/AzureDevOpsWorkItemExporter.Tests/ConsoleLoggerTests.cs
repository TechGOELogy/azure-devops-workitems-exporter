using System;
using System.IO;
using AzureDevOpsWorkItemExporter.Logging;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests;

[Collection("Console")]
public class ConsoleLoggerTests
{
    [Fact]
    public void Log_WritesFormattedPayload()
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            ConsoleLogger.Log("info", "message", new
            {
                Count = 2,
                Items = new object?[] { "a", null },
                When = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains("[INFO]", output);
        Assert.Contains("message", output);
        Assert.Contains("Count=2", output);
        Assert.Contains("Items=[a, null]", output);
        Assert.Contains("When=2020-01-01T00:00:00.0000000Z", output);
    }

    [Fact]
    public void Log_WithoutPayload_WritesMessageOnly()
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            ConsoleLogger.Log("warn", "no payload");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains("[WARN]", output);
        Assert.Contains("no payload", output);
        Assert.DoesNotContain("|", output);
    }
}
