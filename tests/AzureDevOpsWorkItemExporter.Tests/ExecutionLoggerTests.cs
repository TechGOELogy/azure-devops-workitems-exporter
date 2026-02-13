using System;
using System.IO;
using System.Linq;
using AzureDevOpsWorkItemExporter.Logging;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests;

public class ExecutionLoggerTests
{
    private static readonly string[] DefaultArgs = { "--config", "configuration.yaml" };
    private static readonly string[] PatArgs = { "--config", "configuration.yaml", "--pat", "secret-token" };
    private static readonly string[] InlinePatArgs = { "--config", "configuration.yaml", "--pat=secret-inline" };
    private static readonly string[] FieldNames = { "System.Id", "System.Title" };

    [Fact]
    public void RecordStartAndStatus_AppendsEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var logger = new ExecutionLogger(tempDir, DefaultArgs);
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
        logger.RecordFieldNames(FieldNames);
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

        var logger = new ExecutionLogger(tempDir, PatArgs);
        logger.RecordStart(new { });
        logger.Dispose();

        var files = Directory.GetFiles(tempDir);
        Assert.Single(files);

        var entries = File.ReadAllText(files[0]);
        Assert.Contains("--pat", entries);
        Assert.Contains("***", entries);
        Assert.DoesNotContain("secret-token", entries);
    }

    [Fact]
    public void RecordStart_MasksInlinePatToken()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var logger = new ExecutionLogger(tempDir, InlinePatArgs);
        logger.RecordStart(new { });
        logger.Dispose();

        var files = Directory.GetFiles(tempDir);
        Assert.Single(files);

        var entries = File.ReadAllText(files[0]);
        Assert.Contains("--pat=***", entries);
        Assert.DoesNotContain("secret-inline", entries);
    }

    [Fact]
    public void RecordWarning_WritesEntry()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var logger = new ExecutionLogger(tempDir, DefaultArgs);
        logger.RecordWarning("warning", new { Code = 1 });
        logger.Dispose();

        var files = Directory.GetFiles(tempDir);
        Assert.Single(files);

        var entries = File.ReadAllText(files[0]);
        Assert.Contains("warning", entries);
        Assert.Contains("Code", entries);
    }

    [Fact]
    public void RecordException_WritesError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var logger = new ExecutionLogger(tempDir, DefaultArgs);
        logger.RecordException(new InvalidOperationException("boom"));
        logger.Dispose();

        var files = Directory.GetFiles(tempDir);
        Assert.Single(files);

        var entries = File.ReadAllText(files[0]);
        Assert.Contains("Exception occurred", entries);
        Assert.Contains("InvalidOperationException", entries);
        Assert.Contains("boom", entries);
    }

    [Fact]
    public void SetVerbosity_UnknownDefaultsToInfo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var logger = new ExecutionLogger(tempDir, DefaultArgs);
        logger.SetVerbosity("unknown");
        logger.RecordDebug("debug-hidden", new { });
        logger.RecordStatus("ok", "visible");
        logger.Dispose();

        var entries = File.ReadAllText(Directory.GetFiles(tempDir)[0]);
        Assert.DoesNotContain("debug-hidden", entries);
        Assert.Contains("Execution ok", entries);
    }
}
