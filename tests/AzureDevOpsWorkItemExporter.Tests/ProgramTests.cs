using System;
using System.IO;
using System.Threading.Tasks;
using AzureDevOpsWorkItemExporter;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests;

[Collection("Console")]
public class ProgramTests
{
    [Fact]
    public async Task Main_WithHelp_PrintsUsage()
    {
        var output = await CaptureOutputAsync(() => CliRunner.RunAsync(new[] { "--help" }));

        Assert.Contains("Usage:", output);
        Assert.Contains("Azure DevOps Workitem Exporter", output);
    }

    [Fact]
    public async Task Main_WithVersion_PrintsVersion()
    {
        var output = await CaptureOutputAsync(() => CliRunner.RunAsync(new[] { "--version" }));

        Assert.Contains("Version:", output);
    }

    [Fact]
    public async Task Main_WithDryRun_PrintsDryRunMessage()
    {
        var configPath = CreateTempConfig(@"
azure-devops:
  organization: sample-org
  project: SampleProject

type: wiql
wiql: SELECT [System.Id] FROM WorkItems

export:
  link: workitem
  type:
    - html
");

        try
        {
            var output = await CaptureOutputAsync(() => CliRunner.RunAsync(new[] { "--config", configPath, "--dry-run" }));
            Assert.Contains("Dry-run mode: validation only.", output);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task Main_WithInvalidConfig_PrintsValidationErrors()
    {
        var configPath = CreateTempConfig(@"
azure-devops:
  organization: sample-org
  project: SampleProject

type: wiql
wiql: SELECT [System.Id] FROM WorkItems
");

        try
        {
            var output = await CaptureOutputAsync(() => CliRunner.RunAsync(new[] { "--config", configPath }));
            Assert.Contains("Configuration validation failed:", output);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task Main_WithMissingPat_PrintsError()
    {
        var configPath = CreateTempConfig(@"
azure-devops:
  organization: sample-org
  project: SampleProject

type: wiql
wiql: SELECT [System.Id] FROM WorkItems

export:
  link: workitem
  type:
    - html
");

        try
        {
            var output = await CaptureOutputAsync(() => CliRunner.RunAsync(new[] { "--config", configPath }));
            Assert.Contains("Unexpected error while loading configuration:", output);
            Assert.Contains("PAT is required", output);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    private static async Task<string> CaptureOutputAsync(Func<Task<int>> action)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            await action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return writer.ToString();
    }

    private static string CreateTempConfig(string contents)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, contents);
        return path;
    }
}
