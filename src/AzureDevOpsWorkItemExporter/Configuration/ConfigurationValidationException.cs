using System;
using System.Collections.Generic;

namespace AzureDevOpsWorkItemExporter.Configuration;

public sealed class ConfigurationValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public ConfigurationValidationException(IEnumerable<string> errors)
        : base("Configuration validation failed.")
    {
        Errors = errors is IReadOnlyList<string> list ? list : new List<string>(errors);
    }
}
