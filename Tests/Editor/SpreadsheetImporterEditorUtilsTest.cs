using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameLovers.SpreadsheetImporter.Editor.Tests
{
	public class SpreadsheetImporterEditorUtilsTest
	{
		private const string Sheet = "https://docs.google.com/spreadsheets/d/SHEET_id-1";

		private ISpreadsheetImporter _importer;
		private ISpreadsheetImporter _otherImporter;
		private string _csvPath;

		[SetUp]
		public void SetUp()
		{
			_csvPath = Path.Combine(Path.GetTempPath(), $"SpreadsheetImporterTest-{Guid.NewGuid():N}.csv");
			File.WriteAllText(_csvPath, "Name\nA\nB\n");
			_importer = Substitute.For<ISpreadsheetImporter>();
			_otherImporter = Substitute.For<ISpreadsheetImporter, IScriptableObjectImporter>();
			_importer.CsvPath.Returns(_csvPath);
			_otherImporter.CsvPath.Returns(_csvPath);
		}

		[TearDown]
		public void TearDown()
		{
			File.Delete(_csvPath);
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.Run records a failing importer's path:line error, counts it, and still
		// runs the importers after it.
		// RCR: SpreadsheetImporterEditorUtils.cs Run — delete `report.Failed++;` → RED (Failed is 0). 2026-11-16
		public void Run_OneImporterFails_RecordsTheErrorAndRunsTheNextImporter()
		{
			_importer.When(importer => importer.Import(Arg.Any<List<Dictionary<string, string>>>()))
				.Do(_ => throw new SpreadsheetImportException(1, "bad row"));
			LogAssert.Expect(LogType.Error, $"{_csvPath}:3: bad row");

			var report = SpreadsheetImporterEditorUtils.Run(new[] { _importer, _otherImporter }, false, null);

			Assert.AreEqual(1, report.Failed);
			Assert.AreEqual($"{_csvPath}:3: bad row", report.Results[0].Error);
			Assert.AreEqual(2, report.Results[1].Rows);
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.Run reports how many data rows each importer imported.
		// RCR: SpreadsheetImporterEditorUtils.cs Run — change `result.Rows = Import(importer);` to `Import(importer);`
		// → RED (Rows is 0). 2026-11-16
		public void Run_SuccessfulImport_ReportsTheRowCountWithoutError()
		{
			var report = SpreadsheetImporterEditorUtils.Run(new[] { _importer }, false, null);

			Assert.AreEqual(0, report.Failed);
			Assert.AreEqual(2, report.Results[0].Rows);
			Assert.IsTrue(string.IsNullOrEmpty(report.Results[0].Error));
		}

		[Test]
		// ADMIT: ImportReport serializes its counters under the field names agents read from the JSON report.
		// RCR: ImportReport.cs ImportReport — change `public int Failed;` to `public int Failed { get; set; }`
		// → RED (the JSON has no Failed field). 2026-11-16
		public void ImportReport_ToJson_WritesTheReportFieldNames()
		{
			var report = new ImportReport { Failed = 1 };

			report.Results.Add(new ImportResult { Importer = "ItemsImporter", Rows = 2, Error = "x.csv:3: bad" });

			var json = JsonUtility.ToJson(report);

			StringAssert.Contains("\"Failed\":1", json);
			StringAssert.Contains("\"Importer\":\"ItemsImporter\"", json);
			StringAssert.Contains("\"Rows\":2", json);
			StringAssert.Contains("\"Error\":\"x.csv:3: bad\"", json);
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.SelectImporters keeps only the importers named on the command line.
		// RCR: SpreadsheetImporterEditorUtils.cs SelectImporters — change `if (names == null)` to `if (names != "")`
		// → RED (both importers are returned). 2026-11-16
		public void SelectImporters_OneName_ReturnsOnlyThatImporter()
		{
			Assume.That(_otherImporter.GetType().Name, Is.Not.EqualTo(_importer.GetType().Name));

			var selected = SpreadsheetImporterEditorUtils.SelectImporters(new[] { _importer, _otherImporter },
				_otherImporter.GetType().Name);

			CollectionAssert.AreEqual(new[] { _otherImporter }, selected);
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.SelectImporters fails on a name that matches no importer instead of
		// silently running fewer importers.
		// RCR: SpreadsheetImporterEditorUtils.cs SelectImporters — change `if (requested.Count > 0)` to
		// `if (requested.Count > 1)` → RED (no ArgumentException). 2026-11-16
		public void SelectImporters_UnknownName_ThrowsListingTheAvailableImporters()
		{
			var available = _importer.GetType().Name;

			var exception = Assert.Throws<ArgumentException>(() =>
				SpreadsheetImporterEditorUtils.SelectImporters(new[] { _importer }, $"{available}, Missing"));

			StringAssert.Contains("Missing", exception.Message);
			StringAssert.Contains($"available: {available}", exception.Message);
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.ArgValue returns the argument after the flag even when it starts with a
		// dash, as a Google spreadsheet id can.
		// RCR: SpreadsheetImporterEditorUtils.cs ArgValue — change `index + 1 >= args.Length` to
		// `index + 1 >= args.Length || args[index + 1].StartsWith("-")` → RED (ArgumentException). 2026-11-16
		public void ArgValue_ValueStartingWithDash_ReturnsIt()
		{
			var args = new[] { "Unity", "-spreadsheetId", "-abc_1", "-quit" };

			Assert.AreEqual("-abc_1", SpreadsheetImporterEditorUtils.ArgValue(args, "-spreadsheetId"));
		}

		[TestCase("/keys/arg.json", true, "/keys/env.json", "/keys/arg.json")]
		[TestCase(null, true, "/keys/env.json", null)]
		[TestCase(" ", false, "/keys/env.json", "/keys/env.json")]
		[TestCase(null, false, "", null)]
		// ADMIT: SpreadsheetImporterEditorUtils.ServiceAccountKeyFor lets a signed-in user win over GOOGLE_APPLICATION_CREDENTIALS
		// but not over an explicit command-line key.
		// RCR: SpreadsheetImporterEditorUtils.cs ServiceAccountKeyFor — change `if (userSignedIn)` to `if (false)` → RED (the
		// signed-in case returns env.json). 2026-11-16
		public void ServiceAccountKeyFor_Sources_PrefersCommandLineThenSignedInUserThenEnvironment(string argument,
			bool userSignedIn, string environment, string expected)
		{
			Assert.AreEqual(expected, SpreadsheetImporterEditorUtils.ServiceAccountKeyFor(argument, userSignedIn, environment));
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.SheetRange quotes a tab title and doubles its quotes, so a title such as A1 or
		// one with an apostrophe still selects the whole tab.
		// RCR: SpreadsheetImporterEditorUtils.cs SheetRange — change `title.Replace("'", "''")` to `title` → RED (the
		// apostrophe stays single). 2026-11-16
		public void SheetRange_TitleWithApostrophe_QuotesAndDoublesIt()
		{
			Assert.AreEqual("'Bob''s tab'", SpreadsheetImporterEditorUtils.SheetRange("Bob's tab"));
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.SheetRows pads the rows the Sheets API trims, so every CSV row has the header's
		// width like the CSV export.
		// RCR: SpreadsheetImporterEditorUtils.cs SheetRows — change `rows.Max(row => row.Length)` to
		// `rows.Min(row => row.Length)` → RED (ArgumentOutOfRangeException on the negative padding). 2026-11-16
		public void SheetRows_RaggedRows_PadsToTheWidestRow()
		{
			var rows = SpreadsheetImporterEditorUtils.SheetRows(JArray.Parse("[[\"Id\",\"Name\",\"Power\"],[\"1\"],[]]"));

			CollectionAssert.AreEqual(new[] { "Id", "Name", "Power" }, rows[0]);
			CollectionAssert.AreEqual(new[] { "1", "", "" }, rows[1]);
			CollectionAssert.AreEqual(new[] { "", "", "" }, rows[2]);
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.SheetRows reads an empty tab, for which the Sheets API omits "values", as no
		// rows, so the import reports the empty file instead of crashing.
		// RCR: SpreadsheetImporterEditorUtils.cs SheetRows — change `values?.Select` to `values.Select` → RED
		// (ArgumentNullException). 2026-11-16
		public void SheetRows_NoValues_ReturnsNoRows()
		{
			Assert.IsEmpty(SpreadsheetImporterEditorUtils.SheetRows(null));
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.ArgValue names the flag that is missing its value.
		// RCR: SpreadsheetImporterEditorUtils.cs ArgValue — change `index + 1 >= args.Length` to `index + 1 > args.Length`
		// → RED (IndexOutOfRangeException instead of ArgumentException). 2026-11-16
		public void ArgValue_FlagWithoutValue_ThrowsNamingTheFlag()
		{
			var exception = Assert.Throws<ArgumentException>(() =>
				SpreadsheetImporterEditorUtils.ArgValue(new[] { "Unity", "-spreadsheetReport" }, "-spreadsheetReport"));

			StringAssert.Contains("-spreadsheetReport", exception.Message);
		}

		[TestCase(Sheet + "/edit#gid=5", "5")]
		[TestCase(Sheet + "/edit?gid=7#gid=7", "7")]
		[TestCase(Sheet + "/edit?gid=9", "9")]
		[TestCase(Sheet + "/edit?usp=sharing", "0")]
		// ADMIT: SpreadsheetImporterEditorUtils.GoogleSheetCsvUrl finds the tab id after `#`, `?` or `&`, so a URL
		// copied from the browser in any current form exports the same tab.
		// RCR: SpreadsheetImporterEditorUtils.cs GoogleSheetCsvUrl — change `[#?&]gid=` to `[#]gid=` → RED
		// (the `/edit?gid=9` case exports gid=0). 2026-11-15
		public void GoogleSheetCsvUrl_BrowserUrlForms_ReturnsCsvExportOfTheSameTab(string url, string gid)
		{
			Assert.AreEqual($"{Sheet}/export?format=csv&gid={gid}", SpreadsheetImporterEditorUtils.GoogleSheetCsvUrl(url));
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.GoogleSheetCsvUrl accepts the account-scoped `/spreadsheets/u/<n>/d/` form.
		// RCR: SpreadsheetImporterEditorUtils.cs GoogleSheetCsvUrl — delete `(?:u/\d+/)?` from the id pattern → RED
		// (FormatException: not a Google Sheet URL). 2026-11-15
		public void GoogleSheetCsvUrl_AccountScopedUrl_ReturnsCsvExport()
		{
			var url = SpreadsheetImporterEditorUtils.GoogleSheetCsvUrl("https://docs.google.com/spreadsheets/u/1/d/SHEET_id-1/edit#gid=5");

			Assert.AreEqual($"{Sheet}/export?format=csv&gid=5", url);
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.GoogleSheetCsvUrl rejects a published-to-web URL instead of building an
		// export URL for the spreadsheet id "e".
		// RCR: SpreadsheetImporterEditorUtils.cs GoogleSheetCsvUrl — change `== PublishedUrlMarker` to `== null` → RED
		// (no FormatException is thrown). 2026-11-15
		public void GoogleSheetCsvUrl_PublishedToWebUrl_Throws()
		{
			Assert.Throws<System.FormatException>(() =>
				SpreadsheetImporterEditorUtils.GoogleSheetCsvUrl("https://docs.google.com/spreadsheets/d/e/2PACX-1vAbC/pubhtml"));
		}

		[Test]
		// ADMIT: SpreadsheetImporterEditorUtils.GoogleSheetCsvUrl swaps in a trimmed spreadsheet-id override and keeps
		// the tab id.
		// RCR: SpreadsheetImporterEditorUtils.cs GoogleSheetCsvUrl — change `: spreadsheetIdOverride.Trim();` to
		// `: id.Groups[1].Value;` → RED (the URL keeps SHEET_id-1). 2026-11-15
		public void GoogleSheetCsvUrl_SpreadsheetIdOverride_ReplacesOnlyTheSpreadsheetId()
		{
			var url = SpreadsheetImporterEditorUtils.GoogleSheetCsvUrl(Sheet + "/edit#gid=5", " COPY_id ");

			Assert.AreEqual("https://docs.google.com/spreadsheets/d/COPY_id/export?format=csv&gid=5", url);
		}
	}
}
