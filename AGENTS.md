# GameLovers DataImporter — Agent Guide

This guide adds package-specific rules to the host repository guide. Consumer usage belongs in `README.md`.

## Scope

- Package: `com.gamelovers.dataimporter`; minimum Unity version and dependencies are authoritative in `package.json`.
- The pipeline is Editor-only: an upstream (Google Sheet or Excel worksheet) is pulled into a CSV file in the project, and the CSV file is imported into a ScriptableObject asset. Nothing parses sheet data in a player.
- Runtime assembly `GameLovers.DataImporter` holds only `ParseIgnoreAttribute`, because config structs in player assemblies carry it. Editor namespace: `GameLovers.DataImporter.Editor`.
- This package is render-pipeline-neutral.

## Assembly boundaries

- `Runtime/GameLovers.DataImporter.asmdef` must stay free of `UnityEditor`, GameData, and Newtonsoft references. Do not move parsing, reading, or writing code into it.
- The CSV parser, the Excel reader, importer discovery, menus, the window, asset writes, and web requests belong under `Editor/`.
- The Editor assembly depends on GameData types exposed in its API. Preserve direct asmdef/package dependencies; do not rely on transitive resolution.
- `XlsxReader` uses only `System.IO.Compression` and `System.Xml.Linq`. Do not add a third-party spreadsheet library.

## Pipeline invariants

- The CSV file is the single input of an import. `Import` never reads the upstream; only `Pull` does, and `Pull` only rewrites the CSV file. Keep the two steps separate so an import is reproducible offline and in CI.
- Pulled CSV files are written with LF line endings and a final newline, so a pull that changes no data produces no diff.
- The ScriptableObject asset is the default output and is always written. The JSON payload is opt-in per importer through `WriteJsonPayload` and is `JsonUtility.ToJson` of the same asset, so both outputs always hold the same content. Do not add a second serializer or a runtime reader type for it.
- `Run` executes importers sequentially, records one `ImportResult` per importer, logs each failure as `path:line: message`, and never throws or saves. `ImportAll` and `RunFromCommandLine` run it over `ImportOrderAttribute` order, then type name, save once, and throw when any importer failed. That exception is what makes a batchmode `-executeMethod` run exit non-zero; do not swallow it.
- The public field names of `ImportReport` and `ImportResult` are the JSON report format agents parse. Renaming one or turning it into a property is a breaking change.
- `RunFromCommandLine` deletes an existing report before running, so a report on disk always belongs to the last run. An unknown `-dataImporters` name fails before any importer runs.
- Progress bars are skipped in batchmode. Cancelling a download aborts the request and fails only that importer.
- A CSV file without data rows is an import error, so an emptied sheet or worksheet cannot silently empty an asset.
- Report a row failure by throwing `DataImportException` with the zero-based data row. Only `DataImporterEditorUtils.Import` turns the row into a file line, using the line map `CsvParser.ConvertCsv` fills.
- Google Sheet URLs are rewritten to public CSV export URLs from the spreadsheet id and `gid` wherever they appear in the URL, including the account-scoped `/u/<n>/` form. A published-to-web URL is rejected. An HTML response is an error, not data.
- A private sheet is pulled with a service-account key: `GoogleServiceAccount` signs an RS256 assertion for the `drive.readonly` scope, `DataImporterEditorUtils` trades it for an access token once per pull and sends it to the same CSV export URL. Without a key, downloads stay anonymous. The key path comes from `-dataImportServiceAccountKey`, then the window's EditorPrefs setting, then `GOOGLE_APPLICATION_CREDENTIALS`.
- Never log, report, or store the key's content, the assertion, or the token. Only the key's path is stored, in EditorPrefs. A key inside the project folder gets a warning, not a refusal, because CI may write the key into its workspace.
- The Editor's Mono runtime cannot import PKCS#8 keys (`RSA.ImportPkcs8PrivateKey` throws `PlatformNotSupportedException`), so `GoogleServiceAccount.ReadPrivateKey` reads the DER itself. Keep it dependency-free.
- The spreadsheet-id override is a per-pull argument and must not mutate importer definitions.

## Parser invariants

- `CsvParser.ConvertCsv` is quote-aware: a quoted cell may contain commas, doubled quotes, and line breaks, and CRLF, LF, and CR files read the same. A quote opens a quoted cell only at the start of a cell; anywhere else it is text. Rows whose cells are all blank are skipped. Cell values and headers are trimmed.
- Headers that are empty or begin with `$` are ignored. A duplicate header is an error.
- All scalar parsing uses the invariant culture. Never parse or format a number, date, or time with the current culture.
- `CsvParser.DeserializeTo<T>` populates public fields, not properties, and stays lenient: missing columns and unmatched headers leave defaults. Strictness lives in `CsvParser.ValidateColumns`, which the row-list importer calls unless `StrictColumns` is overridden.
- Headers ending in `[]` drive the single-config sub-list importer; matching is by exact field name after removing the suffix. An unknown Key is an error unless the key starts with `$`.
- Preserve `DeserializeObject` dispatch precedence: arrays, lists, `UnitySerializedDictionary`, plain dictionaries, then scalar/JSON parsing.
- Array and dictionary cell parsing uses the package delimiter sets. A flat dictionary representation must contain an even number of values.
- Key/value struct detection applies only to generic value types with exactly two public fields named `Key`/`Value` or `Value1`/`Value2`.
- `XlsxReader` returns values only: numbers and dates as invariant text, time-only and elapsed-time cells as a duration, booleans as `TRUE`/`FALSE`, formulas as their cached value. Whether a number is a date comes from the cell's number format, never from its size. A formula without a cached value and an error cell are errors.

## Importer invariants

- Concrete `IDataImporter` implementations are discovered through `TypeCache`; no registration table is required.
- An importer writes the project's first asset of exactly its ScriptableObject type, found through `DataImporterEditorUtils.FindAsset`, and creates one next to the CSV file when there is none.
- Use `ConfigsImporter<TConfig,TScriptableObject>` for one row per config entry, `SingleConfigImporter<TConfig,TScriptableObject>` for one config built from the whole sheet, and `SingleConfigSubListImporter<TConfig,TScriptableObject>` for Key/Value rows with nested row groups. Do not create a fourth base shape for a case one of these supports.
- `IScriptableObjectImporter` is the optional discovery surface used by the window's Select Asset action. Keep it separate from `IDataImporter` so code-generating importers are not forced to own an asset type.
- A new source kind is a branch in `DataImporterEditorUtils.Pull` that produces CSV text. Do not add a source interface until a consumer needs a source the package cannot own.

## Tests and verification

- Before changing anything under `Tests/`, read `Tests/AGENTS.md`.
- The automated suite covers the parser, the Excel reader, URL rewriting, `Run` results, and command-line arguments. Asset writes, the window, and downloads are not covered: changes there require a real `Import All` and `Pull And Import All` on the imported sample and inspection of the resulting assets.
- Changes to the window require a visual check in the Editor.
- Update `README.md`, `MIGRATION.md`, and the sample when importer setup or public parsing behavior changes; update this guide only for durable package invariants.
