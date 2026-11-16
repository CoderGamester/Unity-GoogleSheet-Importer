using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using GameLovers.GameData;
using Newtonsoft.Json;

namespace GameLovers.DataImporter.Editor
{
	/// <summary>Reads and writes CSV text and converts cell values into typed fields.</summary>
	public static class CsvParser
	{
		public const string IGNORE_COLUMN_CHAR = "$";
		public const string IGNORE_FIELD_CHAR = "#";
		public const string SUB_LIST_SUFFIX = "[]";

		public static readonly char[] PairSplitChars = { ':', '<', '>', '=', '|' };
		public static readonly char[] ArraySplitChars = { ',', '(', ')', '[', ']', '{', '}' };

		private static readonly char[] _quotedCellChars = { ',', '"', '\r', '\n' };

		/// <summary>
		/// Parses the entire <paramref name="csv"/> text.
		/// Each row is an element on the returned list
		/// Each column is an element on the returned dictionary. The dictionary key will be the CSV header
		/// </summary>
		public static List<Dictionary<string, string>> ConvertCsv(string csv)
		{
			return ConvertCsv(csv, null);
		}

		/// <summary>
		/// Parses the entire <paramref name="csv"/> text and adds the 1-based file line of every returned row
		/// to <paramref name="rowLines"/>, so a failure can be reported against the source file.
		/// </summary>
		public static List<Dictionary<string, string>> ConvertCsv(string csv, List<int> rowLines)
		{
			var recordLines = new List<int>();
			var lines = SplitRecords(csv, recordLines);
			var list = new List<Dictionary<string, string>>(Math.Max(lines.Length - 1, 0));

			if (lines.Length == 0)
			{
				return list;
			}

			var headlines = lines[0];

			for (var j = 0; j < headlines.Length; j++)
			{
				headlines[j] = headlines[j].Trim();
			}

			for (var i = 1; i < lines.Length; i++)
			{
				var dictionary = new Dictionary<string, string>(headlines.Length);
				var values = lines[i];

				for (var j = 0; j < headlines.Length; j++)
				{
					if (headlines[j].Length == 0 || headlines[j].StartsWith(IGNORE_COLUMN_CHAR))
					{
						continue;
					}

					if (dictionary.ContainsKey(headlines[j]))
					{
						throw new FormatException($"Duplicate column '{headlines[j]}'");
					}

					if (j >= values.Length)
					{
						dictionary.Add(headlines[j], "");
					}
					else
					{
						dictionary.Add(headlines[j], values[j].Trim());
					}
				}

				list.Add(dictionary);
				rowLines?.Add(recordLines[i]);
			}

			return list;
		}

		/// <summary>
		/// Writes <paramref name="rows"/> as CSV text with LF line endings, quoting cells only when needed.
		/// </summary>
		public static string ToCsv(IEnumerable<IReadOnlyList<string>> rows)
		{
			var builder = new StringBuilder();

			foreach (var row in rows)
			{
				for (var i = 0; i < row.Count; i++)
				{
					var cell = row[i] ?? string.Empty;

					if (i > 0)
					{
						builder.Append(',');
					}

					if (cell.IndexOfAny(_quotedCellChars) >= 0 || cell.Trim().Length != cell.Length)
					{
						builder.Append('"').Append(cell.Replace("\"", "\"\"")).Append('"');
					}
					else
					{
						builder.Append(cell);
					}
				}

				builder.Append('\n');
			}

			return builder.ToString();
		}

		/// <summary>
		/// Compares the <paramref name="columns"/> of a sheet against the public fields of <paramref name="type"/>
		/// and describes every column without a field and every field without a column.
		/// </summary>
		public static List<string> ValidateColumns(Type type, IEnumerable<string> columns)
		{
			var problems = new List<string>();
			var present = new List<string>(columns);
			var fieldNames = new HashSet<string>();

			foreach (var field in type.GetFields())
			{
				if (field.IsStatic)
				{
					continue;
				}

				fieldNames.Add(field.Name);

				if (!present.Contains(field.Name) && field.GetCustomAttributes(typeof(ParseIgnoreAttribute), false).Length == 0)
				{
					problems.Add($"Missing column '{field.Name}' for {type.Name}.{field.Name}");
				}
			}

			foreach (var column in present)
			{
				if (!fieldNames.Contains(column))
				{
					problems.Add($"Unknown column '{column}': {type.Name} has no public field with that name " +
								 $"(prefix the header with '{IGNORE_COLUMN_CHAR}' to ignore the column)");
				}
			}

			return problems;
		}

