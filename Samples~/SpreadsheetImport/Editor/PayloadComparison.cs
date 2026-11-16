using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameLovers.SpreadsheetImporter.Samples.SpreadsheetImport.Editor
{
	internal static class PayloadComparison
	{
		// Loads the JSON payload the way a player would and compares it with the asset, so the two outputs of
		// the same import can be checked against each other.
		[MenuItem("Tools/GameLovers/Spreadsheet Importer Samples/Compare Asset And JSON Payload")]
		private static void Compare()
		{
			var assetPath = SampleData.Path(nameof(ItemConfigs) + ".asset");
			var jsonPath = SampleData.Path(nameof(ItemConfigs) + ".json");
			var asset = AssetDatabase.LoadAssetAtPath<ItemConfigs>(assetPath);
			var json = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);

			if (asset == null || json == null)
			{
				EditorUtility.DisplayDialog("Spreadsheet Importer sample", "Run Tools > GameLovers > Spreadsheet Importer > Import All first.", "OK");
				return;
			}

			var fromJson = ScriptableObject.CreateInstance<ItemConfigs>();

			JsonUtility.FromJsonOverwrite(json.text, fromJson);

			var identical = JsonUtility.ToJson(fromJson) == JsonUtility.ToJson(asset);
			var message = $"Asset: {asset.Configs.Count} rows, {new FileInfo(assetPath).Length} bytes on disk\n" +
						  $"JSON payload: {fromJson.Configs.Count} rows, {new FileInfo(jsonPath).Length} bytes on disk\n" +
						  $"Same content: {(identical ? "yes" : "NO")}";

			Object.DestroyImmediate(fromJson);
			EditorUtility.DisplayDialog("Spreadsheet Importer sample", message, "OK");
		}
	}
}
