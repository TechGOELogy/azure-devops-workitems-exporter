using AzureDevOpsWorkItemExporter;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Parse_ReadsAllKnownFlags()
    {
        var options = CliRunner.CliOptions.Parse(new[]
        {
            "--config", "config.yaml",
            "--pat", "token",
            "--output", "out",
            "--dry-run",
            "--version",
            "-h",
            "extra"
        });

        Assert.Equal("config.yaml", options.ConfigPath);
        Assert.Equal("token", options.PatToken);
        Assert.Equal("out", options.OutputPath);
        Assert.True(options.DryRun);
        Assert.True(options.Version);
        Assert.True(options.Help);
    }

    [Fact]
    public void Parse_IgnoresUnknownArguments()
    {
        var options = CliRunner.CliOptions.Parse(new[] { "--unknown", "value" });

        Assert.Null(options.ConfigPath);
        Assert.Null(options.PatToken);
        Assert.Null(options.OutputPath);
        Assert.False(options.DryRun);
        Assert.False(options.Version);
        Assert.False(options.Help);
    }
}
