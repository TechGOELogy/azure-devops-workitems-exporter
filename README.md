# Azure DevOps Workitem Exporter

CLI first exporter that fetches Azure DevOps work items (via WIQL/query ID/parents/children) and writes them to templated formats (Word/HTML/Markdown/PDF) plus structured CSV, Excel and JSON output. Key features:

## Attribution

This tool was specifically made by GPT-5.3 Codex fully. Estimated Codex contribution: 85%.

1. **CLI driven** - accepts `--config`, `--pat`, `--output`, `--dry-run`, `--version`. Config points at `configuration.yaml` with Azure DevOps connection metadata, export depth controls, templates paths, and logging overrides.
2. **Templating with Scriban** - supply per-format Scriban files for Word/PDF/HTML/Markdown; the renderer passes `work_items`, `selected_fields`, and `export_meta` (title/generation time/organization/project/query/link/depth/retry/formats) so templates can iterate over hierarchy, display fields, and add headers/sections.
3. **Structured exporters** - CSV/JSON/Excel honor the `select` block (required for those formats); template outputs always receive full field sets so your Scriban logic can include any Azure DevOps property. Each export logs start/complete messages and writes outputs into `export-<timestamp>` folders from the base directory.
4. **Robust logging** - console shows timestamped severity lines, PDF/Word exports log their start/finish, JSON logfile per run tracks history with intensity control plus PAT masking and log location override.
5. **Versioned, self-contained** - `.csproj` declares `1.0.0` metadata, and the CLI prints name + version before running (and logs it per run).

## Getting started

1. Restore & test:

```bash
dotnet restore
dotnet test
```

2. Create `configuration.yaml` (copy sample) with your organization/project, `type` (WIQL or WIID), depth/link/type etc. Provide per-format template paths under `templates`.
3. Run exporting:

```bash
dotnet run -- --config configuration.yaml --pat <PAT> --output ./export --dry-run
dotnet run -- --config configuration.yaml --pat <PAT> --output ./export
```

Logs go to `export-logs/` unless overridden; each CLI run also creates a `export-<timestamp>` folder for artifacts.

## Templates

Place Scriban files in `template-examples/` (the repo already has examples for word/html/markdown/pdf). Example placeholders:

```scriban
{{ export_meta.title }}
Generated: {{ export_meta.generated_at }}

{{~ for work_item in work_items ~}}
ID: {{ work_item.id }}
Title: {{ work_item.fields["System.Title"] }}
{{~ end ~}}
```

Word/PDF templates treat each text line as a paragraph/line; adjust spacing with blank lines or custom separators (`----`). Use `selected_fields` or `export_meta.formats` to drive conditional sections; the renderer passes complete Azure DevOps fields plus hierarchy arrays (`work_item.children`, `work_item.parents`).

## Contribution

1. Update templates under `template-examples/` for new formats (use Scriban).
2. Add tests when you touch export logic (`tests/AzureDevOpsWorkItemExporter.Tests` has helpers for logging, config loader, etc.).
3. Keep version increments in `.csproj` + README release notes.

## License

MIT (c) 2026 Shubham Goel - see [LICENSE](LICENSE).
