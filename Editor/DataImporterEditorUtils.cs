using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GameLovers.DataImporter.Editor
{
	/// <summary>
	/// Discovers the project's importers and runs their pull and import steps, in the Editor or headless.
	/// </summary>
	public static class DataImporterEditorUtils
	{
		private const string MenuRoot = "Tools/GameLovers/Data Importer/";
		private const string PublishedUrlMarker = "e";
		private const int DownloadTimeoutSeconds = 30;
		private const string ProgressTitle = "Data Importer";
		private const string ImportersArg = "-dataImporters";
		private const string PullArg = "-dataImportPull";
		private const string SpreadsheetIdArg = "-dataImportSpreadsheetId";
		private const string ReportArg = "-dataImportReport";
		private const string ServiceAccountKeyArg = "-dataImportServiceAccountKey";
		private const string ServiceAccountKeyVariable = "GOOGLE_APPLICATION_CREDENTIALS";
		private const string ServiceAccountKeyPrefsPrefix = "GameLovers.DataImporter.ServiceAccountKey:";

		/// <summary>The key path set in the window, kept per user and project in EditorPrefs.</summary>
		internal static string ServiceAccountKeySetting
		{
			get => EditorPrefs.GetString(ServiceAccountKeyPrefsKey, string.Empty);
			set
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					EditorPrefs.DeleteKey(ServiceAccountKeyPrefsKey);
				}
				else
				{
					EditorPrefs.SetString(ServiceAccountKeyPrefsKey, value.Trim());
				}
			}
		}

		/// <summary>The value of the <c>GOOGLE_APPLICATION_CREDENTIALS</c> environment variable, or null.</summary>
		internal static string ServiceAccountKeyEnvironment => Environment.GetEnvironmentVariable(ServiceAccountKeyVariable);

		private static string ServiceAccountKeyPrefsKey => ServiceAccountKeyPrefsPrefix + Application.dataPath;

		/// <summary>
		/// Imports every CSV file into its target and throws when any importer fails, so a batchmode
		/// <c>-executeMethod</c> run exits with a non-zero code.
		/// </summary>
		[MenuItem(MenuRoot + "Import All", priority = 100)]
		public static void ImportAll()
		{
			ImportAll(false, null);
		}

		/// <summary>Refreshes every CSV file that has an upstream source, then imports all of them.</summary>
		[MenuItem(MenuRoot + "Pull And Import All", priority = 101)]
		public static void PullAndImportAll()
		{
			ImportAll(true, null);
		}

		/// <summary>
		/// Runs every importer in order, optionally pulling each upstream first, logs one <c>path:line</c>
		/// error per failing importer and throws once all of them have run.
		/// </summary>
		public static void ImportAll(bool pull, string spreadsheetIdOverride)
		{
			ThrowIfFailed(RunAndSave(DiscoverImporters(), pull, spreadsheetIdOverride));
		}

		/// <summary>
		/// Headless entry point for <c>-executeMethod</c>. Reads <c>-dataImporters A,B</c> (importer type names;
		/// all importers when absent), <c>-dataImportPull</c>, <c>-dataImportSpreadsheetId &lt;id&gt;</c> and
		/// <c>-dataImportReport &lt;path&gt;</c> from the command line, writes the JSON report when asked, and throws
		/// when any importer fails so the process exits non-zero.
		/// </summary>
		public static void RunFromCommandLine()
		{
			var args = Environment.GetCommandLineArgs();
			var reportPath = ArgValue(args, ReportArg);

			if (reportPath != null && File.Exists(reportPath))
			{
				File.Delete(reportPath);
			}

			var importers = SelectImporters(DiscoverImporters(), ArgValue(args, ImportersArg));
			var report = RunAndSave(importers, Array.IndexOf(args, PullArg) >= 0, ArgValue(args, SpreadsheetIdArg));

			if (reportPath != null)
			{
				var directory = Path.GetDirectoryName(reportPath);

				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}

				File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
			}

			ThrowIfFailed(report);
		}

		/// <summary>
		/// Runs the given importers in order, optionally pulling each upstream first, and returns one result per
		/// importer. Logs each failure as <c>path:line: message</c> and never throws; it does not save assets.
		/// </summary>
		public static ImportReport Run(IReadOnlyList<IDataImporter> importers, bool pull, string spreadsheetIdOverride)
		{
			var report = new ImportReport();

			try
			{
				for (var i = 0; i < importers.Count; i++)
				{
					var importer = importers[i];
					var result = new ImportResult
					{
						Importer = importer.GetType().Name,
						CsvPath = importer.CsvPath,
						Source = importer.Source
					};

					ShowProgress($"Importing {result.Importer}", (float)i / importers.Count);

					try
					{
						if (pull && !string.IsNullOrWhiteSpace(importer.Source))
						{
							Pull(importer, spreadsheetIdOverride);
							result.Pulled = true;
						}

						result.Rows = Import(importer);
						Debug.Log($"Imported {result.Rows} rows from {importer.CsvPath} with {result.Importer}");
					}
					catch (Exception e)
					{
						result.Error = e.Message;
						report.Failed++;
						Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", e.Message);
					}

					report.Results.Add(result);
				}
			}
			finally
			{
				ClearProgress();
			}

			return report;
		}

		/// <summary>
		/// Creates one instance of every concrete importer, sorted by import order and then type name.
		/// </summary>
		public static List<IDataImporter> DiscoverImporters()
		{
			var importers = new List<IDataImporter>();

			foreach (var type in TypeCache.GetTypesDerivedFrom<IDataImporter>())
			{
				if (!type.IsAbstract && !type.IsInterface)
				{
					importers.Add((IDataImporter)Activator.CreateInstance(type));
				}
			}

			importers.Sort((left, right) =>
			{
				var order = ImportOrder(left).CompareTo(ImportOrder(right));

				return order != 0 ? order : string.Compare(left.GetType().Name, right.GetType().Name, StringComparison.Ordinal);
			});

			return importers;
		}

		/// <summary>
		/// Overwrites the importer's CSV file with the current content of its upstream source. A non-empty
		/// <paramref name="spreadsheetIdOverride"/> replaces the spreadsheet id of a Google Sheet source.
		/// </summary>
		public static void Pull(IDataImporter importer, string spreadsheetIdOverride = null)
		{
			var source = importer.Source;

			if (string.IsNullOrWhiteSpace(source))
			{
				throw new InvalidOperationException($"{importer.GetType().Name} has no upstream source to pull {importer.CsvPath} from");
			}

			var csv = source.StartsWith("http", StringComparison.OrdinalIgnoreCase)
						  ? DownloadCsv(GoogleSheetCsvUrl(source, spreadsheetIdOverride), ServiceAccountKeyPath())
						  : ReadWorkbookAsCsv(source);

			Directory.CreateDirectory(Path.GetDirectoryName(importer.CsvPath));
			File.WriteAllText(importer.CsvPath, csv);
			AssetDatabase.ImportAsset(importer.CsvPath);
		}

		/// <summary>
		/// Reads the importer's CSV file, runs the importer on it and returns the number of data rows, throwing a
		/// <see cref="DataImportException"/> whose message starts with <c>path:line</c> when the file or a row
		/// cannot be imported.
		/// </summary>
		public static int Import(IDataImporter importer)
		{
			var path = importer.CsvPath;
			var lines = new List<int>();

			try
			{
				var rows = CsvParser.ConvertCsv(File.ReadAllText(path), lines);

				if (rows.Count == 0)
				{
					throw new DataImportException(DataImportException.HeaderRow,
						"no data rows: the file needs a header line and at least one row");
				}

				importer.Import(rows);

				return rows.Count;
			}
			catch (DataImportException e)
			{
				var line = e.Row >= 0 && e.Row < lines.Count ? lines[e.Row] : 1;

				throw new DataImportException(e.Row, $"{path}:{line}: {e.Message}", e);
			}
			catch (Exception e)
			{
				throw new DataImportException(DataImportException.HeaderRow, $"{path}: {e.Message}", e);
			}
		}

		/// <summary>
		/// Turns any Google Sheet URL copied from the browser into the CSV export URL of the same tab. A
		/// non-empty <paramref name="spreadsheetIdOverride"/> replaces the spreadsheet id.
		/// </summary>
		public static string GoogleSheetCsvUrl(string sheetUrl, string spreadsheetIdOverride = null)
		{
			var id = Regex.Match(sheetUrl, @"/spreadsheets/(?:u/\d+/)?d/([A-Za-z0-9_-]+)");
			var gid = Regex.Match(sheetUrl, @"[#?&]gid=(\d+)");

			if (!id.Success)
			{
				throw new FormatException($"'{sheetUrl}' is not a Google Sheet URL: no /spreadsheets/d/<id> part");
			}

			if (id.Groups[1].Value == PublishedUrlMarker)
			{
				throw new FormatException($"'{sheetUrl}' is a published-to-web URL: use the URL of the sheet's edit page instead");
			}

			var spreadsheetId = string.IsNullOrWhiteSpace(spreadsheetIdOverride) ? id.Groups[1].Value : spreadsheetIdOverride.Trim();

			return $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format=csv&gid={(gid.Success ? gid.Groups[1].Value : "0")}";
		}

		/// <summary>
		/// Returns the project's asset of exactly the given <paramref name="type"/>, or null when it has none. A
		/// type name shared with another namespace does not match.
		/// </summary>
		public static UnityEngine.Object FindAsset(Type type)
		{
			foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
			{
				var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type);

				if (asset != null)
				{
					return asset;
				}
			}

			return null;
		}

		/// <summary>
		/// Picks the importers named in a comma-separated list of type names, keeping import order. Returns all of
		/// them when <paramref name="names"/> is null and throws when a name matches no importer.
		/// </summary>
		internal static List<IDataImporter> SelectImporters(IReadOnlyList<IDataImporter> importers, string names)
		{
			if (names == null)
			{
				return new List<IDataImporter>(importers);
			}

			var requested = new HashSet<string>(StringComparer.Ordinal);

			foreach (var name in names.Split(','))
			{
				if (name.Trim().Length > 0)
				{
					requested.Add(name.Trim());
				}
			}

			var selected = importers.Where(importer => requested.Remove(importer.GetType().Name)).ToList();

			if (requested.Count > 0)
			{
				var available = string.Join(", ", importers.Select(importer => importer.GetType().Name));

				throw new ArgumentException($"No importer named {string.Join(", ", requested)}; available: {available}");
			}

			return selected;
		}

		/// <summary>Returns the argument after <paramref name="name"/>, or null when the flag is absent.</summary>
		internal static string ArgValue(string[] args, string name)
		{
			var index = Array.IndexOf(args, name);

			if (index < 0)
			{
				return null;
			}

			if (index + 1 >= args.Length)
			{
				throw new ArgumentException($"{name} needs a value");
			}

			return args[index + 1];
		}

		/// <summary>The key file Google Sheet pulls sign in with, or null to download anonymously.</summary>
		internal static string ServiceAccountKeyPath()
		{
			return ResolveServiceAccountKey(Environment.GetCommandLineArgs(), ServiceAccountKeySetting, ServiceAccountKeyEnvironment);
		}

		/// <summary>Picks the key path from the command line, the window setting, then the environment.</summary>
		internal static string ResolveServiceAccountKey(string[] args, string setting, string environment)
		{
			var argument = ArgValue(args, ServiceAccountKeyArg);

			if (!string.IsNullOrWhiteSpace(argument))
			{
				return argument.Trim();
			}

			if (!string.IsNullOrWhiteSpace(setting))
			{
				return setting.Trim();
			}

			return string.IsNullOrWhiteSpace(environment) ? null : environment.Trim();
		}

		/// <summary>True when <paramref name="path"/> is inside the project, where it could be committed.</summary>
		internal static bool IsInsideProject(string path)
		{
			var project = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar) +
						  Path.DirectorySeparatorChar;

			return Path.GetFullPath(path).StartsWith(project, StringComparison.Ordinal);
		}

		private static string ReadServiceAccountKey(string path)
		{
			if (!File.Exists(path))
			{
				throw new FileNotFoundException($"Service-account key not found: {path}");
			}

			if (IsInsideProject(path))
			{
				Debug.LogWarning($"The service-account key {path} is inside the project. Keep it outside so it cannot be committed.");
			}

			return File.ReadAllText(path);
		}

		private static string RequestAccessToken(string keyJson)
		{
			var tokenUri = GoogleServiceAccount.TokenUri(keyJson);
			var form = new WWWForm();

			form.AddField("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer");
			form.AddField("assertion", GoogleServiceAccount.CreateAssertion(keyJson, DateTime.UtcNow));

			using var request = UnityWebRequest.Post(tokenUri, form);

			Send(request, "Signing in with the service-account key");

			var body = request.downloadHandler?.text ?? string.Empty;

			if (request.result != UnityWebRequest.Result.Success)
			{
				throw new IOException($"Google refused the service-account sign-in ({request.responseCode}): {TokenError(body, request.error)}");
			}

			var token = JObject.Parse(body).Value<string>("access_token");

			if (string.IsNullOrEmpty(token))
			{
				throw new IOException("Google's sign-in response has no access token");
			}

			return token;
		}

		private static string TokenError(string body, string fallback)
		{
			try
			{
				var error = JObject.Parse(body);

				return $"{error.Value<string>("error")}: {error.Value<string>("error_description")}";
			}
			catch (JsonException)
			{
				return fallback;
			}
		}

		private static ImportReport RunAndSave(IReadOnlyList<IDataImporter> importers, bool pull, string spreadsheetIdOverride)
		{
			var report = Run(importers, pull, spreadsheetIdOverride);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			return report;
		}

		private static void ThrowIfFailed(ImportReport report)
		{
			if (report.Failed > 0)
			{
				throw new InvalidOperationException(
					$"Data import failed: {report.Failed} of {report.Results.Count} importers reported errors");
			}
		}

		private static int ImportOrder(IDataImporter importer)
		{
			return importer.GetType().GetCustomAttribute<ImportOrderAttribute>()?.ImportOrder ?? int.MaxValue;
		}

		private static void ShowProgress(string info, float progress)
		{
			if (!Application.isBatchMode)
			{
				EditorUtility.DisplayProgressBar(ProgressTitle, info, progress);
			}
		}

		private static void ClearProgress()
		{
			if (!Application.isBatchMode)
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private static void Send(UnityWebRequest request, string info)
		{
			request.timeout = DownloadTimeoutSeconds;

			var operation = request.SendWebRequest();

			try
			{
				while (!operation.isDone)
				{
					if (!Application.isBatchMode &&
						EditorUtility.DisplayCancelableProgressBar(ProgressTitle, info, request.downloadProgress))
					{
						request.Abort();

						throw new OperationCanceledException($"Cancelled: {info}");
					}

					Thread.Sleep(10);
				}
			}
			finally
			{
				ClearProgress();
			}
		}

		private static string DownloadCsv(string url, string keyPath)
		{
			var keyJson = keyPath == null ? null : ReadServiceAccountKey(keyPath);
			var account = keyJson == null ? null : GoogleServiceAccount.ClientEmail(keyJson);

			using var request = UnityWebRequest.Get(url);

			if (keyJson != null)
			{
				request.SetRequestHeader("Authorization", $"Bearer {RequestAccessToken(keyJson)}");
			}

			Send(request, $"Downloading {url}");

			if (account != null && request.responseCode is 401 or 403 or 404)
			{
				throw new IOException($"{url}: the service account {account} cannot read this sheet ({request.responseCode}); " +
									  "share the sheet with that address");
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				throw new IOException($"Download failed for {url}: {request.error}");
			}

			var text = request.downloadHandler.text;
			var contentType = request.GetResponseHeader("Content-Type") ?? string.Empty;

			if (contentType.Contains("text/html") || text.TrimStart().StartsWith("<"))
			{
				throw new IOException(account == null
										  ? $"{url} returned a web page instead of CSV: share the sheet as 'Anyone with the link', " +
											"or set a service-account key for a private sheet"
										  : $"{url} returned a web page instead of CSV: share the sheet with {account}");
			}

			text = text.Replace("\r\n", "\n");

			return text.EndsWith("\n") ? text : text + "\n";
		}

		private static string ReadWorkbookAsCsv(string source)
		{
			var separator = source.LastIndexOf('#');
			var path = separator < 0 ? source : source.Substring(0, separator);
			var sheetName = separator < 0 ? null : source.Substring(separator + 1);

			return CsvParser.ToCsv(XlsxReader.ReadSheet(path, sheetName));
		}
	}
}