		/// <inheritdoc cref="DeserializeTo"/>
		public static T DeserializeTo<T>(Dictionary<string, string> data,
										 params Func<string, Type, object>[] deserializers)
		{
			return (T)DeserializeTo(typeof(T), data, deserializers);
		}

		/// <summary>
		/// Deserializes the given CSV <paramref name="data"/> cell values to an object of the given <paramref name="type"/> type
		/// </summary>
		/// <remarks>
		/// It provides extra custom <paramref name="deserializers"/> to specific parsing
		/// </remarks>
		public static object DeserializeTo(Type type, Dictionary<string, string> data,
										   params Func<string, Type, object>[] deserializers)
		{
			var ignoreType = typeof(ParseIgnoreAttribute);
			var instance = Activator.CreateInstance(type);

			foreach (var field in type.GetFields())
			{
				if (!data.ContainsKey(field.Name))
				{
					continue;
				}

				if (field.GetCustomAttributes(ignoreType, false).Length == 1)
				{
					continue;
				}

				object value;

				try
				{
					value = DeserializeObject(data[field.Name], field.FieldType, deserializers);
				}
				catch (Exception e)
				{
					throw new FormatException(
						$"Column '{field.Name}': cannot read '{data[field.Name]}' as {field.FieldType.Name} ({e.Message})", e);
				}

				field.SetValue(instance, value);
			}

			return instance;
		}

		/// <summary>
		/// Deserializes the given <paramref name="data"/> to the given <paramref name="type"/> in a <see cref="object"/>
		/// format as a result to be set via reflection.
		/// </summary>
		/// <remarks>
		/// It provides extra custom <paramref name="deserializers"/> to specific parsing
		/// </remarks>
		public static object DeserializeObject(string data, Type type,
											   params Func<string, Type, object>[] deserializers)
		{
			var listType = typeof(IList);
			var dictionaryType = typeof(IDictionary);
			var unityDictionaryType = typeof(UnitySerializedDictionary<,>).GetGenericTypeDefinition();

			if (type.IsArray)
			{
				var elementType = type.GetElementType();
				var list = (IList)ArrayParse(data, elementType, deserializers);
				var array = Array.CreateInstance(elementType, list.Count);

				list.CopyTo(array, 0);

				return array;
			}

			if (listType.IsAssignableFrom(type))
			{
				return ArrayParse(data, type.GenericTypeArguments[0], deserializers);
			}

			if (type.BaseType != null && type.BaseType.IsGenericType && type.BaseType.GetGenericTypeDefinition()
					.IsAssignableFrom(unityDictionaryType))
			{
				var types = type.BaseType.GenericTypeArguments;
				return DictionaryParse(data, types[0], types[1], type, deserializers);
			}

			if (dictionaryType.IsAssignableFrom(type))
			{
				var types = type.GenericTypeArguments;
				var keyType = types[0];
				var valueType = types[1];
				return DictionaryParse(data, keyType, valueType,
					typeof(Dictionary<,>).MakeGenericType(keyType, valueType), deserializers);
			}

			return Parse(data, type, deserializers);
		}

		/// <summary>
		/// Deserializes a list of custom complex (non-primitive) types, with their own headers / definitions.
		/// </summary>
		public static object DeserializeSubList(List<Dictionary<string, string>> data, int startIndex, Type type,
												string fieldName,
												params Func<string, Type, object>[] deserializers)
		{
			var subType = type.GetGenericArguments()[0];
			var subData = GetSubListDictionary(data, startIndex);

			var listType = typeof(List<>).MakeGenericType(subType);
			var list = Activator.CreateInstance(listType);
			var addMethod = listType.GetMethod("Add")!;

			foreach (var dict in subData)
			{
				addMethod.Invoke(list, new[] { DeserializeTo(subType, dict, deserializers) });
			}

			return list;
		}

