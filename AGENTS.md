# GameLovers GoogleSheetImporter — Agent Guide

This guide adds package-specific rules to the host repository guide. Consumer usage belongs in `README.md`.

## Scope

- Package: `com.gamelovers.googlesheetimporter`; minimum Unity version and dependencies are authoritative in `package.json`.
- Runtime contains the general CSV parser. The Google Sheet download/import pipeline is Editor-only.
- Runtime namespace: `GameLovers.GoogleSheetImporter`. Editor namespace: `GameLoversEditor.GoogleSheetImporter`.
- This package is render-pipeline-neutral.

## Assembly boundaries

- `Runtime/GameLovers.GoogleSheetImporter.asmdef` must remain free of `UnityEditor` references.
- Asset creation, importer discovery, menus, inspectors, and web requests belong under `Editor/`.
- The runtime parser depends on GameData types exposed in its API. Preserve direct asmdef/package dependencies; do not rely on transitive resolution.

## Parser invariants

- `CsvParser.DeserializeTo<T>` populates public fields, not properties. Missing columns and unmatched headers are intentionally ignored and leave defaults.
- Headers beginning with `$` are ignored. Headers ending in `[]` drive the single-config sub-list importer; matching is by exact field name after removing the suffix.
- Preserve `DeserializeObject` dispatch precedence: arrays, lists, `UnitySerializedDictionary`, plain dictionaries, then scalar/JSON parsing.
- Array and dictionary cell parsing uses the package delimiter sets. A flat dictionary representation must contain an even number of values.
- Key/value struct detection applies only to value types with exactly two public fields named `Key`/`Value` or `Value1`/`Value2`.
- Add parser coverage in `Tests/Editor/CsvParserTest.cs` whenever dispatch, delimiter, fallback, or field-matching behavior changes.

## Importer invariants

- Concrete `IGoogleSheetConfigsImporter` implementations are discovered by reflection; no registration table is required.
- Use `GoogleSheetConfigsImporter<TConfig,TScriptableObject>` for one row per config entry, `GoogleSheetSingleConfigImporter<TConfig,TScriptableObject>` for one config built from the whole sheet, and `GoogleSheetSingleConfigSubListImporter<TConfig,TScriptableObject>` for Key/Value rows with nested row groups. Do not create a fourth base shape for a case one of these supports.
- The row-list base defaults to `CsvParser.DeserializeTo<TConfig>`. The single-config base requires the importer to implement whole-sheet deserialization. The sub-list base accepts custom `Func<string,Type,object>` deserializers through `GetDeserializers()`.
- `GoogleSheetImportOrderAttribute` sorts importers by ascending order and then type name.
- Import URLs are rewritten to public CSV export URLs. The package has no authentication flow, so the sheet must be publicly reachable.
- `ReplaceSpreadsheetId` is a per-run URL override and must not mutate importer definitions.
- Use the existing row-list, single-config, or single-config-with-sublists base class before creating another importer abstraction.
- `IScriptableObjectImporter` is the optional discovery surface used by the inspector's Select Object action. Keep it separate from the basic importer contract so code-generating importers are not forced to own an asset type.

## Tests and verification

- Before changing anything under `Tests/`, read `Tests/AGENTS.md`.
- The current automated suite covers the runtime parser, not the network and `AssetDatabase` import pipeline. Editor pipeline changes require a focused manual import using a reachable sheet and inspection of the resulting asset/code.
- Prefer local Newtonsoft.Json and GameData sources under `Library/PackageCache/` and `Packages/com.gamelovers.gamedata/`.
- Update `README.md` and samples when importer setup or public parsing behavior changes; update this guide only for durable package invariants.
