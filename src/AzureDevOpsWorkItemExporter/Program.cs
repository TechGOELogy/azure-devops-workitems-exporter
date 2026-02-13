using System.Threading.Tasks;

namespace AzureDevOpsWorkItemExporter;

internal static class Program
{
    public static Task<int> Main(string[] args) => CliRunner.RunAsync(args);
}
