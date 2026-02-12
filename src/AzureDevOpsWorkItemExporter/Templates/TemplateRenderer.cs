using System;
using System.IO;
using Scriban;

namespace AzureDevOpsWorkItemExporter.Templates;

public sealed class TemplateRenderer
{
    private readonly string _baseDirectory;

    public TemplateRenderer(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
    }

    public string Render(string templatePath, object model)
    {
        var resolvedPath = ResolveTemplatePath(templatePath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("Template not found.", resolvedPath);
        }

        var templateText = File.ReadAllText(resolvedPath);
        var template = Template.Parse(templateText);
        if (template.HasErrors)
        {
            var details = string.Join("; ", template.Messages);
            throw new InvalidOperationException($"Template parsing failed: {details}");
        }

        return template.Render(model);
    }

    private string ResolveTemplatePath(string templatePath)
    {
        if (Path.IsPathRooted(templatePath))
        {
            return templatePath;
        }

        var candidate = Path.Combine(_baseDirectory, templatePath);
        return candidate;
    }
}
