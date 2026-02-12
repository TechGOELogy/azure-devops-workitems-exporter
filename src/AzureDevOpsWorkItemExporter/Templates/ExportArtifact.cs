namespace AzureDevOpsWorkItemExporter.Templates;

public sealed record ExportArtifact(string? Text, byte[]? Bytes)
{
    public bool IsBinary => Bytes is not null;

    public static ExportArtifact FromText(string text) => new(text, null);

    public static ExportArtifact FromBytes(byte[] bytes) => new(null, bytes);
}
