# Migrating from GoogleSheet Importer to DataImporter

The package `com.gamelovers.googlesheetimporter` is now `com.gamelovers.dataimporter`. The rename is breaking: update the manifest entry, the assembly references, the namespaces, and the importer classes.

## Manifest and assemblies

| Before | After |
| --- | --- |
| `com.gamelovers.googlesheetimporter` | `com.gamelovers.dataimporter` |
| Assembly `GameLovers.GoogleSheetImporter` | `GameLovers.DataImporter` (only `ParseIgnoreAttribute`) |
| Assembly `GameLovers.GoogleSheetImporter.Editor` | `GameLovers.DataImporter.Editor` |
| Namespace `GameLovers.GoogleSheetImporter` | `GameLovers.DataImporter` |
| Namespace `GameLoversEditor.GoogleSheetImporter` | `GameLovers.DataImporter.Editor` |

`CsvParser` moved to the Editor assembly. Code that called it from a player assembly must move to an Editor assembly; players load the imported asset instead of parsing text.

## Types

| Before | After |
| --- | --- |
| `IGoogleSheetConfigsImporter` | `IDataImporter` |
| `GoogleSheetScriptableObjectImportContainer<T>` | `ScriptableObjectImporter<T>` |
| `GoogleSheetConfigsImporter<TConfig, T>` | `ConfigsImporter<TConfig, T>` |
| `GoogleSheetSingleConfigImporter<TConfig, T>` | `SingleConfigImporter<TConfig, T>` |
| `GoogleSheetSingleConfigSubListImporter<TConfig, T>` | `SingleConfigSubListImporter<TConfig, T>` |
| `GoogleSheetImportOrderAttribute` | `ImportOrderAttribute` |
| `GoogleSheetImporter` asset and its inspector | The window at `Tools/GameLovers/Data Importer/Open Window` |

## Importers

Each importer now names the CSV file it reads, and the Google Sheet becomes its upstream:

```csharp
// Before
public override string GoogleSheetUrl => "https://docs.google.com/spreadsheets/d/<sheet-id>/edit#gid=<gid>";

// After
public override string CsvPath => "Assets/Data/Items.csv";
public override string Source => "https://docs.google.com/spreadsheets/d/<sheet-id>/edit#gid=<gid>";
```

Run `Tools/GameLovers/Data Importer/Pull And Import All` once to create the CSV files, and commit them.

## Behavior changes

- **Strict columns.** A row-list import fails on a column with no matching public field and on a field with no column. Prefix a header with `$` to ignore the column, mark a field `[ParseIgnore]` to leave it out of the sheet, or override `StrictColumns` to return false.
- **Unknown keys.** A Key/Value sub-list import fails on a Key with no matching field. Prefix the key with `$` to ignore the row.
- **Empty sheets.** A CSV file with no data rows fails the import instead of writing an empty list.
- **Menus.** `Tools/GoogleSheet Importer/...` became `Tools/GameLovers/Data Importer/...`.
- **Old tool asset.** Delete any `GoogleSheetImporter.asset` left in the project; its script no longer exists. The spreadsheet-id override is now a field in the window.
- **New assets** are created next to the CSV file instead of in `Assets/`. An existing asset is updated where it is.
