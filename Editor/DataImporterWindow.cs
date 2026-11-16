using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLovers.DataImporter.Editor
{
	/// <summary>
	/// Lists the project's importers and runs their pull and import steps one at a time or all together.
	/// </summary>
	public sealed class DataImporterWindow : EditorWindow
	{
		private const string OverrideTooltip =
			"Replaces the spreadsheet id of every Google Sheet source for the pulls started from this window. " +
			"Use it to pull from a duplicate of the sheet without changing the importers.";

		[SerializeField] private string _spreadsheetIdOverride;

		private ScrollView _rows;

		private void CreateGUI()
		{
			var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
			var spreadsheetId = new TextField("Spreadsheet ID override") { value = _spreadsheetIdOverride, tooltip = OverrideTooltip };

			spreadsheetId.RegisterValueChangedCallback(change => _spreadsheetIdOverride = change.newValue);
			toolbar.Add(new Button(() => Run(() => DataImporterEditorUtils.ImportAll(true, _spreadsheetIdOverride)))
			{
				text = "Pull And Import All"
			});
			toolbar.Add(new Button(() => Run(() => DataImporterEditorUtils.ImportAll(false, null))) { text = "Import All" });

			_rows = new ScrollView();
			rootVisualElement.style.paddingLeft = 6;
			rootVisualElement.style.paddingRight = 6;
			rootVisualElement.style.paddingTop = 6;
			rootVisualElement.Add(toolbar);
			rootVisualElement.Add(spreadsheetId);
			rootVisualElement.Add(_rows);
			RebuildRows();
		}

		private void OnFocus()
		{
			if (_rows != null)
			{
				RebuildRows();
			}
		}

		/// <summary>Opens the window, or focuses it when it is already open.</summary>
		[MenuItem("Tools/GameLovers/Data Importer/Open Window", priority = 200)]
		public static void Open()
		{
			GetWindow<DataImporterWindow>("Data Importer");
		}

		private void RebuildRows()
		{
			var importers = DataImporterEditorUtils.DiscoverImporters();

			_rows.Clear();

			if (importers.Count == 0)
			{
				_rows.Add(new HelpBox("No importers yet. Add a class that implements IDataImporter in an Editor assembly.",
					HelpBoxMessageType.Info));
			}

			foreach (var importer in importers)
			{
				_rows.Add(BuildRow(importer));
			}
		}

		private VisualElement BuildRow(IDataImporter importer)
		{
			var hasSource = !string.IsNullOrWhiteSpace(importer.Source);
			var asset = FindAsset(importer);
			var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 } };
			var labels = new VisualElement { style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden } };
			var pull = new Button(() => Run(() => DataImporterEditorUtils.Pull(importer, _spreadsheetIdOverride))) { text = "Pull" };
			var select = new Button(() => Selection.activeObject = asset) { text = "Select Asset" };

			labels.Add(new Label(importer.GetType().Name) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
			labels.Add(new Label(hasSource ? $"{importer.CsvPath}  ←  {importer.Source}" : importer.CsvPath)
			{
				tooltip = hasSource ? importer.Source : importer.CsvPath
			});

			pull.SetEnabled(hasSource);
			pull.tooltip = hasSource ? "Overwrite the CSV file with the upstream source" : "This importer has no upstream source; edit the CSV file directly";
			select.SetEnabled(asset != null);
			select.tooltip = asset != null ? AssetDatabase.GetAssetPath(asset) : "No asset yet; import first";

			row.Add(labels);
			row.Add(pull);
			row.Add(new Button(() => Run(() => DataImporterEditorUtils.Import(importer))) { text = "Import" });
			row.Add(select);

			return row;
		}

		private void Run(Action action)
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", e.Message);
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			RebuildRows();
		}

		private static UnityEngine.Object FindAsset(IDataImporter importer)
		{
			return importer is IScriptableObjectImporter scriptableObjectImporter
					   ? DataImporterEditorUtils.FindAsset(scriptableObjectImporter.ScriptableObjectType)
					   : null;
		}
	}
}