		private static List<Dictionary<string, string>> GetSubListDictionary(
			List<Dictionary<string, string>> data, int startIndex)
		{
			var headerMap = new Dictionary<string, string>();

			foreach (var (key, value) in data[startIndex])
			{
				if (value.Length == 0 || value.EndsWith(SUB_LIST_SUFFIX)) continue;

				headerMap.Add(key, value);
			}

			var objData = new List<Dictionary<string, string>>();
			for (int i = startIndex + 1; i < data.Count; i++)
			{
				var dataLine = data[i];

				if (dataLine["Key"] != IGNORE_FIELD_CHAR) break;

				var rowData = new Dictionary<string, string>();

				foreach (var (key, value) in dataLine)
				{
					if (headerMap.TryGetValue(key, out var header))
					{
						rowData.Add(header, value);
					}
				}

				objData.Add(rowData);
			}

			return objData;
		}

		/// <summary>
		/// Parses the given <paramref name="text"/> into a possible array of the given <typeparamref name="T"/>
		/// A text is in array format as long as is divided by ',', '{}', '()' or '[]' (ex: 1,2,3; {1,2}{4,5}, [1,2,3])
		/// If the given <paramref name="text"/> is not in an array format, it will return an array with <paramref name="text"/> as the only element
		/// </summary>
		/// <remarks>
		/// It provides extra custom <paramref name="deserializers"/> to specific parsing
		/// </remarks>
		public static List<T> ArrayParse<T>(string text, params Func<string, Type, object>[] deserializers)
		{
			return ArrayParse(text, typeof(T), deserializers) as List<T>;
		}

		/// <inheritdoc cref="ArrayParse{T}" />
		public static object ArrayParse(string data, Type type, params Func<string, Type, object>[] deserializers)
		{
			var split = data.Split(ArraySplitChars, StringSplitOptions.RemoveEmptyEntries);
			var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));

			foreach (var value in split)
			{
				list.Add(Parse(value, type, deserializers));
			}

