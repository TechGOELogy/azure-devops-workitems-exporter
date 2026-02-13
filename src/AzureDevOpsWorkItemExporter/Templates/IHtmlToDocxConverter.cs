namespace AzureDevOpsWorkItemExporter.Templates;

public interface IHtmlToDocxConverter
{
    byte[] Convert(string html);
}
