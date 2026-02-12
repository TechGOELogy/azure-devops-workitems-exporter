using System;
using System.IO;
using System.Linq;
using AzureDevOpsWorkItemExporter.Logging;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests;

public class ExecutionLoggerTests
{
    [Fact]
    public void RecordStartAndStatus_AppendsEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var logger = new ExecutionLogger(tempDir, new[] { "--config", "configuration.yaml" });
        logger.RecordStart(new { Mode = "test" });
        logger.RecordStatus("passed", "Validation succeeded.");
        logger.Dispose();

        var files = Directory.GetFiles(tempDir).ToArray();
        Assert.Single(files);

        var entries = File.ReadAllLines(files[0]);
        Assert.Contains("Started CLI process", string.Join(' ', entries));
        Assert.Contains("Execution passed", string.Join(' ', entries));
    }

    [Fact]
    public void RecordFieldNames_IncludesFieldList()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var logger = new ExecutionLogger(tempDir, Array.Empty<string>());
        logger.RecordFieldNames(new[] { "System.Id", "System.Title" });
        logger.Dispose();

        var files = Directory.GetFiles(tempDir);
        Assert.Single(files);

        var entries = File.ReadAllText(files[0]);
        Assert.Contains("Available Azure DevOps fields", entries);
        Assert.Contains("System.Title", entries);
    }

    [Fact]
    public void RecordDebug_RespectsVerbosity()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var logger = new ExecutionLogger(tempDir, Array.Empty<string>());
        logger.SetVerbosity("WARN");
        logger.RecordDebug("debug-only", new { });
        logger.RecordStatus("passed", "ok");
        logger.Dispose();

        var files = Directory.GetFiles(tempDir);
        Assert.Single(files);

        var entries = File.ReadAllText(files[0]);
        Assert.DoesNotContain("debug-only", entries);
        Assert.Contains("Execution passed", entries);
    }

    [Fact]
    public void OverrideLocation_MovesLogFile()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var newDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);

        var logger = new ExecutionLogger(baseDir, Array.Empty<string>());
        logger.RecordStart(new { });
        logger.OverrideLocation(newDir);
        logger.RecordStatus("passed", "Moved.");
        logger.Dispose();

        var oldFiles = Directory.GetFiles(baseDir);
        Assert.Empty(oldFiles);

        var newFiles = Directory.GetFiles(newDir);
        Assert.Single(newFiles);

        var entries = File.ReadAllText(newFiles[0]);
        Assert.Contains("Moved.", entries);
    }

    [Fact]
    public void RecordStart_MasksPatToken()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var logger = new ExecutionLogger(tempDir, new[] { "--config", "configuration.yaml", "--pat", "secret-token" });
        logger.RecordStart(new { });
        logger.Dispose();

        var files = Directory.GetFiles(tempDir);
        Assert.Single(files);

        var entries = File.ReadAllText(files[0]);
        Assert.Contains("--pat", entries);
        Assert.Contains("***", entries);
        Assert.DoesNotContain("secret-token", entries);
    }
}
