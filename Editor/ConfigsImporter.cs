using System;
using System.Collections.Generic;
using System.IO;
using GameLovers.GameData;
using UnityEditor;
using UnityEngine;

namespace GameLovers.SpreadsheetImporter.Editor
{
	/// <summary>Optional surface that lets tooling locate the ScriptableObject asset an importer writes.</summary>
	public interface IScriptableObjectImporter
	{
		/// <summary>The type of the ScriptableObject asset the importer writes.</summary>
		Type ScriptableObjectType { get; }
	}

	/// <summary>
	/// Imports one CSV file into the project at edit time. Concrete implementations are discovered by reflection.
	/// </summary>
	public interface ISpreadsheetImporter
	{
		/// <summary>Project-relative path of the CSV file this importer reads.</summary>
		string CsvPath { get; }

		/// <summary>
		/// Optional upstream the CSV file is pulled from: a Google Sheet URL, or an <c>.xlsx</c> path with an
		/// optional <c>#SheetName</c> suffix. Null or empty when the CSV file is authored directly.
		/// </summary>
		string Source { get; }

		/// <summary>Writes the parsed <paramref name="data"/> rows into the project.</summary>
		// ReSharper disable once ParameterTypeCanBeEnumerable.Global
		void Import(List<Dictionary<string, string>> data);
	}

	/// <summary>
	/// Imports a CSV file into the single asset of <typeparamref name="TScriptableObject"/> type, creating it
	/// next to the CSV file when the project has none.
	/// </summary>
	public abstract class ScriptableObjectImporter<TScriptableObject> :
		IScriptableObjectImporter, ISpreadsheetImporter where TScriptableObject : ScriptableObject
	{
		/// <inheritdoc />
		public abstract string CsvPath { get; }

		/// <inheritdoc />
		public virtual string Source => null;

		/// <summary>
		/// When true, every import also writes the asset's content as a JSON TextAsset next to it, for content
		/// that is delivered remotely and must load into a player built with a different schema.
		/// </summary>
		public virtual bool WriteJsonPayload => false;

		/// <inheritdoc />
		public Type ScriptableObjectType => typeof(TScriptableObject);

		/// <inheritdoc />
		public void Import(List<Dictionary<string, string>> data)
		{
			var type = typeof(TScriptableObject);
			var scriptableObject = SpreadsheetImporterEditorUtils.FindAsset(type) as TScriptableObject;
			var isNew = scriptableObject == null;
			var path = isNew
						   ? $"{CsvPath.Substring(0, CsvPath.LastIndexOf('/'))}/{type.Name}.asset"
						   : AssetDatabase.GetAssetPath(scriptableObject);

			if (isNew)
			{
				scriptableObject = ScriptableObject.CreateInstance<TScriptableObject>();
			}

			OnImport(scriptableObject, data);

			if (isNew)
			{
				AssetDatabase.CreateAsset(scriptableObject, path);
			}

			EditorUtility.SetDirty(scriptableObject);
			OnImportComplete(scriptableObject);

			if (WriteJsonPayload)
			{
				File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonUtility.ToJson(scriptableObject));
			}
		}

		/// <summary>
		/// Writes the parsed rows onto the target asset. Called with the asset already located or instantiated,
		/// and with saving plus <c>OnImportComplete</c> handled by the caller.
		/// </summary>
		protected abstract void OnImport(TScriptableObject scriptableObject, List<Dictionary<string, string>> data);

