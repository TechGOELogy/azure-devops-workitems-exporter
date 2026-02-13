using System;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlToOpenXml;

namespace AzureDevOpsWorkItemExporter.Templates;

public sealed class HtmlToDocxConverter : IHtmlToDocxConverter
{
    public byte[] Convert(string html)
    {
        var safeHtml = html ?? string.Empty;

        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
                   stream,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                   true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            var converter = new HtmlConverter(mainPart);
            var elements = converter.Parse(safeHtml);
            foreach (var element in elements)
            {
                mainPart.Document.Body!.AppendChild(element);
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }
}
