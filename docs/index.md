# Azure DevOps Workitem Exporter

*Self-contained CLI that exports Azure DevOps work items into Word, PDF, Markdown, HTML plus structured CSV/JSON/Excel using Scriban templates and configurable depth semantics.*

## Attribution

This tool was specifically made by GPT-5.3 Codex fully. Estimated Codex contribution: 85%.

## Home

| CLI | Source |
| --- | ------ |
| [`dotnet run -- --config configuration.yaml --pat <PAT>`](#cli-flags) | Source tree root (this repo) |

Use the `configuration.yaml` in the repo root (copy it, adjust your organization/project, WIQL/WIID selector, select fields, export depth, templates and logging destinations) then run the CLI using `dotnet run` or `dotnet publish` followed by the resulting executable. Each run prints:

- CLI banner + version (matching `.csproj`).
- timestamped console log lines (`[ISO timestamp] [INFO|ERROR] ...`) for configuration validation, per-format export start/completion, dry runs, and failures.
- Binary emits JSON logs per run (configurable location, log4net-like layout, PAT masking) plus exported artifacts under `export-<timestamp>`.


## Features

1. **Flexible selectors**: `config.type` accepts `wiql` (run a WIQL query) or `wiid` (single ID with parent/child traversal). When WIQL is specified, the `export.link` and `export.depth` blocks are ignored - the WIQL results are authoritative (but `select` still governs CSV/JSON/Excel columns).  
2. **Depth-controlled hierarchy**: specify `export.link` (child/parent/both/workitem) plus `export.depth.parent`/`export.depth.child`. Only immediate connections are fetched for structured exports, yet templates can traverse the full hierarchy.  
3. **Multiple formats**: choose from `word`, `pdf`, `html`, `md`, `csv`, `json`, `excel` (more easily extendable via templates). PDF/Word exports use Scriban templates that treat each line as a paragraph/line; structured formats rely on selected fields.  
4. **Template management**: declare paths under `templates` (per format) in `configuration.yaml`. Fallback templates are under `template-examples/` (global Markdown/HTML).  
5. **Robust logging**: console logs with severity, a JSON history log per run, per-format start/complete messages, and PAT masking in CLI arguments.


## CLI Flags

```
--config <path>     Path to configuration YAML (defaults to `configuration.yaml`)
--pat <token>       Personal Access Token (required except dry-run)
--output <dir>      Base directory for `export-<timestamp>` output folders (defaults to binary directory)
--dry-run          Validate configuration without exporting artifacts
--version          Print CLI version (from `.csproj`) and exit
--help, -h         Show help text
```

### Example

```bash
dotnet run -- \
  --config configuration.yaml \
  --pat YOUR_PAT \
  --output ./export-outputs
```

Dry run:

```bash
dotnet run -- --config configuration.yaml --pat YOUR_PAT --dry-run
```


## Configuration File (`configuration.yaml`)

Example schema:

```yaml
azure-devops:
  organization: azure-devops-organization
  project: "Project Name"

type: wiid
wiid: 1234

select:
  - System.Id
  - System.Title

export:
  link: child
  type:
    - pdf
    - word
    - html
    - md
    - excel
    - json
    - csv
  depth:
    parent: 0
    child: 1
  retry: 5

logging:
  verbosity: DEBUG
  location: ./export-logs

templates:
  word: template-examples/word-template.scriban
  pdf: template-examples/pdf-template.scriban
  html: template-examples/html-template-parent.scriban
  md: template-examples/markdown-template-child.scriban
```

- `select` is mandatory when exporting CSV/JSON/Excel and defines column order; templated formats still receive the full set of Azure DevOps fields (`work_item.fields`).  
- `templates` entries may be absolute or relative to the binary directory.  
- `logging.location` overrides where JSON log files are saved; the CLI also creates history logs by default under `export-logs/`.


## Template Structure

Templates are Scriban text files. The renderer exposes a model:

```scriban
{
  work_items: [ ... ],
  selected_fields: [ ... ],
  export_meta: {
    title, summary, generated_at,
    organization, project, query_type,
    wiql, wiid, link, depth_parent, depth_child,
    retry, formats, run_directory
  }
}
```

Templates for Word and PDF treat each newline as a paragraph/line, so control spacing with blank lines or separators.


## Templates Included

Sample templates live in the template examples folder. See [template-examples](https://github.com/TechGOELogy/azure-devops-workitems-exporter/tree/main/src/AzureDevOpsWorkItemExporter/template-examples) for full examples and Scriban patterns. For deeper syntax and built-ins, read the [Scriban documentation](https://github.com/scriban/scriban).


## Logs

- Console: timestamped severity lines without JSON (plain `key=value` payload).  
- `ExecutionLogger` writes JSON logs per run (masked PATs, configurable location) and contains entry types like `start`, `status`, `fieldNames`, `exceptions`, and debug statements.


## Contribution & Releases

1. Update `.csproj` version metadata (`Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`) for each release.  
2. Keep README/docs in sync - document template changes, CLI flags, or logging adjustments.  
3. Add tests under `tests/AzureDevOpsWorkItemExporter.Tests` when altering export/hierarchy logic, and ensure `dotnet test` passes before merging.  
4. Optional: add GitHub Actions to run `dotnet test` and build the CLI for automatic validation on PRs.  


## About the Hosted Documentation

This `docs/index.md` is published via GitHub Pages (set Pages source to the `docs/` folder) providing CLI instructions, configuration schema, template guidance, logging behavior, and contributor notes-perfect for consuming online before cloning the repo.
