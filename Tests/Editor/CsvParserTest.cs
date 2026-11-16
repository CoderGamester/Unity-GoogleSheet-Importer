using System;
using System.Collections.Generic;
using System.Globalization;
using GameLovers.DataImporter;
using GameLovers.DataImporter.Editor;
using NUnit.Framework;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace
// ReSharper disable UnassignedField.Global

namespace GameLovers.DataImporter.Editor.Tests
{
	public class CsvParserTest
	{
		public enum MockEnum
		{
			MockValue
		}
		public struct MockKeyValuePair
		{
			public int Key;
			public int Value;

			public MockKeyValuePair(int key, int value)
			{
				Key = key;
				Value = value;
			}
		}

		public class MockArrayClass
		{
			public int[] Array;
		}

		public struct MockReward
		{
			public int Id;
			public int Amount;
		}

		public class MockClass
		{
			[ParseIgnore]
			public string Ignored;
			public string String;
			public int Int;
			public float Float;
			public double Double;
			public MockEnum Enum;
			//public int[] Array;
			// public List<int> List;
			// public KeyValuePair<int,int> Pair;
			public Dictionary<int, int> Dictionary;
		}

		[Test]
		// ADMIT: CsvParser.SplitRecords keeps a double-quoted cell whole, so a quoted "1,2" stays one value
		// instead of splitting on its inner comma and shifting every later column.
		// RCR: CsvParser.cs SplitRecords — change `inQuotes = true;` to `inQuotes = false;` → RED
		// (dic[0]["Array"] is "1", not "1,2"). 2026-11-15
		public void ConvertCsv_Successfully()
		{
			var csv = "Ignored,String,Int,Float,Double,Enum,Array,List,Pair,Dictionary\r\n" +
					  "Ignored,text,1,1.1,1.1,MockValue,\"1,2\",\"1,2\",\"1:2\",\"1,2\"";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(1, dic.Count);
			Assert.AreEqual(10, dic[0].Count);
			Assert.AreEqual("Ignored", dic[0]["Ignored"]);
			Assert.AreEqual("text", dic[0]["String"]);
			Assert.AreEqual("1", dic[0]["Int"]);
			Assert.AreEqual("1.1", dic[0]["Float"]);
			Assert.AreEqual("1.1", dic[0]["Double"]);
			Assert.AreEqual("MockValue", dic[0]["Enum"]);
			Assert.AreEqual("1,2", dic[0]["Array"]);
			Assert.AreEqual("1,2", dic[0]["List"]);
			Assert.AreEqual("1:2", dic[0]["Pair"]);
			Assert.AreEqual("1,2", dic[0]["Dictionary"]);
		}

		[Test]
		// ADMIT: CsvParser.SplitRecords ends a row on a bare LF, so a file saved with LF line endings yields its
		// data rows instead of one header-only record.
		// RCR: CsvParser.cs SplitRecords — change `var isLineBreak = c == '\n' ||` to test '\f' instead → RED
		// (dic.Count is 0, not 1). 2026-11-15
		public void ConvertCsv_LfLineEndings_ParsesRows()
		{
			var csv = "int,float\n" +
					  "1,1.1\n";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(1, dic.Count);
			Assert.AreEqual("1", dic[0]["int"]);
			Assert.AreEqual("1.1", dic[0]["float"]);
		}

		[Test]
		// ADMIT: CsvParser.SplitRecords reads a doubled quote inside a quoted cell as one literal quote instead
		// of ending the cell.
		// RCR: CsvParser.cs SplitRecords — change the doubled-quote test `csv[i + 1] == '"'` to compare with '\f'
		// → RED (dic[0]["quote"] is `say hi`, not `say "hi"`). 2026-11-15
		public void ConvertCsv_QuotedCellWithLineBreakAndDoubledQuotes_KeepsCellText()
		{
			var csv = "note,quote\r\n" +
					  "\"line1\nline2\",\"say \"\"hi\"\"\"";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(1, dic.Count);
			Assert.AreEqual("line1\nline2", dic[0]["note"]);
			Assert.AreEqual("say \"hi\"", dic[0]["quote"]);
		}

