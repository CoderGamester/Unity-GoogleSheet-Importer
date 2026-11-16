using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;

namespace GameLovers.SpreadsheetImporter.Editor.Tests
{
	public class XlsxReaderTest
	{
		private const string Main = "xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"";
		private const string Relationships = "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
		private const string PackageRelationships = "xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"";

		private string _path;

		[SetUp]
		public void SetUp()
		{
			_path = Path.Combine(Path.GetTempPath(), $"{nameof(XlsxReaderTest)}-{Guid.NewGuid():N}.xlsx");
		}

		[TearDown]
		public void TearDown()
		{
			File.Delete(_path);
		}

		[Test]
		// ADMIT: XlsxReader.DateStyles treats the built-in date formats as dates, so a serial number in a
		// date-styled cell reads as an ISO date while the same number in a plain cell stays a number.
		// RCR: XlsxReader.cs DateStyles — change `(formatId >= 14 && formatId <= 22)` to start at 15 → RED
		// (rows[0][0] is "46037", not "2026-01-15"). 2026-11-15
		public void ReadSheet_DateStyledNumber_ReturnsIsoDate()
		{
			WriteWorkbook(Sheet("<row r=\"1\"><c r=\"A1\" s=\"1\"><v>46037</v></c><c r=\"B1\"><v>46037</v></c></row>"));

			var rows = XlsxReader.ReadSheet(_path);

			Assert.AreEqual(new[] { "2026-01-15", "46037" }, rows[0]);
		}

		[Test]
		// ADMIT: XlsxReader.DateStyles marks the built-in elapsed-time formats as time-only, so 1.5 in an
		// [h]:mm:ss cell reads as a duration instead of a date in 1900.
		// RCR: XlsxReader.cs DateStyles — change `(formatId >= 45 && formatId <= 47)` to start at 47 → RED
		// (rows[0][0] is "1.5", not "1.12:00:00"). 2026-11-15
		public void ReadSheet_ElapsedTimeStyledNumber_ReturnsDurationNotDate()
		{
			WriteWorkbook(Sheet("<row r=\"1\"><c r=\"A1\" s=\"2\"><v>1.5</v></c></row>"));

			var rows = XlsxReader.ReadSheet(_path);

			Assert.AreEqual(new[] { "1.12:00:00" }, rows[0]);
		}

		[Test]
		// ADMIT: XlsxReader.ReadRows places a cell by its reference, so a row that skips column B keeps C in the
		// third position.
		// RCR: XlsxReader.cs ReadRows — change the column lookup to `var column = nextColumn;` → RED
		// (the row is ["1", "3"], not ["1", "", "3"]). 2026-11-15
		public void ReadSheet_SparseCellReferences_KeepsColumnPositions()
		{
			WriteWorkbook(Sheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c><c r=\"C1\"><v>3</v></c></row>"));

			var rows = XlsxReader.ReadSheet(_path);

			Assert.AreEqual(new[] { "1", "", "3" }, rows[0]);
		}

		[Test]
		// ADMIT: XlsxReader.RichText joins the text runs of a formatted shared string.
		// RCR: XlsxReader.cs RichText — change the run test `element.Name.LocalName == "r"` to "q" → RED
		// (rows[0][0] is empty, not "Hello"). 2026-11-15
		public void ReadSheet_SharedRichText_JoinsTheTextRuns()
		{
			WriteWorkbook(Sheet("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row>"), "<si><r><t>Hel</t></r><r><t>lo</t></r></si>");

			var rows = XlsxReader.ReadSheet(_path);

			Assert.AreEqual(new[] { "Hello" }, rows[0]);
		}

		[Test]
		// ADMIT: XlsxReader.CellText rewrites a stored double through round-trip formatting, so the 1.1 that
		// Excel stores as 1.1000000000000001 reads as "1.1".
		// RCR: XlsxReader.cs CellText — change `return number.ToString("R", CultureInfo.InvariantCulture);` to
		// `return value;` → RED (rows[0][0] is "1.1000000000000001"). 2026-11-15
		public void ReadSheet_StoredDouble_ReturnsShortestRoundTripText()
		{
			WriteWorkbook(Sheet("<row r=\"1\"><c r=\"A1\"><v>1.1000000000000001</v></c></row>"));

			var rows = XlsxReader.ReadSheet(_path);

			Assert.AreEqual(new[] { "1.1" }, rows[0]);
		}

		[Test]
		// ADMIT: XlsxReader.CellText rejects a formula cell that has no calculated value instead of importing an
		// empty cell.
		// RCR: XlsxReader.cs CellText — invert `if (Child(cell, "f") != null)` to `== null` → RED
		// (no FormatException is thrown). 2026-11-15
		public void ReadSheet_FormulaWithoutCalculatedValue_Throws()
		{
			WriteWorkbook(Sheet("<row r=\"1\"><c r=\"A1\"><f>B1*2</f></c></row>"));

			Assert.Throws<FormatException>(() => XlsxReader.ReadSheet(_path));
		}

		[Test]
		// ADMIT: XlsxReader.SheetPath resolves a worksheet by its name through the workbook relationships.
		// RCR: XlsxReader.cs SheetPath — replace the `string.Equals(Attribute(s, "name"), sheetName,` comparison
		// with `true` → RED (the cell reads "1" from the first sheet, not "2"). 2026-11-15
		public void ReadSheet_SheetName_ReadsTheNamedSheet()
		{
			WriteWorkbook(Sheet("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>"), null,
				Sheet("<row r=\"1\"><c r=\"A1\"><v>2</v></c></row>"));

			var rows = XlsxReader.ReadSheet(_path, "Second");

			Assert.AreEqual(new[] { "2" }, rows[0]);
		}

		private static string Sheet(string rows)
		{
			return $"<worksheet {Main}><sheetData>{rows}</sheetData></worksheet>";
		}

		private void WriteWorkbook(string firstSheet, string sharedStringItems = null, string secondSheet = null)
		{
			var parts = new Dictionary<string, string>
			{
				{
					"xl/workbook.xml",
					$"<workbook {Main} {Relationships}><sheets><sheet name=\"First\" sheetId=\"1\" r:id=\"rId1\"/>" +
					"<sheet name=\"Second\" sheetId=\"2\" r:id=\"rId2\"/></sheets></workbook>"
				},
				{
					"xl/_rels/workbook.xml.rels",
					$"<Relationships {PackageRelationships}><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\"/>" +
					"<Relationship Id=\"rId2\" Target=\"worksheets/sheet2.xml\"/></Relationships>"
				},
				{
					"xl/styles.xml",
					$"<styleSheet {Main}><cellXfs count=\"3\"><xf numFmtId=\"0\"/><xf numFmtId=\"14\"/><xf numFmtId=\"46\"/></cellXfs></styleSheet>"
				},
				{ "xl/sharedStrings.xml", $"<sst {Main}>{sharedStringItems}</sst>" },
				{ "xl/worksheets/sheet1.xml", firstSheet },
				{ "xl/worksheets/sheet2.xml", secondSheet ?? Sheet(string.Empty) }
			};

			using var archive = new ZipArchive(File.Create(_path), ZipArchiveMode.Create);

			foreach (var part in parts)
			{
				using var writer = new StreamWriter(archive.CreateEntry(part.Key).Open(), new UTF8Encoding(false));

				writer.Write(part.Value);
			}
		}
	}
}
