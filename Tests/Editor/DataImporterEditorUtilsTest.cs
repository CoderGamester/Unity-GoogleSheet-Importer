using NUnit.Framework;

namespace GameLovers.DataImporter.Editor.Tests
{
	public class DataImporterEditorUtilsTest
	{
		private const string Sheet = "https://docs.google.com/spreadsheets/d/SHEET_id-1";

		[TestCase(Sheet + "/edit#gid=5", "5")]
		[TestCase(Sheet + "/edit?gid=7#gid=7", "7")]
		[TestCase(Sheet + "/edit?gid=9", "9")]
		[TestCase(Sheet + "/edit?usp=sharing", "0")]
		// ADMIT: DataImporterEditorUtils.GoogleSheetCsvUrl finds the tab id after `#`, `?` or `&`, so a URL
		// copied from the browser in any current form exports the same tab.
		// RCR: DataImporterEditorUtils.cs GoogleSheetCsvUrl — change `[#?&]gid=` to `[#]gid=` → RED
		// (the `/edit?gid=9` case exports gid=0). 2026-11-15
		public void GoogleSheetCsvUrl_BrowserUrlForms_ReturnsCsvExportOfTheSameTab(string url, string gid)
		{
			Assert.AreEqual($"{Sheet}/export?format=csv&gid={gid}", DataImporterEditorUtils.GoogleSheetCsvUrl(url));
		}

		[Test]
		// ADMIT: DataImporterEditorUtils.GoogleSheetCsvUrl accepts the account-scoped `/spreadsheets/u/<n>/d/` form.
		// RCR: DataImporterEditorUtils.cs GoogleSheetCsvUrl — delete `(?:u/\d+/)?` from the id pattern → RED
		// (FormatException: not a Google Sheet URL). 2026-11-15
		public void GoogleSheetCsvUrl_AccountScopedUrl_ReturnsCsvExport()
		{
			var url = DataImporterEditorUtils.GoogleSheetCsvUrl("https://docs.google.com/spreadsheets/u/1/d/SHEET_id-1/edit#gid=5");

			Assert.AreEqual($"{Sheet}/export?format=csv&gid=5", url);
		}

		[Test]
		// ADMIT: DataImporterEditorUtils.GoogleSheetCsvUrl rejects a published-to-web URL instead of building an
		// export URL for the spreadsheet id "e".
		// RCR: DataImporterEditorUtils.cs GoogleSheetCsvUrl — change `== PublishedUrlMarker` to `== null` → RED
		// (no FormatException is thrown). 2026-11-15
		public void GoogleSheetCsvUrl_PublishedToWebUrl_Throws()
		{
			Assert.Throws<System.FormatException>(() =>
				DataImporterEditorUtils.GoogleSheetCsvUrl("https://docs.google.com/spreadsheets/d/e/2PACX-1vAbC/pubhtml"));
		}

		[Test]
		// ADMIT: DataImporterEditorUtils.GoogleSheetCsvUrl swaps in a trimmed spreadsheet-id override and keeps
		// the tab id.
		// RCR: DataImporterEditorUtils.cs GoogleSheetCsvUrl — change `: spreadsheetIdOverride.Trim();` to
		// `: id.Groups[1].Value;` → RED (the URL keeps SHEET_id-1). 2026-11-15
		public void GoogleSheetCsvUrl_SpreadsheetIdOverride_ReplacesOnlyTheSpreadsheetId()
		{
			var url = DataImporterEditorUtils.GoogleSheetCsvUrl(Sheet + "/edit#gid=5", " COPY_id ");

			Assert.AreEqual("https://docs.google.com/spreadsheets/d/COPY_id/export?format=csv&gid=5", url);
		}
	}
}
