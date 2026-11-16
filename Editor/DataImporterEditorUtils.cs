using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
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
			var importers = DiscoverImporters();
			var failures = 0;

			foreach (var importer in importers)
			{
				try
				{
					if (pull && !string.IsNullOrWhiteSpace(importer.Source))
					{
						Pull(importer, spreadsheetIdOverride);
					}

					Import(importer);
					Debug.Log($"Imported {importer.CsvPath} with {importer.GetType().Name}");
				}
				catch (Exception e)
				{
					failures++;
					Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", e.Message);
				}
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			if (failures > 0)
			{
				throw new InvalidOperationException($"Data import failed: {failures} of {importers.Count} importers reported errors");
			}
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
						  ? DownloadCsv(GoogleSheetCsvUrl(source, spreadsheetIdOverride))
						  : ReadWorkbookAsCsv(source);

			Directory.CreateDirectory(Path.GetDirectoryName(importer.CsvPath));
			File.WriteAllText(importer.CsvPath, csv);
			AssetDatabase.ImportAsset(importer.CsvPath);
		}

		/// <summary>
		/// Reads the importer's CSV file and runs the importer on it, throwing a <see cref="DataImportException"/>
		/// whose message starts with <c>path:line</c> when the file or a row cannot be imported.
		/// </summary>
		public static void Import(IDataImporter importer)
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

		private static int ImportOrder(IDataImporter importer)
		{
			return importer.GetType().GetCustomAttribute<ImportOrderAttribute>()?.ImportOrder ?? int.MaxValue;
		}

		private static string DownloadCsv(string url)
		{
			using var request = UnityWebRequest.Get(url);

			request.timeout = DownloadTimeoutSeconds;

			var operation = request.SendWebRequest();

			while (!operation.isDone)
			{
				Thread.Sleep(10);
			}

			if (request.result != UnityWebRequest.Result.Success)
			{
				throw new IOException($"Download failed for {url}: {request.error}");
			}

			var text = request.downloadHandler.text;
			var contentType = request.GetResponseHeader("Content-Type") ?? string.Empty;

			if (contentType.Contains("text/html") || text.TrimStart().StartsWith("<"))
			{
				throw new IOException($"{url} returned a web page instead of CSV: share the sheet as 'Anyone with the link'");
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
