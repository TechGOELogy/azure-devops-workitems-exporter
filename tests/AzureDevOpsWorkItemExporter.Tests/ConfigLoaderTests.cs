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
    public void Load_EmptyPath_ThrowsArgumentException()
    {
        var loader = new ConfigLoader();
        Assert.Throws<ArgumentException>(() => loader.Load(" "));
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFoundException()
    {
        var loader = new ConfigLoader();
        Assert.Throws<FileNotFoundException>(() => loader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
    }

    [Fact]
    public void Load_MissingAzureDevOps_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace("azure-devops:\n  organization: sample-org\n  project: SampleProject\n\n", string.Empty, StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("azure-devops", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_InvalidType_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace("type: wiql", "type: unknown", StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("type must be either", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_MissingExportSection_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace("export:\n  link: both\n  type:\n    - csv\n  depth:\n    parent: 1\n    child: 2\n  retry: 3\n", string.Empty, StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            var combined = string.Join(" ", ex.Errors);
            Assert.Contains("export.link", combined, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("export.type", combined, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_InvalidExportLink_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace("link: both", "link: invalid", StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("export.link must be one of", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_EmptyExportType_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace("type:\n    - csv\n", "type: []\n", StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("export.type must include at least one format", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_ExportTypeWithBlankEntry_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace("    - csv", "    - \"\"", StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("export.type entries must be non-empty", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_NegativeRetry_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace("retry: 3", "retry: -1", StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("export.retry", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_WiqlMissing_ThrowsValidationException()
    {
        var brokenYaml = ValidSample.Replace("wiql: SELECT [System.Id] FROM WorkItems\n\n", string.Empty, StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("wiql is required", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_WiidMissing_ThrowsValidationException()
    {
        var brokenYaml = ValidSample
            .Replace("type: wiql", "type: wiid", StringComparison.Ordinal)
            .Replace("wiql: SELECT [System.Id] FROM WorkItems\n\n", string.Empty, StringComparison.Ordinal);
        var tempFile = CreateTempConfig(brokenYaml);
        try
        {
            var loader = new ConfigLoader();
            var ex = Assert.Throws<ConfigurationValidationException>(() => loader.Load(tempFile));
            Assert.Contains("wiid is required", string.Join(" ", ex.Errors), StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void Load_LoggingSettings_AreParsed()
    {
        var yaml = ValidSample + @"
logging:
  verbosity: DEBUG
  location: logs
";
        var tempFile = CreateTempConfig(yaml);
        try
        {
            var loader = new ConfigLoader();
            var config = loader.Load(tempFile);
            Assert.Equal("DEBUG", config.Logging.Verbosity);
            Assert.Equal("logs", config.Logging.Location);
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
