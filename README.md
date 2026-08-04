# GameLovers GoogleSheet Importer

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)

Editor-time tool that imports a published Google Sheet as CSV directly into Unity `ScriptableObject` config data — no runtime dependency, no build-time cost.

## Why Use This Package?

Game data (item tables, level configs, localization) commonly lives in a spreadsheet that designers edit directly. This package turns "designer edits a Google Sheet" into "press Import" — every row becomes a field-matched entry on a `ScriptableObject`, using reflection so no per-column boilerplate is needed for straightforward config shapes.

### Key Features
- **Zero-boilerplate row mapping** — a CSV row maps onto a `[Serializable]` struct's public fields by column-header name.
- **Three importer shapes** out of the box: one-row-per-entry, whole-sheet-as-one-config, and whole-sheet-as-one-config-with-sub-lists.
- **Rich cell parsing** — arrays (`1,2,3` / `(1,2,3)`), dictionaries (`a:1,b:2`), enums, `DateTime`/`TimeSpan`, nullable value types, and key/value structs (including `UnitySerializedDictionary` entries from `com.gamelovers.gamedata`), all via a single `CsvParser`.
- **Custom deserializers** — pass your own `Func<string, Type, object>` parsers for anything the built-in dispatch doesn't cover.
- **No registration step** — any class implementing `IGoogleSheetConfigsImporter` is discovered automatically via reflection.

## System Requirements

- **[Unity](https://unity.com/download)** (v6000.0+)
- **[Newtonsoft Json.NET for Unity](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@latest)** (v2.0.0+) — fallback deserialization path
- **[GameLovers GameData](https://github.com/CoderGamester/Unity-GameData)** (v1.0.0+) — `IConfigsContainer<T>` / `ISingleConfigContainer<T>` container interfaces

Dependencies are automatically resolved when installing via Unity Package Manager.

## Installation

### Via Unity Package Manager (Recommended)

1. Open Unity Package Manager (`Window` → `Package Manager`)
2. Click `+` → `Add package from git URL`
3. Enter: `https://github.com/CoderGamester/Unity-GoogleSheet-Importer.git`

### Via manifest.json

```json
{
  "dependencies": {
    "com.gamelovers.googlesheetimporter": "https://github.com/CoderGamester/Unity-GoogleSheet-Importer.git"
  }
}
```

## Key Components

| Type | Purpose |
|---|---|
| `CsvParser` | Static CSV-to-object deserialization — the whole runtime surface |
| `IGoogleSheetConfigsImporter` | The one interface every importer implements (`GoogleSheetUrl` + `Import`) |
| `GoogleSheetConfigsImporter<TConfig, TScriptableObject>` | One CSV row → one `TConfig` entry, many entries per sheet |
| `GoogleSheetSingleConfigImporter<TConfig, TScriptableObject>` | Whole sheet → one `TConfig` |
| `GoogleSheetSingleConfigSubListImporter<TConfig, TScriptableObject>` | Whole sheet → one `TConfig`, with `Key`/`Value` row matching and sub-list support |
| `GoogleSheetImportOrderAttribute` | `[GoogleSheetImportOrder(int)]` — controls run order when importing all sheets at once |
| `ParseIgnoreAttribute` | `[ParseIgnore]` on a field to exclude it from CSV deserialization |

## Quick Start

### 1. Define your config struct and a container asset

```csharp
using System;
using GameLovers.GameData;
using UnityEngine;

[Serializable]
public struct ItemConfig
{
    public int Id;
    public string Name;
    public int Value;
}

[CreateAssetMenu(menuName = "Configs/ItemConfigs")]
public class ItemConfigs : ScriptableObject, IConfigsContainer<ItemConfig>
{
    [SerializeField] private List<ItemConfig> _configs;
    public List<ItemConfig> Configs { get => _configs; set => _configs = value; }
}
```

### 2. Write an importer

```csharp
using GameLoversEditor.GoogleSheetImporter;

public class ItemConfigsImporter : GoogleSheetConfigsImporter<ItemConfig, ItemConfigs>
{
    public override string GoogleSheetUrl =>
        "https://docs.google.com/spreadsheets/d/<your-sheet-id>/edit#gid=<your-gid>";
}
```

The sheet's header row must match `ItemConfig`'s public field names (`Id`, `Name`, `Value`) — no other wiring is required. `ItemConfigsImporter` is discovered automatically; no registration call anywhere.

### 3. Import

- `Tools > GoogleSheet Importer > Select GoogleSheetImporter.asset` creates/selects the importer tool asset.
- Its custom inspector lists every discovered importer with an **Import** button, plus a top-level **Import All Sheets** button.
- Or skip the asset entirely: `Tools > GoogleSheet Importer > Import Google Sheet Data` imports everything in one click.

The target sheet must be shared as "Anyone with the link can view" — the importer fetches its CSV export URL directly, with no auth step.

## Samples

### Importing Samples

In the Unity Package Manager window, select **GoogleSheet Importer**, open the **Samples** tab, and import **Importers Example**.

### Available Samples

| Sample | Demonstrates |
|---|---|
| Importers Example | All three importer base classes in use — `DataConfigsImporter` (`GoogleSheetConfigsImporter<,>`), `GameConfigsImporter` (`GoogleSheetSingleConfigImporter<,>`), and `GameIdsImporter` (`IGoogleSheetConfigsImporter` directly, generating a C# enum + lookup file instead of a `ScriptableObject`). References `Configs.*` types that are illustrative, not included — the sample shows the importer shape, not a runnable end-to-end pipeline. |

## Related docs

| Document | Purpose |
|---|---|
| [AGENTS.md](AGENTS.md) | Contributor/agent guide — architecture, gotchas, workflows |
| [CHANGELOG.md](CHANGELOG.md) | Version history |

## Contributing

Contributions are welcome! See [AGENTS.md](AGENTS.md) for architecture details, coding standards, and common workflows.

## Support

- **Issues**: [Report bugs or request features](https://github.com/CoderGamester/Unity-GoogleSheet-Importer/issues)

## License

MIT — see [LICENSE.md](LICENSE.md).
