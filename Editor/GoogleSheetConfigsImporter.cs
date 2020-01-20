using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using GameLovers.ConfigsContainer;
using GameLovers.GoogleSheetImporter;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.GoogleSheetImporter
{
	/// <inheritdoc />
	/// <remarks>
	/// Generic implementation of an importer to load <typeparamref name="TConfig"/> types into <typeparamref name="TScriptableObject"/>
	/// types
	/// </remarks>
	public abstract class GoogleSheetConfigsImporter<TConfig, TScriptableObject> : IGoogleSheetImporter
		where TConfig : struct
		where TScriptableObject : ScriptableObject, IConfigsContainer<TConfig>
	{
		/// <inheritdoc />
		public abstract string GoogleSheetUrl { get; }
		
		/// <inheritdoc />
		public void Import(List<Dictionary<string, string>> data)
		{
			var type = typeof(TScriptableObject);
			var assets = AssetDatabase.FindAssets($"t:{type.Name}");
			var scriptableObject = assets.Length > 0 ? 
				AssetDatabase.LoadAssetAtPath<TScriptableObject>(AssetDatabase.GUIDToAssetPath(assets[0])) :
				ScriptableObject.CreateInstance<TScriptableObject>();

			if (assets.Length == 0)
			{
				AssetDatabase.CreateAsset(scriptableObject, $"Assets/{type.Name}.asset");
			}

			scriptableObject.Configs.Clear();
			
			foreach (var pair in data)
			{
				scriptableObject.Configs.Add(Deserialize(pair));
			}
			
			EditorUtility.SetDirty(scriptableObject);
		}

		/// <summary>
		/// Override this method to have your own deserialization of the given <paramref name="data"/>
		/// </summary>
		protected virtual TConfig Deserialize(Dictionary<string, string> data)
		{
			return CsvParser.DeserializeTo<TConfig>(data);
		}
	}
}