		[Test]
		// ADMIT: CsvParser.SplitRecords opens a quoted cell only at the start of a cell, so a quote inside plain
		// text stays text instead of swallowing the cells that follow.
		// RCR: CsvParser.cs SplitRecords — change `if (c == '"' && field.Length == 0)` to `if (c == '"')` → RED
		// (FormatException: unterminated quoted cell). 2026-11-15
		public void ConvertCsv_QuoteInsideUnquotedCell_IsKeptAsText()
		{
			var csv = "size,name\n" +
					  "6\" pipe,\"a,b\"";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual("6\" pipe", dic[0]["size"]);
			Assert.AreEqual("a,b", dic[0]["name"]);
		}

		[Test]
		// ADMIT: CsvParser.SplitRecords counts line breaks inside quoted cells, so ConvertCsv reports the file
		// line each row starts on even after a multi-line cell and a blank line.
		// RCR: CsvParser.cs SplitRecords — change `line += isLineBreak ? 1 : 0;` to `line += 0;` → RED
		// (the second row is reported on line 4, not 5). 2026-11-15
		public void ConvertCsv_RowLines_ReportsFileLineOfEachRow()
		{
			var csv = "note\n" +
					  "\"line1\nline2\"\n" +
					  "\n" +
					  "last";
			var lines = new List<int>();
			var dic = CsvParser.ConvertCsv(csv, lines);

			Assert.AreEqual(2, dic.Count);
			Assert.AreEqual(new[] { 2, 5 }, lines);
		}

		[Test]
		// ADMIT: CsvParser.ToCsv doubles the quotes of a cell it wraps in quotes, so the cell reads back intact.
		// RCR: CsvParser.cs ToCsv — change `cell.Replace("\"", "\"\"")` to replace a quote with one quote → RED
		// (the csv holds `"say "hi""`, not `"say ""hi"""`). 2026-11-15
		public void ToCsv_CellsNeedingQuotes_AreQuotedAndReadBack()
		{
			var row = new[] { "plain", "a,b", "say \"hi\"", " padded " };
			var csv = CsvParser.ToCsv(new[] { new[] { "A", "B", "C", "D" }, row });
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual("A,B,C,D\nplain,\"a,b\",\"say \"\"hi\"\"\",\" padded \"\n", csv);
			Assert.AreEqual("a,b", dic[0]["B"]);
			Assert.AreEqual("say \"hi\"", dic[0]["C"]);
		}

		[Test]
		// ADMIT: CsvParser.ValidateColumns reports a field with no column unless the field carries [ParseIgnore].
		// RCR: CsvParser.cs ValidateColumns — change the attribute test `.Length == 0` to `.Length == 1` → RED
		// (problems[0] names the ignored field 'Ignored', not 'Dictionary'). 2026-11-15
		public void ValidateColumns_UnknownColumnAndMissingField_ReportsBothAndSkipsIgnoredField()
		{
			var problems = CsvParser.ValidateColumns(typeof(MockClass),
				new[] { "String", "Int", "Float", "Double", "Enum", "Extra" });

			Assert.AreEqual(2, problems.Count);
			StringAssert.StartsWith("Missing column 'Dictionary'", problems[0]);
			StringAssert.StartsWith("Unknown column 'Extra'", problems[1]);
		}

		[Test]
		// ADMIT: CsvParser.ConvertCsv starts its row loop at index 1, treating line 0 as the header, so a
		// header-only sheet produces no rows.
		// RCR: CsvParser.cs ConvertCsv — change `for (var i = 1; i < lines.Length; i++)` to start at 0 → RED
		// (dic.Count becomes 1, not 0; the header line is emitted as data).
		public void ConvertCsv_OnlyHeadlines_EmptyResult()
		{
			var csv = "int,float,double,enum,pair";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(0, dic.Count);
		}

