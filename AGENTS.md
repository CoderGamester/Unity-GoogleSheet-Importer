# GameLovers.GoogleSheetImporter - AI Agent Guide

> **Companion files**: `CLAUDE.md` wraps this file for Claude Code — edit `AGENTS.md`, not `CLAUDE.md`. `README.md` is the user-facing entry point.

## 1. Package Overview
- **Package**: `com.gamelovers.googlesheetimporter`
- **Unity**: minimum 6000.0; compatibility reference streams 6000.0.x, 6000.3.x, and 6000.5.x. Reference editors: 6000.0.81f1, 6000.3.21f1, 6000.5.7f1 (primary). Do not call a stream validated without current matrix artifacts.
- **Dependencies** (see `package.json`)
  - `com.unity.nuget.newtonsoft-json` (2.0.0): fallback JSON deserialization path in `CsvParser.Parse`
  - `com.gamelovers.gamedata` (1.0.0): `IConfigsContainer<TConfig>` / `ISingleConfigContainer<TConfig>` (the interfaces a target `ScriptableObject` must implement) and `UnitySerializedDictionary<,>` (recognized specially by `CsvParser.DeserializeObject`)

Editor-time tool that downloads a published Google Sheet as CSV and deserializes each row into a `[Serializable]` struct, saved onto a `ScriptableObject`. All import logic runs in the Editor only — there is no runtime component.

## 2. Runtime Architecture (high level)

There are two independent halves: a general-purpose CSV parser (`Runtime/`, no Editor dependency) and the Google Sheet import pipeline built on top of it (`Editor/`, `UnityEditor`-only).

### CSV parsing (`GameLovers.GoogleSheetImporter`, `Runtime/CsvParser.cs`)
- `CsvParser.ConvertCsv(string csv)` → `List<Dictionary<string, string>>`. One dictionary per data row, keyed by header. A header prefixed with `$` (`IGNORE_COLUMN_CHAR`) is dropped entirely during this pass.
- `CsvParser.DeserializeTo<T>(Dictionary<string, string> data, ...)` reflects over `T`'s public fields (not properties) and populates each from the matching dictionary key. Fields marked `[ParseIgnore]` are skipped. A field the CSV doesn't have a matching key for is silently left at its default — there is no "missing column" error.
- `CsvParser.DeserializeObject` (the per-field dispatcher `DeserializeTo` calls) recognizes, in order: arrays, `IList`/generic list, a type whose base type closes over `UnitySerializedDictionary<,>`, plain `IDictionary`, then falls through to `Parse` for everything else (primitives, enums, `DateTime`, `TimeSpan`, nullable value types, and a Newtonsoft.Json fallback for anything else).
- `CsvParser.ArrayParse<T>` / `DictionaryParse<TKey,TValue>` split a single cell's text on `ArraySplitChars` (`, ( ) [ ] { }`) and `PairSplitChars` (`: < > = |`) respectively — a cell like `1,2,3` or `(1,2,3)` parses as an array; `a:1,b:2` or a flat `a,1,b,2` (even count, no pair-split char in the first element) parses as a dictionary.
- **Key/value struct auto-detection**: `Parse` special-cases a value-type struct whose only two public fields are named exactly `Key`/`Value` or `Value1`/`Value2` — it constructs the struct via `Activator.CreateInstance(type, key, value)` from a `PairSplitChars`-delimited cell. This is what makes `UnitySerializedDictionary<TKey,TValue>`'s entry struct importable from a single cell.
- **Sub-lists**: a config field whose CSV column header ends in `[]` (`SUB_LIST_SUFFIX`) is not itself deserialized — `GoogleSheetSingleConfigSubListImporter.Deserialize` (see below) detects the suffix and instead reads a run of subsequent rows (each marked with `Key == "#"`, `IGNORE_FIELD_CHAR`) as the sub-list's own rows, via `CsvParser.DeserializeSubList`.

