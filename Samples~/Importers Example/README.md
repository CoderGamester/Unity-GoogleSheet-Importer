# Importers Example

> **Unity compatibility:** Minimum Unity version `6000.0`; reference streams are `6000.0.x`, `6000.3.x`, and `6000.5.x`. The importer package is pipeline-neutral. See the [package compatibility matrix](../../README.md#unity-compatibility).

This sample contains two ScriptableObject target shapes and three **Editor-only** importers:

| Importer | Sheet shape | Output |
| --- | --- | --- |
| `DataConfigsImporter` | One row per `DataConfig` | A `DataConfigs` asset |
| `GameConfigsImporter` | `Key` / `Value` rows | A `GameConfigs` asset |
| `GameIdsImporter` | IDs and groups | Generated `Assets/GameId.cs` in the consuming project |

## Run it

1. Import the sample from Package Manager.
2. Create `DataConfigs` and `GameConfigs` assets with the sample Create Asset menus.
3. Use `Tools/GoogleSheet Importer/Import Google Sheet Data`.
4. Inspect the updated assets. If you run `GameIdsImporter`, review the generated `Assets/GameId.cs` before committing it; it overwrites that target path.

The importers intentionally live under `Editor/`. Keep application configuration types outside `Editor/` so they remain available to runtime code. The published Sheets are public fixtures, not a place for secrets. Import order controls discovery order only; do not make one importer depend on another import's asynchronous completion.
