using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AzureDevOpsWorkItemExporter.Configuration;
using AzureDevOpsWorkItemExporter.Logging;
using AzureDevOpsWorkItemExporter.Services;
using AzureDevOpsWorkItemExporter.Templates;

namespace AzureDevOpsWorkItemExporter;

internal static class CliRunner
{
    private static readonly Uri AzureDevOpsBaseUri = new(
        Environment.GetEnvironmentVariable("AZDO_BASE_URL") ?? "https://dev.azure.com/");

    public static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("Azure DevOps Workitem Exporter");
        Console.WriteLine($"Version: {GetVersion()}");

        await ExecuteAsync(args);
        return 0;
    }

    private static async Task ExecuteAsync(string[] args)
    {
        var options = CliOptions.Parse(args);

        if (options.Help)
        {
            CliOptions.PrintUsage();
            return;
        }

        if (options.Version)
        {
            Console.WriteLine(GetVersion());
            return;
        }

        var configPath = options.ConfigPath ?? "configuration.yaml";
        using var logger = new ExecutionLogger(AppContext.BaseDirectory, args);
        logger.RecordStart(new
        {
            options.ConfigPath,
            options.OutputPath,
            options.DryRun,
            HasPat = !string.IsNullOrWhiteSpace(options.PatToken)
        });

        try
        {
            var loader = new ConfigLoader();
            var config = loader.Load(configPath);

            if (!string.IsNullOrWhiteSpace(config.Logging?.Location))
            {
                logger.OverrideLocation(config.Logging.Location);
            }

            logger.SetVerbosity(config.Logging?.Verbosity);
            logger.RecordConfiguration(config);
            ConsoleLogger.Log("info", "Configuration validated", new { config.AzureDevOps.Organization, config.AzureDevOps.Project });

            if (options.DryRun)
            {
                Console.WriteLine("Dry-run mode: validation only.");
                ConsoleLogger.Log("info", "Dry-run run requested", new { options.DryRun, config.AzureDevOps?.Project, config.AzureDevOps?.Organization });
                logger.RecordStatus("info", "Dry-run validation completed.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.PatToken))
            {
                throw new InvalidOperationException("PAT is required for executing exports. Pass --pat <token>.");
            }

            var baseOutput = string.IsNullOrWhiteSpace(options.OutputPath)
                ? AppContext.BaseDirectory
                : options.OutputPath;
            Directory.CreateDirectory(baseOutput);

            var runDir = Path.Combine(baseOutput, $"export-{DateTime.UtcNow:ddMMyy-HHmmss}");
            Directory.CreateDirectory(runDir);
            logger.RecordDebug("Resolved output paths", new { BaseOutput = baseOutput, RunDirectory = runDir });
            if (config.Templates.Count > 0)
            {
                logger.RecordDebug("Template mappings resolved", new { Templates = config.Templates });
            }

            using var httpClient = new HttpClient { BaseAddress = AzureDevOpsBaseUri };
            IAzureDevOpsClient azureClient = new AzureDevOpsHttpClient(httpClient);
            var renderer = new TemplateRenderer(AppContext.BaseDirectory);
            var exporter = new FormatExporterService(renderer, config.Templates);
            var orchestrator = new ExportOrchestrator(azureClient, exporter, runDir);
            var exportResults = await orchestrator.ExecuteAsync(config, options.PatToken, CancellationToken.None);
            logger.RecordFieldNames(exportResults.FieldNames);
            logger.RecordStatus("info", $"Prepared {exportResults.SavedPaths.Count} format(s) for output to {runDir}.");

            ConsoleLogger.Log("info", "Export completed", new
            {
                exportResults.WorkItemCount,
                Formats = exportResults.Formats,
                OutputDirectory = runDir,
                SavedFormats = exportResults.SavedPaths.Count
            });
        }
        catch (ConfigurationValidationException validationEx)
        {
            logger.RecordException(validationEx);
            logger.RecordStatus("failed", "Configuration validation failed.");

            ConsoleLogger.Log("error", "Configuration validation failed", new { validationEx.Errors });
            Console.WriteLine("Configuration validation failed:");
            foreach (var error in validationEx.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }
        catch (Exception ex)
        {
            logger.RecordException(ex);
            logger.RecordStatus("failed", "Unexpected error.");

            ConsoleLogger.Log("error", "Unexpected error", new { ex.Message, ex.StackTrace });
            Console.WriteLine("Unexpected error while loading configuration:");
            Console.WriteLine(ex.Message);
        }
    }

    private static string GetVersion()
    {
        var assembly = typeof(CliRunner).Assembly;
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return attribute?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    internal sealed record CliOptions(string? ConfigPath, string? PatToken, string? OutputPath, bool DryRun, bool Help, bool Version)
    {
        public static CliOptions Parse(string[] args)
        {
            string? config = null;
            string? pat = null;
            string? output = null;
            bool dryRun = false;
            bool help = false;
            bool version = false;

            var index = 0;
            while (index < args.Length)
            {
                var token = args[index];
                switch (token)
                {
                    case "--config" when index + 1 < args.Length:
                        config = args[index + 1];
                        index += 2;
                        break;
                    case "--pat" when index + 1 < args.Length:
                        pat = args[index + 1];
                        index += 2;
                        break;
                    case "--output" when index + 1 < args.Length:
                        output = args[index + 1];
                        index += 2;
                        break;
                    case "--dry-run":
                        dryRun = true;
                        index++;
                        break;
                    case "--version":
                        version = true;
                        index++;
                        break;
                    case "--help":
                    case "-h":
                        help = true;
                        index++;
                        break;
                    default:
                        index++;
                        break;
                }
            }

            return new CliOptions(config, pat, output, dryRun, help, version);
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage: dotnet run -- --config <path> [--pat <token>] [--output <dir>] [--dry-run] [--version]");
            Console.WriteLine("  --config <path>   Path to YAML configuration (default: configuration.yaml)");
            Console.WriteLine("  --pat <token>     Personal Access Token (required for exports)");
            Console.WriteLine("  --output <dir>    Base directory where export-<timestamp> folders are created");
            Console.WriteLine("  --dry-run         Validate configuration without exporting");
            Console.WriteLine("  --version         Show semver and exit");
            Console.WriteLine("  --help            Show this help text");
        }
    }
}