		[Test]
		// ADMIT: CsvParser.ConvertCsv pads a short data row with an empty string for every header past the
		// row's value count, rather than indexing past the end of the row.
		// RCR: CsvParser.cs ConvertCsv — change the `if (j >= values.Length)` pad branch to `if (false)` →
		// RED (IndexOutOfRangeException from `values[j]` on the missing "double" column).
		public void ConvertCsv_MissMatchColumnsCount_FillsWithDefaultData()
		{
			var csv = "int,float,double\r\n" +
					  "1,1.1";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual("", dic[0]["double"]);
		}

		[Test]
		// ADMIT: CsvParser.DeserializeTo skips any field carrying [ParseIgnore], so a CSV column whose name
		// matches an ignored field is never written to the instance.
		// RCR: CsvParser.cs DeserializeTo — change the attribute test `.Length == 1` to `.Length == 2` (never
		// true) → RED (result.Ignored is "Ignored" instead of null).
		public void Deserialize_Successfully()
		{
			var csv = "Ignored,String,Int,Float,Double,Enum,Array,List,Pair,Dictionary\r\n" +
					  "Ignored,text,1,1.1,1.1,MockValue,\"1,2\",\"1,2\",\"1:2\",\"1,2\"";
			var dic = CsvParser.ConvertCsv(csv);
			var result = CsvParser.DeserializeTo<MockClass>(dic[0]);

			Assert.AreEqual(null, result.Ignored);
			Assert.AreEqual("text", result.String);
			Assert.AreEqual(1, result.Int);
			Assert.AreEqual(1.1f, result.Float);
			Assert.AreEqual(1.1d, result.Double);
			Assert.AreEqual(MockEnum.MockValue, result.Enum);
			//Assert.AreEqual(new[] {1, 2}, result.Array);
			// Assert.AreEqual(new List<int> {1, 2}, result.List);
			// Assert.AreEqual(new KeyValuePair<int, int>(1, 2), result.Pair);
			Assert.AreEqual(new Dictionary<int, int> { { 1, 2 } }, result.Dictionary);
		}

		[Test]
		// ADMIT: CsvParser.DeserializeTo skips a field the CSV has no column for, leaving it at its default
		// rather than faulting on the absent dictionary key.
		// RCR: CsvParser.cs DeserializeTo — delete `if (!data.ContainsKey(field.Name)) { continue; }` → RED
		// (KeyNotFoundException on the first field with no matching column).
		public void Deserialize_MissingFields_Successfully()
		{
			var csv = "Int,Float\r\n" +
					  "1,1.1";
			var dic = CsvParser.ConvertCsv(csv);
			var result = CsvParser.DeserializeTo<MockClass>(dic[0]);

			Assert.AreEqual(null, result.String);
			Assert.AreEqual(1, result.Int);
			Assert.AreEqual(1.1f, result.Float);
			Assert.AreEqual(0, result.Double);
			Assert.AreEqual(MockEnum.MockValue, result.Enum);
			//Assert.AreEqual(null, result.Array);
			// Assert.AreEqual(null, result.List);
			// Assert.AreEqual(new KeyValuePair<int, int>(), result.Pair);
			Assert.AreEqual(null, result.Dictionary);
		}

		[Test]
		// ADMIT: CsvParser.ArrayParse splits on every bracket form in ArraySplitChars, so `1,[2],{3,4},(5),6`
		// flattens to six elements rather than treating brackets as literal text.
		// RCR: CsvParser.cs ArraySplitChars — remove '[' and ']' from the array → RED (the "[2]" element no
		// longer splits, so Parse<int> throws FormatException on it).
		public void ArrayParse_Successfully()
		{
			var result = CsvParser.ArrayParse<int>("1,[2],{3,4},(5),6");

			Assert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, result);
		}