			return list;
		}

		/// <summary>
		/// Parses the given <paramref name="text"/> into a <seealso cref="Dictionary{TKey, TValue}"/> type.
		/// A text is in <seealso cref="Dictionary{TKey, TValue}"/> type format if follows the same rules
		/// of <seealso cref="ArrayParse{T}"/> and has at least 2 elements inside
		/// If the given <paramref name="text"/> is not in an <seealso cref="Dictionary{TKey, TValue}"/> type format,
		/// it will return an empty dictionary
		/// </summary>
		/// <remarks>
		/// It provides extra custom <paramref name="deserializers"/> to specific parsing
		/// </remarks>
		public static Dictionary<TKey, TValue> DictionaryParse<TKey, TValue>(
			string text, params Func<string, Type, object>[] deserializers)
		{
			var keyType = typeof(TKey);
			var valueType = typeof(TValue);
			return DictionaryParse(text, keyType, valueType, typeof(Dictionary<,>).MakeGenericType(keyType, valueType),
				deserializers) as Dictionary<TKey, TValue>;
		}

		private static object DictionaryParse(string text, Type keyType, Type valueType, Type dictionaryType,
											  params Func<string, Type, object>[] deserializers)
		{
			var items = ArrayParse<string>(text, deserializers);
			var dictionary = (IDictionary)Activator.CreateInstance(dictionaryType);

			if (items.Count == 0)
			{
				return null;
			}

			if (items[0].IndexOfAny(PairSplitChars) != -1)
			{
				foreach (var item in items)
				{
					var split = item.Split(PairSplitChars);
					var key = Parse(split[0], keyType, deserializers);
					var value = Parse(split[1], valueType, deserializers);

					dictionary.Add(key, value);
				}
			}
			else if (items.Count % 2 == 1)
			{
				throw new
					IndexOutOfRangeException($"Dictionary must have an even amount of values and the following text" +
						$"has {items.Count.ToString()} values. \nText:{text}");
			}
			else
			{
				for (var i = 0; i < items.Count; i += 2)
				{
					var key = Parse(items[i], keyType, deserializers);
					var value = Parse(items[i + 1], valueType, deserializers);

					dictionary.Add(key, value);
				}
			}

			return dictionary;
		}

		/// <summary>
		/// Parses the given <paramref name="text"/> to the given <typeparamref name="T"/> type
		/// </summary>
		/// <remarks>
		/// It provides extra custom <paramref name="deserializers"/> to specific parsing
		/// </remarks>
		public static T Parse<T>(string text, params Func<string, Type, object>[] deserializers)
		{
			return (T)Parse(text, typeof(T), deserializers);
		}

		/// <inheritdoc cref="Parse{T}" />
		public static object Parse(string text, Type type, params Func<string, Type, object>[] deserializers)
		{
			text = text.Trim();

			if (type == typeof(string))
			{
				return text;
			}

			if (type.IsEnum)
			{
				return Enum.Parse(type, text);
			}

			if (TryGetKeyValuePair(text, type, out var key, out var value, deserializers))
			{
				return Activator.CreateInstance(type, key, value);
			}

			deserializers ??= Array.Empty<Func<string, Type, object>>();

			foreach (var func in deserializers)
			{
				var res = func(text, type);

				if (res != null)
				{
					return res;
				}
			}

			if (type == typeof(DateTime))
			{
				return DateTime.Parse(text, CultureInfo.InvariantCulture);
			}

			if (type == typeof(TimeSpan))
			{
				return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
			}

			//Handling Nullable types i.e, int?, double?, bool? .. etc
			if (type.IsValueType && Nullable.GetUnderlyingType(type) != null)
			{
				// ReSharper disable once PossibleNullReferenceException
				return TypeDescriptor.GetConverter(type).ConvertFromInvariantString(text);
			}

			try
			{
				if (type.IsValueType)
				{
					return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw;
			}

			return JsonConvert.DeserializeObject($"\"{text}\"", type);
		}

		private static string[][] SplitRecords(string csv, List<int> recordLines)
		{
			var records = new List<string[]>();
			var fields = new List<string>();
			var field = new StringBuilder();
			var inQuotes = false;
			var line = 1;
			var recordLine = 1;

			void EndRecord()
			{
				fields.Add(field.ToString());
				field.Clear();

				if (fields.Exists(cell => cell.Trim().Length > 0))
				{
					records.Add(fields.ToArray());
					recordLines.Add(recordLine);
				}

				fields.Clear();
			}

			for (var i = csv.Length > 0 && csv[0] == '﻿' ? 1 : 0; i < csv.Length; i++)
			{
				var c = csv[i];
				var isLineBreak = c == '\n' || (c == '\r' && (i + 1 == csv.Length || csv[i + 1] != '\n'));

				if (inQuotes)
				{
					if (c != '"')
					{
						field.Append(c);
						line += isLineBreak ? 1 : 0;
					}
					else if (i + 1 < csv.Length && csv[i + 1] == '"')
					{
						field.Append('"');
						i++;
					}
					else
					{
						inQuotes = false;
					}

					continue;
				}

				if (c == '"' && field.Length == 0)
				{
					inQuotes = true;
				}
				else if (c == ',')
				{
					fields.Add(field.ToString());
					field.Clear();
				}
				else if (isLineBreak)
				{
					EndRecord();
					line++;
					recordLine = line;
				}
				else if (c != '\r')
				{
					field.Append(c);
				}
			}

			if (inQuotes)
			{
				throw new FormatException($"Unterminated quoted cell in the row starting on line {recordLine}");
			}

			EndRecord();

			return records.ToArray();
		}

		private static string SerializedKeyValuePair(string text)
		{
			var split = text.Split(PairSplitChars);

			return $"{{\"Key\":\"{split[0].Trim()}\",\"Value\":\"{split[1].Trim()}\"}}";
		}

		private static bool TryGetKeyValuePair(string data, Type type, out object key, out object value,
											   params Func<string, Type, object>[] deserializers)
		{
			if (!type.IsValueType)
			{
				key = null;
				value = null;

				return false;
			}

			var fields = type.GetFields();
			var split = data.Split(PairSplitChars);

			if (type.IsGenericType && fields.Length == 2 && (fields[0].Name == "Key" || fields[0].Name == "Value1") &&
				(fields[1].Name == "Value" || fields[1].Name == "Value2"))
			{
				key = string.IsNullOrWhiteSpace(data) ? default : Parse(split[0], fields[0].FieldType, deserializers);
				value = string.IsNullOrWhiteSpace(data) ? default : Parse(split[1], fields[1].FieldType, deserializers);

				return true;
			}

			key = null;
			value = null;

			return false;
		}
	}
}