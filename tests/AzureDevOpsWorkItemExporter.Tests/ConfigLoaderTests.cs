using System;
using System.IO;
using AzureDevOpsWorkItemExporter.Configuration;
using Xunit;

namespace AzureDevOpsWorkItemExporter.Tests;

public class ConfigLoaderTests
{
    private const string ValidSample = @"
azure-devops:
  organization: sample-org
  project: SampleProject

type: wiql
wiql: SELECT [System.Id] FROM WorkItems

select:
  - System.Id
  - System.Title

export:
  link: both
  type:
    - csv
  depth:
    parent: 1
    child: 2
  retry: 3
";

    [Fact]
    public void Load_ValidConfiguration_ReturnsModel()
    {
        var tempFile = CreateTempConfig(ValidSample);
        try
        {
            var loader = new ConfigLoader();
            var config = loader.Load(tempFile);

            Assert.NotNull(config.AzureDevOps);
            Assert.Equal("sample-org", config.AzureDevOps.Organization);
            Assert.Contains("csv", config.Export.Type, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_NegativeDepth_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace("parent: 1", "parent: -1", StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("export.depth.parent", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_MissingSelectForCsv_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace(
            "select:\n  - System.Id\n  - System.Title\n\n",
            string.Empty,
            StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("select must include at least one field", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_MissingSelectForHtml_AllowsConfiguration()
    {
        var htmlYaml = ValidSample
            .Replace("select:\n  - System.Id\n  - System.Title\n\n", string.Empty, StringComparison.Ordinal)
            .Replace("    - csv", "    - html", StringComparison.Ordinal);
        var tempFile = CreateTempConfig(htmlYaml);
        try
        {
            var loader = new ConfigLoader();
            var config = loader.Load(tempFile);
            Assert.Contains("html", config.Export.Type, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static string CreateTempConfig(string contents)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, contents);
        return path;
    }
}