		/// <summary>Runs after the asset has been written and dirtied; the base implementation does nothing.</summary>
		protected virtual void OnImportComplete(TScriptableObject scriptableObject) { }
	}

	/// <summary>
	/// Imports one row per data entry: each row becomes one <typeparamref name="TConfig"/> in the asset's list.
	/// </summary>
	public abstract class ConfigsImporter<TConfig, TScriptableObject> :
		ScriptableObjectImporter<TScriptableObject>
		where TConfig : struct
		where TScriptableObject : ScriptableObject, IConfigsContainer<TConfig>
	{
		/// <summary>
		/// When true, a column without a matching field, or a field without a column, fails the import instead
		/// of being skipped. Turn it off when <c>Deserialize</c> maps rows by something other than field names.
		/// </summary>
		protected virtual bool StrictColumns => true;

		/// <inheritdoc />
		protected override void OnImport(TScriptableObject scriptableObject, List<Dictionary<string, string>> data)
		{
			var configs = new List<TConfig>(data.Count);

			if (StrictColumns && data.Count > 0)
			{
				var problems = CsvParser.ValidateColumns(typeof(TConfig), data[0].Keys);

				if (problems.Count > 0)
				{
					throw new SpreadsheetImportException(SpreadsheetImportException.HeaderRow, string.Join("; ", problems));
				}
			}

			for (var i = 0; i < data.Count; i++)
			{
				try
				{
					configs.Add(Deserialize(data[i]));
				}
				catch (Exception e) when (e is not SpreadsheetImportException)
				{
					throw new SpreadsheetImportException(i, e.Message, e);
				}
			}

			scriptableObject.Configs = configs;
		}

		/// <summary>
		/// Turns one row into a config. The default reflects over <typeparamref name="TConfig"/>'s public
		/// fields; override for rows the parser cannot map by name alone.
		/// </summary>
		protected virtual TConfig Deserialize(Dictionary<string, string> data)
		{
			return CsvParser.DeserializeTo<TConfig>(data);
		}
	}

	/// <summary>
	/// Imports the whole sheet into one <typeparamref name="TConfig"/>: each row is a Key/Value pair naming one
	/// field of the config.
	/// </summary>
	public abstract class SingleConfigImporter<TConfig, TScriptableObject> :
		ScriptableObjectImporter<TScriptableObject>
		where TConfig : struct
		where TScriptableObject : ScriptableObject, ISingleConfigContainer<TConfig>
	{
		/// <inheritdoc />
		protected override void OnImport(TScriptableObject scriptableObject, List<Dictionary<string, string>> data)
		{
			scriptableObject.Config = Deserialize(data);
		}

		/// <summary>
		/// Turns the whole sheet into a single config. No default is provided, because the row-to-field
		/// mapping for a single-config sheet is entirely importer-specific.
		/// </summary>
		protected abstract TConfig Deserialize(List<Dictionary<string, string>> data);
	}

	/// <summary>
	/// Works like <see cref="SingleConfigImporter{TConfig,TScriptableObject}"/> and also reads lists of nested
	/// rows: a Key ending in <c>[]</c> starts a list whose entries are the following rows keyed <c>#</c>.
	/// </summary>
	public abstract class SingleConfigSubListImporter<TConfig, TScriptableObject> :
		SingleConfigImporter<TConfig, TScriptableObject>
		where TConfig : struct
		where TScriptableObject : ScriptableObject, ISingleConfigContainer<TConfig>
	{
		/// <inheritdoc />
		protected override TConfig Deserialize(List<Dictionary<string, string>> data)
		{
			var config = new TConfig() as object;
			var type = typeof(TConfig);

			for (var i = 0; i < data.Count; i++)
			{
				var row = data[i];
				var fieldName = row["Key"];
				var isSubList = fieldName.EndsWith(CsvParser.SUB_LIST_SUFFIX);

				if (fieldName == CsvParser.IGNORE_FIELD_CHAR || fieldName.StartsWith(CsvParser.IGNORE_COLUMN_CHAR))
				{
					continue;
				}

				if (isSubList)
				{
					fieldName = fieldName.Replace(CsvParser.SUB_LIST_SUFFIX, "");
				}

				var field = type.GetField(fieldName);

				if (field == null)
				{
					throw new SpreadsheetImportException(i, $"Unknown key '{fieldName}': {type.Name} has no public field " +
													 $"with that name (prefix the key with '{CsvParser.IGNORE_COLUMN_CHAR}' to ignore the row)");
				}

				try
				{
					var value = isSubList
									? CsvParser.DeserializeSubList(data, i, field.FieldType, field.Name, GetDeserializers())
									: CsvParser.DeserializeObject(row["Value"], field.FieldType, GetDeserializers());

					field.SetValue(config, value);
				}
				catch (Exception e) when (e is not SpreadsheetImportException)
				{
					throw new SpreadsheetImportException(i, $"Key '{fieldName}': {e.Message}", e);
				}
			}

			return (TConfig)config;
		}

		/// <summary>
		/// Custom per-type parsers handed to <c>CsvParser</c> for cell values it cannot convert itself.
		/// </summary>
		protected abstract Func<string, Type, object>[] GetDeserializers();
	}
}