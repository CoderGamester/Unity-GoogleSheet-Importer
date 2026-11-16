using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace GameLovers.SpreadsheetImporter.Editor
{
	/// <summary>
	/// Lists the project's importers and runs their pull and import steps one at a time or all together, showing
	/// the outcome of each run on the importer's row.
	/// </summary>
	public sealed class SpreadsheetImporterWindow : EditorWindow
	{
		private const string StyleSheetPath = "/Editor/SpreadsheetImporterWindow.uss";
		private const string OverrideTooltip =
			"Replaces the spreadsheet id of every Google Sheet source for the pulls started from this window. " +
			"Use it to pull from a duplicate of the sheet without changing the importers.";
		private const string NoGoogleSheetTooltip = "No importer has a Google Sheet source";
		private const string GoogleSheetKind = "Google Sheet";
		private const string LinkSharedOnly = "only sheets shared as \"Anyone with the link\" can be pulled";

		private static readonly (string Step, string Url)[] SetupSteps =
		{
			("Create a Google Cloud project, or pick an existing one.", "https://console.cloud.google.com/projectcreate"),
			("Enable the Google Sheets API in that project.", "https://console.cloud.google.com/apis/library/sheets.googleapis.com"),
			("Set up the sign-in screen with an app name and your e-mail. Audience: Internal for a Google Workspace team; " +
			 "otherwise External, then Publish app.", "https://console.cloud.google.com/auth/overview"),
			("Create an OAuth client of type Desktop app, then download its JSON file.",
			 "https://console.cloud.google.com/auth/clients/create")
		};

		[SerializeField] private string _spreadsheetIdOverride;

		private readonly Dictionary<string, (ImportResult Result, DateTime Time)> _results =
			new Dictionary<string, (ImportResult Result, DateTime Time)>();

		private TextField _spreadsheetId;
		private VisualElement _google;
		private string _googleMessage;
		private bool _showSetup;
		private Label _summary;
		private ScrollView _rows;

		private void CreateGUI()
		{
			var packagePath = PackageInfo.FindForAssembly(typeof(SpreadsheetImporterWindow).Assembly)?.assetPath;
			var styleSheet = packagePath == null ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(packagePath + StyleSheetPath);
			var toolbar = new VisualElement();

			if (styleSheet != null)
			{
				rootVisualElement.styleSheets.Add(styleSheet);
			}

			_summary = new Label();
			_spreadsheetId = new TextField("Spreadsheet ID override") { value = _spreadsheetIdOverride };
			_rows = new ScrollView();

			_google = new VisualElement();
			_google.AddToClassList("importer-google");
			_spreadsheetId.RegisterValueChangedCallback(change => _spreadsheetIdOverride = change.newValue);
			toolbar.AddToClassList("importer-toolbar");
			toolbar.Add(new Button(() => Run(SpreadsheetImporterEditorUtils.DiscoverImporters(), true)) { text = "Pull And Import All" });
			toolbar.Add(new Button(() => Run(SpreadsheetImporterEditorUtils.DiscoverImporters(), false)) { text = "Import All" });
			toolbar.Add(_summary);
			_summary.AddToClassList("importer-toolbar__summary");
			rootVisualElement.AddToClassList("importer-window");
			rootVisualElement.Add(toolbar);
			rootVisualElement.Add(_spreadsheetId);
			rootVisualElement.Add(_google);
			rootVisualElement.Add(_rows);
			RebuildGoogle();
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
		[MenuItem("Tools/GameLovers/Spreadsheet Importer/Open Window", priority = 200)]
		public static void Open()
		{
			GetWindow<SpreadsheetImporterWindow>("Spreadsheet Importer");
		}

		private void RebuildRows()
		{
			var importers = SpreadsheetImporterEditorUtils.DiscoverImporters();
			var failed = importers.Count(importer => _results.TryGetValue(importer.GetType().Name, out var last) &&
														!string.IsNullOrEmpty(last.Result.Error));
			var hasGoogleSheet = importers.Any(importer => SourceKind(importer.Source) == GoogleSheetKind);

			_rows.Clear();
			_summary.text = failed > 0 ? $"{importers.Count} importers, {failed} failed" : $"{importers.Count} importers";
			_spreadsheetId.SetEnabled(hasGoogleSheet);
			_spreadsheetId.tooltip = hasGoogleSheet ? OverrideTooltip : NoGoogleSheetTooltip;

			if (importers.Count == 0)
			{
				_rows.Add(new HelpBox("No importers yet. Add a class that implements ISpreadsheetImporter in an Editor assembly.",
					HelpBoxMessageType.Info));
			}

			foreach (var importer in importers)
			{
				_rows.Add(BuildRow(importer));
			}
		}

		private VisualElement BuildRow(ISpreadsheetImporter importer)
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

		private void Run(IReadOnlyList<ISpreadsheetImporter> importers, bool pull)
		{
			var report = SpreadsheetImporterEditorUtils.Run(importers, pull, _spreadsheetIdOverride);
			var time = DateTime.Now;

			foreach (var result in report.Results)
			{
				_results[result.Importer] = (result, time);
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			RebuildRows();
		}

		private void RebuildGoogle()
		{
			var hasClient = SpreadsheetImporterSettings.instance.HasGoogleClient;
			var header = new VisualElement();
			var title = new Label("Google sign-in");
			var status = new Label();

			_google.Clear();
			header.AddToClassList("importer-google__header");
			title.AddToClassList("importer-row__name");
			status.AddToClassList("importer-google__status");
			header.Add(title);
			header.Add(status);

			if (!hasClient)
			{
				status.text = $"Not set up: {LinkSharedOnly}";
			}
			else if (GoogleSignIn.InProgress)
			{
				status.text = "Waiting for you to approve in the browser…";
				header.Add(new Button(() =>
				{
					GoogleSignIn.Cancel();
					RebuildGoogle();
				}) { text = "Cancel" });
			}
			else if (GoogleSignIn.SignedIn)
			{
				status.text = $"Signed in as {GoogleSignIn.Account}: pulls can read your private sheets";
				header.Add(new Button(() =>
				{
					GoogleSignIn.SignOut();
					RebuildGoogle();
				}) { text = "Sign Out" });
			}
			else
			{
				status.text = $"Not signed in: {LinkSharedOnly}";
				header.Add(new Button(SignIn) { text = "Sign In With Google", tooltip = "Opens Google's consent page in your browser" });
			}

			header.Add(new Button(() =>
			{
				_showSetup = !_showSetup;
				RebuildGoogle();
			})
			{
				text = _showSetup ? "Hide Setup" : hasClient ? "Setup" : "Set Up",
				tooltip = "One person sets up Google sign-in once per project; everyone else only signs in"
			});
			_google.Add(header);

			if (_googleMessage != null)
			{
				_google.Add(new HelpBox(_googleMessage, HelpBoxMessageType.Error));
			}

			if (_showSetup)
			{
				_google.Add(SetupGuide(hasClient));
			}
		}

		private VisualElement SetupGuide(bool hasClient)
		{
			var guide = new VisualElement();

			guide.AddToClassList("importer-google__guide");
			guide.Add(StatusLabel("Once per project, by one person. The rest of the team then only clicks Sign In With Google."));

			for (var i = 0; i < SetupSteps.Length; i++)
			{
				var url = SetupSteps[i].Url;

				guide.Add(SetupStep(i + 1, SetupSteps[i].Step, new Button(() => Application.OpenURL(url)) { text = "Open", tooltip = url }));
			}

			guide.Add(SetupStep(SetupSteps.Length + 1,
				hasClient ? "The client is loaded. Load another file to replace it." : "Load the downloaded file here.",
				new Button(LoadClientFile) { text = "Load Client File…" }));
			guide.Add(StatusLabel("It is saved to ProjectSettings/GameLoversSpreadsheetImporterSettings.asset. Commit that file to share it."));

			return guide;
		}

		private void LoadClientFile()
		{
			var path = EditorUtility.OpenFilePanel("Google OAuth client file", string.Empty, "json");

			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			try
			{
				var client = GoogleSignIn.ReadClientFile(File.ReadAllText(path));

				SpreadsheetImporterSettings.instance.SetGoogleClient(client.ClientId, client.ClientSecret);
				_googleMessage = null;
				_showSetup = false;
			}
			catch (Exception e)
			{
				_googleMessage = e.Message;
			}

			RebuildGoogle();
		}

		private void SignIn()
		{
			_googleMessage = null;

			try
			{
				GoogleSignIn.Begin(error =>
				{
					if (this == null)
					{
						return;
					}

					_googleMessage = error;
					RebuildGoogle();
				});
			}
			catch (Exception e)
			{
				_googleMessage = e.Message;
			}

			RebuildGoogle();
		}

		private static VisualElement SetupStep(int number, string text, Button action)
		{
			var step = new VisualElement();
			var label = new Label($"{number}. {text}");

			step.AddToClassList("importer-google__step");
			label.AddToClassList("importer-google__step-text");
			step.Add(label);
			step.Add(action);

			return step;
		}

		private static Label StatusLabel(string text)
		{
			var label = new Label(text);

			label.AddToClassList("importer-google__note");

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

		private static void OpenSource(ISpreadsheetImporter importer)
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

		private static UnityEngine.Object FindAsset(ISpreadsheetImporter importer)
		{
			return importer is IScriptableObjectImporter scriptableObjectImporter
					   ? SpreadsheetImporterEditorUtils.FindAsset(scriptableObjectImporter.ScriptableObjectType)
					   : null;
		}
	}
}
