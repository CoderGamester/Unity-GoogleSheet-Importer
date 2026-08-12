# GameLovers Google Sheet Importer

Editor tooling that imports a published Google Sheet as CSV into ScriptableObject configuration assets.

[![Unity](https://img.shields.io/badge/Unity-6000.0%20%7C%206000.3%20%7C%206000.5-blue.svg)](https://unity.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.md)

## When to use it

Use this package when designers own public spreadsheet data and the game consumes ScriptableObject-backed configuration. Import orchestration is Editor-only. `CsvParser` is compiled into a runtime assembly, so this is not a zero-runtime-dependency package. It is pipeline-neutral.

## Unity compatibility

| Item | Current policy |
| --- | --- |
| Minimum Unity version | `6000.0` |
| Reference streams | `6000.0.x`, `6000.3.x`, `6000.5.x` |
| Reference editors | `6000.0.81f1`, `6000.3.21f1`, `6000.5.7f1` (primary) |
| Render pipeline | Pipeline-neutral |
| Validation status | Compatibility target; check the repository matrix for fresh results. |

## Install

Git dependencies are not resolved transitively by Unity. Install GameData and this package together:

```json
{
  "dependencies": {
    "com.gamelovers.gamedata": "https://github.com/CoderGamester/Unity-GameData.git#1.0.3",
    "com.gamelovers.googlesheetimporter": "https://github.com/CoderGamester/Unity-GoogleSheet-Importer.git#0.7.3"
  }
}
```

## First success

Put configuration types in a runtime assembly, then put the importer in an `Editor/` folder or Editor-only asmdef.

```csharp
// Runtime assembly
using System;
using System.Collections.Generic;
using GameLovers.GameData;
using UnityEngine;

[Serializable]
public struct ItemConfig { public int Id; public string Name; public int Value; }

[CreateAssetMenu(menuName = "Configs/Item Configs")]
public sealed class ItemConfigs : ScriptableObject, IConfigsContainer<ItemConfig>
{
    [SerializeField] private List<ItemConfig> configs = new();
    public List<ItemConfig> Configs { get => configs; set => configs = value; }
}
```

```csharp
// Editor-only assembly
using GameLoversEditor.GoogleSheetImporter;

public sealed class ItemConfigsImporter : GoogleSheetConfigsImporter<ItemConfig, ItemConfigs>
{
    public override string GoogleSheetUrl =>
        "https://docs.google.com/spreadsheets/d/<sheet-id>/edit#gid=<gid>";
}
```

Make the sheet publicly readable, use `Id`, `Name`, and `Value` as the header row, then use `Tools/GoogleSheet Importer/...` to import. Keep the importer and its target asset unique: the importer selects the first matching asset or creates one under `Assets/`.

## Data-shape rules

| Rule | Behavior |
| --- | --- |
| Fields | Match case-sensitive public field names |
| Missing or extra columns | Ignored rather than validated as a schema error |
| Parsing | Primitive values, enums, arrays, dictionaries, nullable values, and custom parsers are supported |
| CSV dialect | Do not rely on multiline/RFC-4180 edge cases; validate the exact sheet data you ship |
| Security | A public Sheet URL is public data; never place secrets in it |
| Ordering | `GoogleSheetImportOrderAttribute` orders startup, not async completion; do not make one sheet depend on another completing unless orchestration is changed. |

Use a custom parser for shapes that do not have an unambiguous scalar representation. Duplicate headers and malformed dictionary cells should be treated as import errors to fix in the sheet, not silently accepted data.

## Sample and support

The **Importers Example** sample is the end-to-end reference: import it through Package Manager, read its README, and confirm its generated asset before adopting its shape.

See [CHANGELOG.md](CHANGELOG.md), file an [issue](https://github.com/CoderGamester/Unity-GoogleSheet-Importer/issues), and consult the package source for editor extension points.