### Import pipeline (`GameLoversEditor.GoogleSheetImporter`, `Editor/`)
- **`IGoogleSheetConfigsImporter`** (`Editor/GoogleSheetConfigsImporter.cs`) — the one interface every importer implements: `string GoogleSheetUrl { get; }` + `void Import(List<Dictionary<string,string>> data)`. `GoogleSheetToolImporter` discovers every non-abstract type implementing this interface via `AppDomain.CurrentDomain.GetAssemblies()` reflection — an importer needs no registration beyond existing as a concrete class.
- **`IScriptableObjectImporter`** — optional second interface (`Type ScriptableObjectType { get; }`) that lets the inspector's "Select Object" button locate/select the target asset. `GoogleSheetScriptableObjectImportContainer<TScriptableObject>` implements both.
- Three ready-made base classes, all abstract, all extending `GoogleSheetScriptableObjectImportContainer<TScriptableObject>`:
  - **`GoogleSheetConfigsImporter<TConfig, TScriptableObject>`** — one row per `TConfig` entry; `TScriptableObject` must implement `IConfigsContainer<TConfig>` (from `GameLovers.GameData`). Override `Deserialize(Dictionary<string,string> row)` only if you need custom per-row logic; the default calls `CsvParser.DeserializeTo<TConfig>`.
  - **`GoogleSheetSingleConfigImporter<TConfig, TScriptableObject>`** — the entire sheet becomes one `TConfig`; `TScriptableObject` must implement `ISingleConfigContainer<TConfig>`. You must override `Deserialize(List<Dictionary<string,string>> data)` yourself (no default).
  - **`GoogleSheetSingleConfigSubListImporter<TConfig, TScriptableObject>`** — same single-config shape, but each row is `Key`/`Value` pair matched against `TConfig`'s field names by reflection, with sub-list support (see above). Override `GetDeserializers()` to supply custom `Func<string, Type, object>[]` parsers.
- **`GoogleSheetImportOrderAttribute`** (`[GoogleSheetImportOrder(int)]`, class-level) — controls the order `GoogleSheetToolImporter.GetAllImporters()` runs importers in when "Import All Sheets" is pressed. Lower runs first; default (no attribute) is `int.MaxValue`. Importers with equal order are broken by type-name alphabetical sort.
- **`GoogleSheetImporter`** (`Editor/GoogleSheetImporter.cs`) — a `[CreateAssetMenu]` `ScriptableObject` with one field, `ReplaceSpreadsheetId`. Its own custom inspector, `GoogleSheetToolImporter`, is the actual UI: lists every discovered importer with per-row "Import" / "Select Object" buttons and a top-level "Import All Sheets" button. `ReplaceSpreadsheetId`, when set, substitutes the spreadsheet ID segment of every importer's `GoogleSheetUrl` for that run only (useful for testing against a duplicated sheet) — it does not mutate the importer's own `GoogleSheetUrl`.
- **Fetch mechanism**: `GoogleSheetToolImporter.ImportSheetAsync` rewrites the sheet's `.../edit#gid=...` URL to `.../export?format=csv&gid=...` and downloads it with `UnityWebRequest`. **The source sheet must be published/shared such that this CSV export URL is publicly reachable** — there is no auth step in this package.

## 3. Key Directories / Files
- **Runtime**: `Runtime/CsvParser.cs` (the whole runtime surface), `Runtime/ParseIgnoreAttribute.cs`
- **Editor**: `Editor/GoogleSheetConfigsImporter.cs` (interfaces + 3 base importer classes), `Editor/GoogleSheetImporter.cs` (the asset + menu item), `Editor/GoogleSheetToolImporter.cs` (the custom inspector that does the actual fetch/import work), `Editor/GoogleSheetImportOrderAttribute.cs`
- **Tests**: `Tests/Editor/CsvParserTest.cs` — the only test file, covering `CsvParser` deserialization paths (primitives, enums, ignored fields, key/value structs). There is no test coverage for the `Editor/` import pipeline itself (it requires a live network fetch and `AssetDatabase` state). Before reading, editing, or creating any file in `Tests/`, you **MUST** read [`Tests/AGENTS.md`](Tests/AGENTS.md) first.
- **Samples**: `Samples~/Importers Example/` — `GameIdsImporter.cs` (`IGoogleSheetConfigsImporter` directly, generates an enum + lookup extension methods as a `.cs` file via `AssetDatabase`/`File.WriteAllText` — the only importer that writes code instead of a `ScriptableObject`), `GameConfigsImporter.cs` (`GoogleSheetSingleConfigImporter<GameConfig, GameConfigs>`), `DataConfigsImporter.cs` (`GoogleSheetConfigsImporter<DataConfig, DataConfigs>`). These reference `Configs.GameConfig` / `Configs.DataConfigs` / etc. types that are **not included in the sample** — it demonstrates the importer classes' shape, not a runnable end-to-end example.

