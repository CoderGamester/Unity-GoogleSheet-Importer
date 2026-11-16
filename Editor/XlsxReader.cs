using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace GameLovers.SpreadsheetImporter.Editor
{
	/// <summary>Reads the cell values of one Excel worksheet without a third-party dependency.</summary>
	public static class XlsxReader
	{
		private const string WorkbookPath = "xl/workbook.xml";
		private const string WorkbookRelationshipsPath = "xl/_rels/workbook.xml.rels";
		private const string SharedStringsPath = "xl/sharedStrings.xml";
		private const string StylesPath = "xl/styles.xml";
		private const int FirstCustomFormatId = 164;

		/// <summary>
		/// Returns the non-empty rows of the worksheet named <paramref name="sheetName"/>, or of the first
		/// worksheet when the name is null or empty. Numbers and dates come back as invariant text, booleans as
		/// TRUE or FALSE, and a formula as its last calculated value.
		/// </summary>
		public static List<string[]> ReadSheet(string path, string sheetName = null)
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

			var workbook = Load(archive, WorkbookPath) ?? throw new InvalidDataException($"{path} is not an Excel workbook");
			var sheetPath = SheetPath(archive, workbook, sheetName, path);
			var sheet = Load(archive, sheetPath) ?? throw new InvalidDataException($"{path} has no worksheet part '{sheetPath}'");
			var workbookProperties = Child(workbook.Root, "workbookPr");
			var date1904 = workbookProperties != null && IsTrue(Attribute(workbookProperties, "date1904"));

			return ReadRows(sheet, SharedStrings(archive), DateStyles(archive), date1904, $"{path}#{sheetName}");
		}

		private static List<string[]> ReadRows(XDocument sheet, List<string> sharedStrings, Dictionary<int, bool> dateStyles,
											   bool date1904, string label)
		{
			var rows = new List<string[]>();

			foreach (var row in Children(Child(sheet.Root, "sheetData"), "row"))
			{
				var cells = new List<string>();
				var nextColumn = 0;

				foreach (var cell in Children(row, "c"))
				{
					var reference = Attribute(cell, "r");
					var column = reference == null ? nextColumn : ColumnIndex(reference);

					while (cells.Count <= column)
					{
						cells.Add(string.Empty);
					}

					cells[column] = CellText(cell, sharedStrings, dateStyles, date1904, $"{label}!{reference}");
					nextColumn = column + 1;
				}

				if (cells.Exists(text => text.Trim().Length > 0))
				{
					rows.Add(cells.ToArray());
				}
			}

			return rows;
		}

		private static string CellText(XElement cell, List<string> sharedStrings, Dictionary<int, bool> dateStyles,
									   bool date1904, string label)
		{
			var type = Attribute(cell, "t");
			var value = Child(cell, "v")?.Value;

			if (type == "inlineStr")
			{
				return RichText(Child(cell, "is"));
			}

			if (value == null)
			{
				if (Child(cell, "f") != null)
				{
					throw new FormatException($"{label} holds a formula without a calculated value: open and save the workbook in Excel");
				}

				return string.Empty;
			}

			if (type == "s")
			{
				return sharedStrings[int.Parse(value, CultureInfo.InvariantCulture)];
			}

			if (type == "b")
			{
				return value == "1" ? "TRUE" : "FALSE";
			}

			if (type == "e")
			{
				throw new FormatException($"{label} holds the error {value}");
			}

			if (type == "str" || type == "d")
			{
				return value;
			}

			var number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
			var style = Attribute(cell, "s");

			if (style != null && dateStyles.TryGetValue(int.Parse(style, CultureInfo.InvariantCulture), out var isTimeOnly))
			{
				return DateText(number, date1904, isTimeOnly);
			}

			return number.ToString("R", CultureInfo.InvariantCulture);
		}

		private static string DateText(double serial, bool date1904, bool isTimeOnly)
		{
			if (isTimeOnly)
			{
				return TimeSpan.FromSeconds(Math.Round(serial * 86400)).ToString("c", CultureInfo.InvariantCulture);
			}

			// Excel's 1900 system counts a 29 February 1900 that never existed, so serials from 61 are one day ahead.
			var epoch = date1904 ? new DateTime(1904, 1, 1) : new DateTime(1899, 12, serial < 61 ? 31 : 30);
			var date = epoch.AddSeconds(Math.Round(serial * 86400));

			return date.ToString(date.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		}

		private static string SheetPath(ZipArchive archive, XDocument workbook, string sheetName, string path)
		{
			var sheets = Children(Child(workbook.Root, "sheets"), "sheet").ToList();
			var sheet = string.IsNullOrEmpty(sheetName)
							? sheets.FirstOrDefault()
							: sheets.FirstOrDefault(s => string.Equals(Attribute(s, "name"), sheetName, StringComparison.OrdinalIgnoreCase));

			if (sheet == null)
			{
				throw new InvalidDataException(
					$"{path} has no worksheet '{sheetName}'; it has: {string.Join(", ", sheets.Select(s => Attribute(s, "name")))}");
			}

			var relationships = Load(archive, WorkbookRelationshipsPath);
			var relationship = Children(relationships?.Root, "Relationship").FirstOrDefault(r => Attribute(r, "Id") == Attribute(sheet, "id"));
			var target = relationship == null ? null : Attribute(relationship, "Target");

			if (target == null)
			{
				throw new InvalidDataException($"{path} does not say where worksheet '{Attribute(sheet, "name")}' is stored");
			}

			return target.StartsWith("/") ? target.Substring(1) : "xl/" + target;
		}

		private static List<string> SharedStrings(ZipArchive archive)
		{
			var document = Load(archive, SharedStringsPath);

			return Children(document?.Root, "si").Select(RichText).ToList();
		}

		// Maps the index of every date or time cell style to whether it shows a time without a date.
		private static Dictionary<int, bool> DateStyles(ZipArchive archive)
		{
			var styles = Load(archive, StylesPath);
			var dateStyles = new Dictionary<int, bool>();
			var customFormats = new Dictionary<int, bool>();
			var index = 0;

			foreach (var format in Children(Child(styles?.Root, "numFmts"), "numFmt"))
			{
				if (IsDateFormat(Attribute(format, "formatCode") ?? string.Empty, out var isTimeOnly))
				{
					customFormats[int.Parse(Attribute(format, "numFmtId"), CultureInfo.InvariantCulture)] = isTimeOnly;
				}
			}

			foreach (var style in Children(Child(styles?.Root, "cellXfs"), "xf"))
			{
				var formatId = int.Parse(Attribute(style, "numFmtId") ?? "0", CultureInfo.InvariantCulture);
				var isBuiltInTime = (formatId >= 18 && formatId <= 21) || (formatId >= 45 && formatId <= 47);
				var isBuiltInDate = (formatId >= 14 && formatId <= 22) || isBuiltInTime;

				if (formatId < FirstCustomFormatId && isBuiltInDate)
				{
					dateStyles[index] = isBuiltInTime;
				}
				else if (customFormats.TryGetValue(formatId, out var isTimeOnly))
				{
					dateStyles[index] = isTimeOnly;
				}

				index++;
			}

			return dateStyles;
		}

		private static bool IsDateFormat(string formatCode, out bool isTimeOnly)
		{
			var hasDate = false;
			var hasTime = false;
			var hasMonthOrMinute = false;

			for (var i = 0; i < formatCode.Length; i++)
			{
				var c = char.ToLowerInvariant(formatCode[i]);

				if (c == '\\' || c == '_' || c == '*')
				{
					i++;
				}
				else if (c == '"' || c == '[')
				{
					var end = formatCode.IndexOf(c == '"' ? '"' : ']', i + 1);
					var token = end < 0 ? string.Empty : formatCode.Substring(i + 1, end - i - 1).ToLowerInvariant();

					// A bracketed h, m or s run is elapsed time; other brackets hold colors, locales and conditions.
					hasTime |= c == '[' && token.Length > 0 && token.Trim('h', 'm', 's').Length == 0;
					i = end < 0 ? formatCode.Length : end;
				}
				else
				{
					hasDate |= c == 'd' || c == 'y';
					hasTime |= c == 'h' || c == 's';
					hasMonthOrMinute |= c == 'm';
				}
			}

			isTimeOnly = hasTime && !hasDate;

			return hasDate || hasTime || hasMonthOrMinute;
		}

		private static string RichText(XElement item)
		{
			var builder = new StringBuilder();

			foreach (var element in item?.Elements() ?? Enumerable.Empty<XElement>())
			{
				if (element.Name.LocalName == "t")
				{
					builder.Append(element.Value);
				}
				else if (element.Name.LocalName == "r")
				{
					builder.Append(Child(element, "t")?.Value);
				}
			}

			return builder.ToString();
		}

		private static int ColumnIndex(string cellReference)
		{
			var index = 0;

			foreach (var c in cellReference)
			{
				if (!char.IsLetter(c))
				{
					break;
				}

				index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
			}

			return index - 1;
		}

		private static XDocument Load(ZipArchive archive, string entryPath)
		{
			var entry = archive.GetEntry(entryPath);

			if (entry == null)
			{
				return null;
			}

			using var stream = entry.Open();

			return XDocument.Load(stream);
		}

		private static XElement Child(XElement parent, string name)
		{
			return Children(parent, name).FirstOrDefault();
		}

		// Matches by local name so both the transitional and the strict Open XML namespaces are read.
		private static IEnumerable<XElement> Children(XElement parent, string name)
		{
			return parent?.Elements().Where(element => element.Name.LocalName == name) ?? Enumerable.Empty<XElement>();
		}

		private static string Attribute(XElement element, string name)
		{
			return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value;
		}

		private static bool IsTrue(string value)
		{
			return value == "1" || value == "true";
		}
	}
}
