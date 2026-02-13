namespace AzureDevOpsWorkItemExporter.Templates;

public interface IHtmlToPdfRenderer
{
    byte[] Render(string html);
}
