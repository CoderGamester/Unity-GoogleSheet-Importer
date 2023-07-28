using System.Collections.Generic;
using GameLovers.GoogleSheetImporter;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.GoogleSheetImporter
{
	/// <inheritdoc />
	/// <remarks>
	/// Generic implementation of an importer to load multiple <typeparamref name="TConfig"/> configs into one
	/// <typeparamref name="TScriptableObject"/>.
	/// It will import 1 row per data entry. This means each row will represent 1 <typeparamref name="TConfig"/> entry
	/// and import multiple <typeparamref name="TConfig"/>
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
			var configs = new List<TConfig>();
			var scriptableObject = assets.Length > 0 ? 
				AssetDatabase.LoadAssetAtPath<TScriptableObject>(AssetDatabase.GUIDToAssetPath(assets[0])) :
				ScriptableObject.CreateInstance<TScriptableObject>();

			if (assets.Length == 0)
			{
				AssetDatabase.CreateAsset(scriptableObject, $"Assets/{type.Name}.asset");
			}
			
			foreach (var row in data)
			{
				configs.Add(Deserialize(row));
			}

			scriptableObject.Configs = configs;
			
			EditorUtility.SetDirty(scriptableObject);
			OnImportComplete(scriptableObject);
		}

		/// <summary>
		/// Override this method to have your own deserialization of the given <paramref name="data"/>
		/// </summary>
		protected virtual TConfig Deserialize(Dictionary<string, string> data)
		{
			return CsvParser.DeserializeTo<TConfig>(data);
		}
		
		/// <summary>
		/// Override this method to have your own post import proccess
		/// </summary>
		protected virtual void OnImportComplete(TScriptableObject scriptableObject) { }
	}
	
	/// <inheritdoc />
	/// <remarks>
	/// Generic implementation of an importer to load a single <typeparamref name="TConfig"/> config into one
	/// <typeparamref name="TScriptableObject"/>.
	/// It will import 1 entire sheet into one single<typeparamref name="TConfig"/>. This means each row will match
	/// a different field of the <typeparamref name="TConfig"/> represented by a Key/Value pair.
	/// </remarks>
	public abstract class GoogleSheetSingleConfigImporter<TConfig, TScriptableObject> : IGoogleSheetImporter
		where TConfig : struct
		where TScriptableObject : ScriptableObject, ISingleConfigContainer<TConfig>
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

			scriptableObject.Config = Deserialize(data);
			
			EditorUtility.SetDirty(scriptableObject);
			OnImportComplete(scriptableObject);
		}

		/// <summary>
		/// Override this method to have your own deserialization of the given <paramref name="data"/>
		/// </summary>
		protected abstract TConfig Deserialize(List<Dictionary<string, string>> data);
		
		/// <summary>
		/// Override this method to have your own post import proccess
		/// </summary>
		protected virtual void OnImportComplete(TScriptableObject scriptableObject) { }
	}
}