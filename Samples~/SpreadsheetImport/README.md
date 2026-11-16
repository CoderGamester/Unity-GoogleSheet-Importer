# Spreadsheet Import

> **Unity compatibility:** Minimum Unity version `6000.0`; reference streams are `6000.0.x`, `6000.3.x`, and `6000.5.x`. The package is pipeline-neutral. See the [package compatibility matrix](../../README.md#unity-compatibility).

One **Editor-only** importer per source. Each reads a CSV file in `Data/` and writes a ScriptableObject asset next to it.

| Importer | Source | Sheet shape | Output |
| --- | --- | --- | --- |
| `ItemConfigsImporter` | `Data/Items.csv`, edited directly | One row per item | `ItemConfigs.asset` and `ItemConfigs.json` |
| `EnemyConfigsImporter` | The `Enemies` worksheet of `Data/Enemies.xlsx` | One row per enemy | `EnemyConfigs.asset` |
| `DataConfigsImporter` | A Google Sheet tab | One row per entry, with list and pair cells | `DataConfigs.asset` |
| `GameConfigsImporter` | A Google Sheet tab | `Key` / `Value` rows | `GameConfigs.asset` |

The sample ships already imported, so the assets and CSV files are there to inspect before you run anything.

## Run it

1. Import the sample from Package Manager.
2. Open `Tools/GameLovers/Spreadsheet Importer/Open Window`. The four importers are listed with their CSV file and source.
3. Change a value in `Data/Items.csv` and press **Import** on `ItemConfigsImporter`. Select the asset and check the value.
4. Change a cell in `Data/Enemies.xlsx`, save it, and press **Pull** then **Import** on `EnemyConfigsImporter`. `Data/Enemies.csv` shows the change as a diff.
5. Press **Pull And Import All** to refresh the two Google Sheet tabs as well. This needs a network connection.

## Try a failure

Rename the `Power` header in `Data/Items.csv`, or type a word into a `Power` cell, and import. The Console names the file and line, and the asset keeps its previous content.

## Compare the two outputs

`ItemConfigsImporter` turns on `WriteJsonPayload`, so each import also writes `Data/ItemConfigs.json` with the same content as the asset. Run `Tools/GameLovers/Spreadsheet Importer Samples/Compare Asset And JSON Payload` to load the payload the way a player would and check it against the asset.

## Notes

- The importers live under `Editor/` with their own assembly; the config types live in the sample's runtime assembly so game code can use them.
- A project writes its paths as literals such as `"Assets/Data/Items.csv"`. This sample resolves them through `SampleData` because Package Manager imports it under a versioned folder.
- The Google Sheet is a public fixture, not a place for secrets.
- To remove the sample, delete its folder under `Assets/Samples/`.