## 4. Important Behaviors / Gotchas
- **No compile-time schema validation**: a CSV column with no matching field, or a field with no matching column, is silently ignored / left default. Typos in either the sheet header or the target struct's field name fail silently, not loudly.
- **Reflection targets public fields, not properties**: `CsvParser.DeserializeTo` uses `type.GetFields()`. A config struct with `{ get; set; }` auto-properties will not receive any data through this path — use public fields.
- **`GoogleSheetSingleConfigSubListImporter` field matching is by exact name**: `type.GetField(fieldName)` — a `[]`-suffixed header for a sub-list has the suffix stripped before the lookup (`"Items[]"` → looks up a field literally named `Items`).
- **`DictionaryParse` throws `IndexOutOfRangeException`** if a plain (non-paired) dictionary cell has an odd number of comma-separated values — there must be an even count when no `PairSplitChars` character is present in the data.
- **The Google Sheet CSV export URL must be publicly reachable** — "Anyone with the link can view" sharing, or the underlying `UnityWebRequest` fails with no sheet-specific error message (just the HTTP failure).
- **`ReplaceSpreadsheetId` is per-run, not persisted per-importer**: it patches every importer's URL for that one "Import All Sheets" / "Import" click; it is not saved onto the config assets.

## 5. Coding Standards (Unity 6 / C# 9.0)
- **C#**: C# 9.0 syntax; explicit namespaces; no global usings.
- **Assemblies**: `Runtime/GameLovers.GoogleSheetImporter.asmdef` has no Editor/UnityEditor reference — keep `CsvParser` and `ParseIgnoreAttribute` runtime-safe. All `UnityEditor` usage (asset creation, custom inspectors, `UnityWebRequest` fetch, menu items) belongs in `Editor/GameLovers.GoogleSheetImporter.Editor.asmdef`.
- **Namespace split**: runtime types are `GameLovers.GoogleSheetImporter`; editor types are `GameLoversEditor.GoogleSheetImporter` (note the `GameLoversEditor` prefix, not `GameLovers.GoogleSheetImporter.Editor` — matches the sibling `com.gamelovers.gamedata` / `com.gamelovers.services` convention).

## 6. External Package Sources (for API lookups)
- Newtonsoft.Json: `Library/PackageCache/com.unity.nuget.newtonsoft-json/`
- GameData (`IConfigsContainer<T>`, `ISingleConfigContainer<T>`, `UnitySerializedDictionary<,>`): `Packages/com.gamelovers.gamedata/`

## 7. Common change workflows
- **Add a new importer**: implement `IGoogleSheetConfigsImporter` directly, or extend one of the three abstract base classes in `Editor/GoogleSheetConfigsImporter.cs` depending on whether the sheet is one-row-per-entry (`GoogleSheetConfigsImporter<,>`) or one-sheet-per-config (`GoogleSheetSingleConfigImporter<,>` / `...SubListImporter<,>`). No registration step — any concrete class implementing the interface is picked up automatically by reflection.
- **Change CSV parsing behavior**: `Runtime/CsvParser.cs` is the single file to edit; add/adjust `Tests/Editor/CsvParserTest.cs` coverage alongside.
- **Add a custom field type**: pass a `Func<string, Type, object>[]` of custom deserializers into `DeserializeTo`/`DeserializeObject`/`Parse` — do not special-case new types inside `CsvParser` unless the type is broadly useful (existing special cases: arrays, lists, dictionaries, `UnitySerializedDictionary`, key/value structs, `DateTime`, `TimeSpan`, nullable value types).

## 8. Update Policy
Update this file when:
- `CsvParser`'s type-dispatch order or special-casing changes (arrays / lists / dictionaries / `UnitySerializedDictionary` / key-value structs / fallback `Parse`)
- The importer interface/base-class shape changes (`IGoogleSheetConfigsImporter`, `IScriptableObjectImporter`, the three `GoogleSheet*Importer<,>` base classes)
- The fetch mechanism changes (URL rewriting, auth requirements, `GoogleSheetToolImporter`'s discovery/ordering logic)
- Dependencies in `package.json` change (cross-check this file and `README.md` for stale references)
