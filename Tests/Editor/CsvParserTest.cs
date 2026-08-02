using System;
using System.Collections.Generic;
using GameLovers.GoogleSheetImporter;
using NUnit.Framework;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace
// ReSharper disable UnassignedField.Global

namespace GameLoversEditor.GoogleSheetImporter.Tests
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
		// ADMIT: CsvParser.EnumerateCsvLine's regex keeps a double-quoted cell whole, so a quoted "1,2"
		// stays one value instead of splitting on its inner comma and shifting every later column.
		// RCR: CsvParser.cs EnumerateCsvLine — delete the quoted-cell alternative (the middle branch of the
		// three in the match pattern) → RED (dic[0].Count is no longer 10; dic[0]["Array"] is "1", not "1,2").
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
		// ADMIT: CsvParser.ConvertCsv splits rows on CRLF only, so a bare-LF sheet yields no data rows —
		// NewLineChars exists but is deliberately not used on this path.
		// RCR: CsvParser.cs ConvertCsv — change the row split to use `NewLineChars` instead of the CRLF-only
		// array → RED (dic.Count becomes 1, not 0).
		public void ConvertCsv_WrongFormatSeparator_EmptyResult()
		{
			var csv = "int,float\n" +
					  "1,1.1";
			var dic = CsvParser.ConvertCsv(csv);

			Assert.AreEqual(0, dic.Count);
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
		// ADMIT: a CSV column with no matching field on the target type is ignored.
		// RCR: no UNIQUE mutation exists — DeserializeTo iterates `type.GetFields()` and never consults a
		// key it has no field for, so surplus entries are unreachable by construction. The only edit that
		// reddens this test is the ContainsKey guard deletion already claimed by its sibling
		// Deserialize_MissingFields_Successfully (verified: that mutation reddens both). A5 duplicate —
		// deletion candidate, not a coverage asset.
		public void Deserialize_ExtraFields_Successfully()
		{
			var csv = "Int,Float,ExtraField\r\n" +
					  "1,1.1,extraValue";
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
		// ADMIT: CsvParser.Parse converts an unrecognised value type through Convert.ChangeType against the
		// REQUESTED type, so "1" comes back as a boxed int rather than as its source string.
		// RCR: CsvParser.cs Parse — change `Convert.ChangeType(text, type)` to
		// `Convert.ChangeType(text, typeof(string))` → RED (expected 1, was "1").
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