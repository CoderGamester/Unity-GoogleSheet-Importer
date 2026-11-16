using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace GameLovers.DataImporter.Editor
{
	/// <summary>
	/// Lists the project's importers and runs their pull and import steps one at a time or all together, showing
	/// the outcome of each run on the importer's row.
	/// </summary>
	public sealed class DataImporterWindow : EditorWindow
	{
		private const string StyleSheetPath = "/Editor/DataImporterWindow.uss";
		private const string OverrideTooltip =
			"Replaces the spreadsheet id of every Google Sheet source for the pulls started from this window. " +
			"Use it to pull from a duplicate of the sheet without changing the importers.";
		private const string NoGoogleSheetTooltip = "No importer has a Google Sheet source";
		private const string GoogleSheetKind = "Google Sheet";
		private const string KeyTooltip =
			"JSON key of a Google service account, used to pull private sheets shared with that account. Stored for " +
			"you and this project in EditorPrefs, never in the project. Keep the file outside the project.";

		[SerializeField] private string _spreadsheetIdOverride;

		private readonly Dictionary<string, (ImportResult Result, DateTime Time)> _results =
			new Dictionary<string, (ImportResult Result, DateTime Time)>();

		private TextField _spreadsheetId;
		private VisualElement _keyRow;
		private TextField _keyField;
		private VisualElement _keyStatus;
		private Label _summary;
		private ScrollView _rows;

		private void CreateGUI()
		{
			var packagePath = PackageInfo.FindForAssembly(typeof(DataImporterWindow).Assembly)?.assetPath;
			var styleSheet = packagePath == null ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(packagePath + StyleSheetPath);
			var toolbar = new VisualElement();

			if (styleSheet != null)
			{
				rootVisualElement.styleSheets.Add(styleSheet);
			}

			_summary = new Label();
			_spreadsheetId = new TextField("Spreadsheet ID override") { value = _spreadsheetIdOverride };
			_rows = new ScrollView();

			_keyRow = new VisualElement();
			_keyField = new TextField("Service account key") { value = DataImporterEditorUtils.ServiceAccountKeySetting };
			_keyStatus = new VisualElement();
			_keyRow.AddToClassList("importer-key");
			_keyField.AddToClassList("importer-key__field");
			_keyField.tooltip = KeyTooltip;
			_keyField.RegisterValueChangedCallback(change =>
			{
				DataImporterEditorUtils.ServiceAccountKeySetting = change.newValue;
				UpdateKeyStatus();
			});
			_keyRow.Add(_keyField);
			_keyRow.Add(new Button(BrowseKey) { text = "Browse", tooltip = "Pick the service account's JSON key file" });
			_spreadsheetId.RegisterValueChangedCallback(change => _spreadsheetIdOverride = change.newValue);
			toolbar.AddToClassList("importer-toolbar");
			toolbar.Add(new Button(() => Run(DataImporterEditorUtils.DiscoverImporters(), true)) { text = "Pull And Import All" });
			toolbar.Add(new Button(() => Run(DataImporterEditorUtils.DiscoverImporters(), false)) { text = "Import All" });
			toolbar.Add(_summary);
			_summary.AddToClassList("importer-toolbar__summary");
			rootVisualElement.AddToClassList("importer-window");
			rootVisualElement.Add(toolbar);
			rootVisualElement.Add(_spreadsheetId);
			rootVisualElement.Add(_keyRow);
			rootVisualElement.Add(_keyStatus);
			rootVisualElement.Add(_rows);
			UpdateKeyStatus();
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
			var failed = importers.Count(importer => _results.TryGetValue(importer.GetType().Name, out var last) &&
														!string.IsNullOrEmpty(last.Result.Error));
			var hasGoogleSheet = importers.Any(importer => SourceKind(importer.Source) == GoogleSheetKind);

			_rows.Clear();
			_summary.text = failed > 0 ? $"{importers.Count} importers, {failed} failed" : $"{importers.Count} importers";
			_spreadsheetId.SetEnabled(hasGoogleSheet);
			_spreadsheetId.tooltip = hasGoogleSheet ? OverrideTooltip : NoGoogleSheetTooltip;
			_keyRow.SetEnabled(hasGoogleSheet);
			_keyField.tooltip = hasGoogleSheet ? KeyTooltip : NoGoogleSheetTooltip;

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
			var name = importer.GetType().Name;
			var source = importer.Source;
			var hasSource = !string.IsNullOrWhiteSpace(source);
			var asset = FindAsset(importer);
			var row = new VisualElement();
			var header = new VisualElement();
			var labels = new VisualElement();
			var title = new VisualElement();
			var nameLabel = new Label(name);
			var badge = new Label(SourceKind(source));
			var open = new Button(() => OpenSource(importer)) { text = "Open Source" };
			var pull = new Button(() => Run(new[] { importer }, true)) { text = "Pull And Import" };
			var import = new Button(() => Run(new[] { importer }, false)) { text = "Import" };
			var select = new Button(() => Selection.activeObject = asset) { text = "Select Asset" };

			row.AddToClassList("importer-row");
			header.AddToClassList("importer-row__header");
			labels.AddToClassList("importer-row__labels");
			title.AddToClassList("importer-row__title");
			badge.AddToClassList("importer-badge");
			nameLabel.AddToClassList("importer-row__name");
			title.Add(nameLabel);
			title.Add(badge);
			labels.Add(title);
			labels.Add(PathLabel(importer.CsvPath));

			if (hasSource)
			{
				labels.Add(PathLabel($"← {source}", source));
			}

			open.tooltip = hasSource ? $"Open {source}" : $"Open {importer.CsvPath}; this importer has no upstream source";
			pull.SetEnabled(hasSource);
			pull.tooltip = hasSource
							   ? "Overwrite the CSV file with the upstream source, then import it"
							   : "This importer has no upstream source; edit the CSV file directly";
			import.tooltip = $"Import {importer.CsvPath} into the asset";
			select.SetEnabled(asset != null);
			select.tooltip = asset != null ? AssetDatabase.GetAssetPath(asset) : "No asset yet; import first";

			header.Add(labels);
			header.Add(open);
			header.Add(pull);
			header.Add(import);
			header.Add(select);
			row.Add(header);

			if (_results.TryGetValue(name, out var last))
			{
				row.Add(ResultBox(last.Result, last.Time));
			}

			return row;
		}

		private void Run(IReadOnlyList<IDataImporter> importers, bool pull)
		{
			var report = DataImporterEditorUtils.Run(importers, pull, _spreadsheetIdOverride);
			var time = DateTime.Now;

			foreach (var result in report.Results)
			{
				_results[result.Importer] = (result, time);
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			RebuildRows();
		}

		private void BrowseKey()
		{
			var path = EditorUtility.OpenFilePanel("Service account key", string.Empty, "json");

			if (!string.IsNullOrEmpty(path))
			{
				_keyField.value = path;
			}
		}

		private void UpdateKeyStatus()
		{
			var setting = DataImporterEditorUtils.ServiceAccountKeySetting;
			var environment = DataImporterEditorUtils.ServiceAccountKeyEnvironment;
			var path = DataImporterEditorUtils.ResolveServiceAccountKey(Array.Empty<string>(), setting, environment);

			_keyStatus.Clear();

			if (path == null)
			{
				_keyStatus.Add(StatusLabel("No key: only sheets shared as \"Anyone with the link\" can be pulled"));

				return;
			}

			var origin = string.IsNullOrWhiteSpace(setting) ? "GOOGLE_APPLICATION_CREDENTIALS" : "this field";

			try
			{
				if (DataImporterEditorUtils.IsInsideProject(path))
				{
					_keyStatus.Add(new HelpBox($"The key from {origin} is inside the project. Move it out so it cannot be committed.",
						HelpBoxMessageType.Warning));
				}

				var account = GoogleServiceAccount.ClientEmail(File.ReadAllText(path));

				_keyStatus.Add(StatusLabel($"Signs in as {account} (key from {origin}). Share private sheets with that address."));
			}
			catch (Exception e)
			{
				_keyStatus.Add(new HelpBox($"The key from {origin} cannot be used: {e.Message}", HelpBoxMessageType.Error));
			}
		}

		private static Label StatusLabel(string text)
		{
			var label = new Label(text);

			label.AddToClassList("importer-key__status");

			return label;
		}

		private static string SourceKind(string source)
		{
			if (string.IsNullOrWhiteSpace(source))
			{
				return "CSV";
			}

			return source.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? GoogleSheetKind : "Excel";
		}

		private static void OpenSource(IDataImporter importer)
		{
			var source = importer.Source;

			if (string.IsNullOrWhiteSpace(source))
			{
				EditorUtility.OpenWithDefaultApp(importer.CsvPath);
			}
			else if (SourceKind(source) == GoogleSheetKind)
			{
				Application.OpenURL(source);
			}
			else
			{
				var separator = source.LastIndexOf('#');

				EditorUtility.OpenWithDefaultApp(separator < 0 ? source : source.Substring(0, separator));
			}
		}

		private static Label PathLabel(string text, string tooltip = null)
		{
			var label = new Label(text) { tooltip = tooltip ?? text };

			label.AddToClassList("importer-row__path");

			return label;
		}

		private static VisualElement ResultBox(ImportResult result, DateTime time)
		{
			var stamp = time.ToString("HH:mm:ss");

			if (!string.IsNullOrEmpty(result.Error))
			{
				return new HelpBox($"{stamp}  {result.Error}", HelpBoxMessageType.Error);
			}

			var pulled = result.Pulled ? "Pulled and imported" : "Imported";
			var status = new Label($"✓ {pulled} {result.Rows} rows at {stamp}");

			status.AddToClassList("importer-row__status");

			return status;
		}

		private static UnityEngine.Object FindAsset(IDataImporter importer)
		{
			return importer is IScriptableObjectImporter scriptableObjectImporter
					   ? DataImporterEditorUtils.FindAsset(scriptableObjectImporter.ScriptableObjectType)
					   : null;
		}
	}
}