		// [Test]
		// public void ArrayParsePair_Successfully()
		// {
		// 	var result = CsvParser.ArrayParse<KeyValuePair<int,int>>("1:2,(3 < 4),[5 > 6],{7 = 8}");
		// 	var pairArray = new[]
		// 	{
		// 		new KeyValuePair<int,int>(1,2),
		// 		new KeyValuePair<int,int>(3,4),
		// 		new KeyValuePair<int,int>(5,6),
		// 		new KeyValuePair<int,int>(7,8), 
		// 	};
		// 	
		// 	Assert.AreEqual(pairArray, result);
		// }

		// [Test]
		// public void ArrayParsePair_ElementOddAmount_ThrowsException()
		// {
		// 	Assert.Throws<IndexOutOfRangeException>(() => CsvParser.ArrayParse<KeyValuePair<int,int>>("1:2,(3 < 4),5"));
		// }

		[Test]
		// ADMIT: CsvParser.DictionaryParse consumes a flat, non-paired cell two values at a time as
		// key/value, so `1,2,3,4` becomes {1:2, 3:4}.
		// RCR: CsvParser.cs DictionaryParse — change the flat-pair loop step `i += 2` to `i += 1` → RED
		// (ArgumentException on a duplicate key: {1:2} then {2:3} then {3:4} collide).
		public void DictionaryParse_Successfully()
		{
			var result = CsvParser.DictionaryParse<int, int>("1,2,3,4");

			Assert.AreEqual(new Dictionary<int, int> { { 1, 2 }, { 3, 4 } }, result);
		}

		[Test]
		// ADMIT: CsvParser.DictionaryParse detects the paired form by scanning the FIRST element for any
		// PairSplitChars character, and only then splits each element on that set.
		// RCR: CsvParser.cs DictionaryParse — invert `items[0].IndexOfAny(PairSplitChars) != -1` to `== -1` →
		// RED (takes the flat branch; "1:2" is parsed as a single int and throws FormatException).
		public void DictionaryParsePair_Successfully()
		{
			var result = CsvParser.DictionaryParse<int, int>("1:2,(3>4)");

			Assert.AreEqual(new Dictionary<int, int> { { 1, 2 }, { 3, 4 } }, result);
		}

		[Test]
		// ADMIT: CsvParser.DictionaryParse rejects a flat cell with an odd value count instead of silently
		// dropping the trailing unpaired value.
		// RCR: CsvParser.cs DictionaryParse — change the guard `items.Count % 2 == 1` to `== 3` (never true
		// for this input) → RED (Assert.Throws<IndexOutOfRangeException> fails: no exception thrown).
		public void DictionaryParse_ElementOddAmount_ThrowsException()
		{
			Assert.Throws<IndexOutOfRangeException>(() => CsvParser.DictionaryParse<int, int>("1,2,3"));
		}

		[Test]
		// ADMIT: CsvParser.DeserializeTo names the column and the cell text when a cell cannot be converted.
		// RCR: CsvParser.cs DeserializeTo — change the message prefix `Column '{field.Name}': cannot read` to
		// `Field: cannot read` → RED (the message starts with "Field: cannot read 'abc' as Int32"). 2026-11-15
		public void Deserialize_UnreadableCell_NamesColumnAndValue()
		{
			var data = new Dictionary<string, string> { { "Int", "abc" } };
			var exception = Assert.Throws<FormatException>(() => CsvParser.DeserializeTo<MockClass>(data));

			StringAssert.StartsWith("Column 'Int': cannot read 'abc' as Int32", exception.Message);
		}

		[Test]
		// ADMIT: CsvParser.DeserializeObject converts the parsed list into an array for an array field instead
		// of handing a List<T> to a T[] field.
		// RCR: CsvParser.cs DeserializeObject — change `list.CopyTo(array, 0);` to `list.Clear();` → RED
		// (result.Array[0] is 0, not 1). 2026-11-15
		public void Deserialize_ArrayField_ReturnsArrayOfTheCellValues()
		{
			var data = new Dictionary<string, string> { { "Array", "1,2,3" } };
			var result = CsvParser.DeserializeTo<MockArrayClass>(data);

			Assert.AreEqual(new[] { 1, 2, 3 }, result.Array);
		}

