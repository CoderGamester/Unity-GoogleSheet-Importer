# GameLovers DataImporter

Editor tooling that imports CSV files, Excel worksheets, and Google Sheets into ScriptableObject configuration assets.

[![Unity](https://img.shields.io/badge/Unity-6000.0%20%7C%206000.3%20%7C%206000.5-blue.svg)](https://unity.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)

## When to use it

Use this package when game data is authored as tables, by designers in a spreadsheet or by people and agents in a CSV file, and the game consumes typed configuration. Every source becomes a CSV file in the project, and an Editor-only importer bakes that file into a ScriptableObject asset. A player never parses sheet data: it loads the asset. The package is pipeline-neutral.

It is not a runtime CSV or spreadsheet reader, and it has no authentication flow for private Google Sheets.

## Unity compatibility

| Item | Current policy |
| --- | --- |
| Minimum Unity version | `6000.0` |
| Reference streams | `6000.0.x`, `6000.3.x`, `6000.5.x` |
| Reference editors | `6000.0.81f1`, `6000.3.21f1`, `6000.5.7f1` (primary) |
| Render pipeline | Pipeline-neutral |
| Validation status | Compatibility target; check the repository matrix for fresh results. |

## Install

Git dependencies are not resolved transitively by Unity. Install GameData and this package together, replacing `<release-tag>` with the newest tag listed in [CHANGELOG.md](CHANGELOG.md):

```json
{
  "dependencies": {
    "com.gamelovers.gamedata": "https://github.com/CoderGamester/Unity-GameData.git#1.0.3",
    "com.gamelovers.dataimporter": "https://github.com/CoderGamester/Unity-GoogleSheet-Importer.git#<release-tag>"
  }
}
```

Coming from `com.gamelovers.googlesheetimporter`? Follow [MIGRATION.md](MIGRATION.md).

## First success

Put configuration types in a runtime assembly, then put the importer in an `Editor/` folder or Editor-only asmdef.

```csharp
// Runtime assembly
using System;
using System.Collections.Generic;
using GameLovers.GameData;
using UnityEngine;

[Serializable]
public struct ItemConfig { public int Id; public string Name; public float Power; }

public sealed class ItemConfigs : ScriptableObject, IConfigsContainer<ItemConfig>
{
    [SerializeField] private List<ItemConfig> _configs = new();
    public List<ItemConfig> Configs { get => _configs; set => _configs = value; }
}
```

```csharp
// Editor-only assembly
using GameLovers.DataImporter.Editor;

public sealed class ItemConfigsImporter : ConfigsImporter<ItemConfig, ItemConfigs>
{
    public override string CsvPath => "Assets/Data/Items.csv";
}
```

```text
Id,Name,Power
1,Wooden Sword,4.5
2,Iron Shield,2
```

Save the table as `Assets/Data/Items.csv` and run `Tools/GameLovers/Data Importer/Import All`. The importer writes `Assets/Data/ItemConfigs.asset`, or updates the project's existing `ItemConfigs` asset wherever it is.

## Sources

The CSV file is the one input of an import. A spreadsheet is an optional upstream that **Pull** copies into that file, so the data in the project is always a reviewable diff and an import works offline.

| Source | Declare on the importer | Refresh with |
| --- | --- | --- |
| CSV file in the project | `CsvPath` only | Edit the file |
| Excel worksheet | `Source => "Assets/Data/Balance.xlsx#Items"` | Pull |
| Google Sheet tab | `Source =>` the tab's URL as copied from the browser | Pull |

- **Excel:** values only, read without a third-party library. Formulas import as their last calculated value, dates as `yyyy-MM-dd`, booleans as `TRUE`/`FALSE`. Without `#SheetName` the first worksheet is read.
- **Google Sheets:** use the URL of the sheet's edit page, not a published-to-web link. Without a key, the sheet must be shared as "Anyone with the link", which makes it public data.
- **Private Google Sheets:** create a service account in Google Cloud, download its JSON key, and share the sheet with the account's `client_email` as a Viewer. Point the importer at the key with one of these, first match wins:
  1. `-dataImportServiceAccountKey <path>` on the command line.
  2. The **Service account key** field in the window. It is stored for you and this project in EditorPrefs, not in the project.
  3. The `GOOGLE_APPLICATION_CREDENTIALS` environment variable, the usual choice for CI.

  Keep the key file outside the project: anyone with it can read every sheet shared with the account. The package warns when the key is inside the project folder.

## Outputs

| Output | How | Use it for |
| --- | --- | --- |
| ScriptableObject asset | Always written | The default: shipped in the build or delivered through Addressables |
| JSON payload | `public override bool WriteJsonPayload => true;` | Remotely delivered content that must load into a player built with a different version of the config type |

The payload is a `.json` TextAsset written next to the asset with the same content. Load it into the same container type:

```csharp
var configs = ScriptableObject.CreateInstance<ItemConfigs>();
JsonUtility.FromJsonOverwrite(payload.text, configs);
```

Prefer the asset unless you need the payload. Measured in a macOS standalone player (Mono, Unity 6000.6.6f1) with 100,000 rows, the asset loaded in about 90 ms and allocated 33 MB; the same data as a JSON payload took about 180 ms and allocated 89 MB. At 1,000 rows both load in under 2 ms.

## Data-shape rules

| Rule | Behavior |
| --- | --- |
| Fields | Columns match public field names, case-sensitive |
| Unknown or missing columns | The import fails. Prefix a header with `$` to ignore the column, mark a field `[ParseIgnore]`, or override `StrictColumns` |
| Empty file | A CSV file with no data rows fails the import instead of emptying the asset |
| Cell values | Numbers, enums, strings, dates, nullable values, lists and arrays (`1,2,3`), pairs (`1>2`), and dictionaries; custom parsers for the rest |
| Numbers and dates | Invariant culture: `1.5`, never `1,5` |
| CSV dialect | Quoted cells may contain commas, doubled quotes, and line breaks; CRLF and LF files both work |
| Order | Importers run one after another; `[ImportOrder(n)]` runs lower numbers first |

Three base classes cover the sheet shapes: `ConfigsImporter` (one row per config), `SingleConfigImporter` (the whole sheet is one config), and `SingleConfigSubListImporter` (Key/Value rows with nested lists). Implement `IDataImporter` directly to generate code or write anything else.

## Headless and agent workflow

Edit the CSV file, then import without opening the Editor:

```bash
Unity -batchmode -projectPath . -executeMethod GameLovers.DataImporter.Editor.DataImporterEditorUtils.RunFromCommandLine -dataImportReport Logs/data-import.json -quit -logFile import.log
```

| Argument | Effect |
| --- | --- |
| `-dataImporters ItemConfigsImporter,EnemyConfigsImporter` | Run only these importers, by type name; an unknown name fails the run and lists the available importers |
| `-dataImportPull` | Pull each upstream source into its CSV file before importing |
| `-dataImportSpreadsheetId <id>` | Pull every Google Sheet source from another spreadsheet, such as a duplicate |
| `-dataImportServiceAccountKey <path>` | Sign in with a service-account key to pull private Google Sheets |
| `-dataImportReport <path>` | Write a JSON report; an earlier report at that path is deleted first |

The run exits non-zero when any importer fails. The report lists every importer, and each error names the file and line:

```json
{
    "Failed": 1,
    "Results": [
        { "Importer": "ItemConfigsImporter", "CsvPath": "Assets/Data/Items.csv", "Source": "", "Pulled": false, "Rows": 0,
          "Error": "Assets/Data/Items.csv:3: Column 'Power': cannot read 'strong' as Single (Input string was not in a correct format.)" }
    ]
}
```

`DataImporterEditorUtils.ImportAll` and `PullAndImportAll` run every importer the same way, without arguments.

**Edit the right file.** A Pull overwrites the CSV file of an importer that has a `Source`. For those importers, change the spreadsheet; edit the CSV file directly only for importers without a source. In the window, the source badge on each row shows which kind it is.

## Editor tools and sample

`Tools/GameLovers/Data Importer/` holds **Import All**, **Pull And Import All**, and **Open Window**. The window lists every importer with a CSV, Excel, or Google Sheet badge and four buttons: **Open Source**, **Pull And Import**, **Import**, and **Select Asset**. After a run, each row shows its result: the number of imported rows, or the error with its file and line. A pull shows a progress bar and can be cancelled.

The **Data Import** sample has one importer per source, with the CSV files, an Excel workbook, the imported assets, and a JSON payload to compare against. Import it from Package Manager and read its README.

## Help and changes

Read [CHANGELOG.md](CHANGELOG.md), open an [issue](https://github.com/CoderGamester/Unity-GoogleSheet-Importer/issues), and contribute through the repository. The package is MIT licensed; see [LICENSE.md](LICENSE.md).