		[Test]
		// ADMIT: CsvParser.GetSubListDictionary skips blank cells of a sub-list header row, so unused columns do
		// not collide on an empty sub-field name.
		// RCR: CsvParser.cs GetSubListDictionary — delete `value.Length == 0 ||` from the skip test → RED
		// (ArgumentException: an item with the same key has already been added). 2026-11-15
		public void DeserializeSubList_BlankHeaderCells_AreSkipped()
		{
			var data = new List<Dictionary<string, string>>
			{
				new() { { "Key", "Rewards[]" }, { "Value", "Id" }, { "C", "Amount" }, { "D", "" }, { "E", "" } },
				new() { { "Key", "#" }, { "Value", "7" }, { "C", "10" }, { "D", "" }, { "E", "" } }
			};
			var result = (List<MockReward>)CsvParser.DeserializeSubList(data, 0, typeof(List<MockReward>), "Rewards");

			Assert.AreEqual(1, result.Count);
			Assert.AreEqual(7, result[0].Id);
			Assert.AreEqual(10, result[0].Amount);
		}

		[Test]
		// ADMIT: CsvParser.Parse converts value types with the invariant culture, so "1.5" reads as 1.5 on a
		// machine whose regional settings use a decimal comma.
		// RCR: CsvParser.cs Parse — change `Convert.ChangeType(text, type, CultureInfo.InvariantCulture)` to
		// `Convert.ChangeType(text, type)` → RED (FormatException under fi-FI). 2026-11-15
		public void ParseFloat_CommaDecimalCulture_ReadsInvariantText()
		{
			var culture = CultureInfo.CurrentCulture;

			try
			{
				CultureInfo.CurrentCulture = new CultureInfo("fi-FI");

				Assert.AreEqual(1.5f, CsvParser.Parse<float>("1.5"));
			}
			finally
			{
				CultureInfo.CurrentCulture = culture;
			}
		}

		[Test]
		// ADMIT: CsvParser.Parse converts an unrecognised value type through Convert.ChangeType against the
		// REQUESTED type, so "1" comes back as a boxed int rather than as its source string.
		// RCR: CsvParser.cs Parse — change `Convert.ChangeType(text, type, CultureInfo.InvariantCulture)` to convert
		// to `typeof(string)` instead → RED (InvalidCastException unboxing the string as int). 2026-11-15
		public void ParseInt_Successfully()
		{
			var result = CsvParser.Parse<int>("1");

			Assert.AreEqual(1, result);
		}

		[Test]
		// ADMIT: CsvParser.Parse rethrows a conversion failure rather than swallowing it and returning a
		// default, so a malformed cell fails the import loudly instead of importing a silent zero.
		// RCR: CsvParser.cs Parse — replace the bare `throw;` in the Convert.ChangeType catch with
		// `return 0;` → RED (Assert.Throws<FormatException> fails: no exception thrown).
		public void ParseInt_WithFloat_ThrowsException()
		{
			Assert.Throws<FormatException>(() => CsvParser.Parse<int>("1.1f"));
		}

		// [Test]
		// public void ParsePair_Successfully()
		// {
		// 	var pair1 = CsvParser.Parse<KeyValuePair<int, int>>("1:2");
		// 	var pair2 = CsvParser.Parse<MockKeyValuePair>("1:2");
		// 	var result1 = new KeyValuePair<int, int>(1, 2);
		// 	var result2 = new MockKeyValuePair(1, 2);
		// 	
		// 	Assert.AreEqual(result1, pair1);
		// 	Assert.AreEqual(result2, pair2);
		// }
		//
		// [Test]
		// public void ParsePair_OneElement_ThrowsException()
		// {
		// 	Assert.Throws<IndexOutOfRangeException>(() => CsvParser.Parse<KeyValuePair<int, int>>("1"));
		// }
	}
}