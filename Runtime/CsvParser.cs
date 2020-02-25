using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLovers.GoogleSheetImporter
{
	/// <summary>
	/// Helper class to parse CSV text
	/// </summary>
	public static class CsvParser
	{
		private static readonly char[] _pairSplitChars = new[] {',', ':', '<', '>', '='};
		private static readonly char[] _arraySplitChars = new[] {',', '(', ')', '[', ']', '{', '}'};

		/// <summary>
		/// Deserializes the given CSV <paramref name="data"/> cell values to an object of the given <typeparamref name="T"/> type
		/// </summary>
		public static T DeserializeTo<T>(Dictionary<string, string> data)
		{
			var type = typeof(T);
			var listType = typeof(IList);
			var dictionaryType = typeof(IDictionary);
			var ignoreType = typeof(ParseIgnoreAttribute);
			var instance = Activator.CreateInstance(type);

			foreach (var field in type.GetFields())
			{
				if (!data.ContainsKey(field.Name))
				{
					Debug.LogWarning($"The data does not contain the field {field.Name} data for the object of {type} type");
					continue;
				}

				if (field.GetCustomAttributes(ignoreType, false).Length == 1)
				{
					continue;
				}
				
				var stringSerialized = "";
				
				if (field.FieldType.IsArray)
				{
					stringSerialized = JsonConvert.SerializeObject(ArrayParse(data[field.Name], null, field.FieldType.GetElementType()));
				}
				else if (listType.IsAssignableFrom(field.FieldType))
				{
					stringSerialized = JsonConvert.SerializeObject(ArrayParse(data[field.Name], null, field.FieldType.GenericTypeArguments[0]));
				}
				else if (dictionaryType.IsAssignableFrom(field.FieldType))
				{
					var types = field.FieldType.GenericTypeArguments;
					
					stringSerialized = JsonConvert.SerializeObject(DictionaryParse(data[field.Name], null, types[0], types[1]));
				}
				else if (IsKeyValuePairType(field.FieldType))
				{
					stringSerialized = SerializedKeyValuePair(data[field.Name]);
				}
				else
				{
					stringSerialized = $"\"{data[field.Name]}\"";
				}
				
				field.SetValue(instance, JsonConvert.DeserializeObject(stringSerialized, field.FieldType));
			}

			return (T) instance;
		}
		
		/// <summary>
		/// Parses the entire <paramref name="csv"/> text.
		/// Each row is an element on the returned list
		/// Each column is an element on the returned dictionary. The dictionary key will be the CSV header
		/// </summary>
		public static List<Dictionary<string, string>> ConvertCsv(string csv)
		{
			var lines = csv.Split(new [] { "\r\n" }, StringSplitOptions.None);
			var list = new List<Dictionary<string, string>>(lines.Length - 1);
			var headlines = EnumerateCsvLine(lines[0]);

			for (var i = 1; i < lines.Length; i++)
			{
				var dictionary = new Dictionary<string, string>(headlines.Length);
				var values = EnumerateCsvLine(lines[i]);

				for (var j = 0; j < headlines.Length; j++)
				{
					dictionary.Add(headlines[j], values[j].Trim());
				}
				
				list.Add(dictionary);
			}

			return list;
		}

		/// <summary>
		/// Parses the given <paramref name="text"/> into a possible array of the given <typeparamref name="T"/>
		/// A text is in array format as long as is divided by ',', '{}', '()' or '[]' (ex: 1,2,3; {1,2}{4,5}, [1,2,3])
		/// If the given <paramref name="text"/> is not in an array format, it will return an array with <paramref name="text"/> as the only element
		/// </summary>
		/// <exception cref="FormatException">
		/// Thrown if the given <paramref name="text"/> is not in the given <typeparamref name="T"/> type format
		/// </exception>
		public static List<T> ArrayParse<T>(string text)
		{
			return ArrayParse(text, new List<T>(), typeof(T)) as List<T>;
		}

		/// <summary>
		/// Parses the given <paramref name="text"/> into a <seealso cref="Dictionary{TKey, TValue}"/> type.
		/// A text is in <seealso cref="Dictionary{TKey, TValue}"/> type format if follows the same rules
		/// of <seealso cref="ArrayParse{T}"/> and has at least 2 elements inside
		/// If the given <paramref name="text"/> is not in an <seealso cref="Dictionary{TKey, TValue}"/> type format,
		/// it will return an empty dictionary
		/// </summary>
		/// <exception cref="FormatException">
		/// Thrown if the given <paramref name="text"/> is not in the given <typeparamref name="TKey"/> or <typeparamref name="TValue"/> type format
		/// </exception>
		/// <exception cref="IndexOutOfRangeException">
		/// Thrown if the given <paramref name="text"/> has a odd amount of values to pair. Must always be an even amount of values
		/// </exception>
		public static Dictionary<TKey, TValue> DictionaryParse<TKey, TValue>(string text)
		{
			return DictionaryParse(text, new Dictionary<TKey, TValue>(), typeof(TKey), typeof(TValue)) as Dictionary<TKey, TValue>;
		}

		/// <summary>
		/// Parses the given <paramref name="text"/> to the given <typeparamref name="T"/> type
		/// </summary>
		/// <exception cref="FormatException">
		/// Thrown if the given <paramref name="text"/> is not in the given <typeparamref name="T"/> type format
		/// </exception>
		public static T Parse<T>(string text)
		{
			return (T) Parse(text, typeof(T));
		}

		private static object ArrayParse(string text, IList list, Type type)
		{
			var split = text.Split(_arraySplitChars);

			list = list ?? new List<dynamic>();

			foreach (var value in split)
			{
				if (string.IsNullOrEmpty(value))
				{
					continue;
				}
				
				list.Add(Parse(value, type));
			}

			return list;
		}

		private static object DictionaryParse(string text, IDictionary dictionary, Type keyType, Type valueType)
		{
			var items = ArrayParse<string>(text);
			
			dictionary = dictionary ?? new Dictionary<dynamic, dynamic>();

			if (items[0].IndexOfAny(_pairSplitChars) != -1)
			{
				foreach (var item in items)
				{
					var split = item.Split(_pairSplitChars);
					var key = Parse(split[0], keyType);
					var value = Parse(split[1], valueType);
					
					dictionary.Add(key, value);
				}
			}
			else if (items.Count % 2 == 1)
			{
				throw new IndexOutOfRangeException($"Dictionary must have an even amount of values and the following text" +
				                                   $"has {items.Count.ToString()} values. \nText:{text}");
			}
			else
			{
				for(var i = 0; i < items.Count; i += 2)
				{
					var key = Parse(items[i], keyType);
					var value = Parse(items[i + 1], valueType);
					
					dictionary.Add(key, value);
				}
			}

			return dictionary;
		}

		private static object Parse(string text, Type type)
		{
			if (type == typeof(string))
			{
				return text;
			}
			
			if (type.IsEnum)
			{
				return Enum.Parse(type, text);
			}

			if (IsKeyValuePairType(type))
			{
				return JsonConvert.DeserializeObject(SerializedKeyValuePair(text), type);
			}
			
			//Handling Nullable types i.e, int?, double?, bool? .. etc
			if (type.IsValueType && Nullable.GetUnderlyingType(type) != null) 
			{
				// ReSharper disable once PossibleNullReferenceException
				return TypeDescriptor.GetConverter(type).ConvertFrom(text);
			}

			if (type.IsValueType)
			{
				return Convert.ChangeType(text, type);
			}
			
			throw new FormatException($"The text {text} is not convertible to type {type}");
		}

		private static string[] EnumerateCsvLine(string line) 
		{
			// Regex taken from http://wiki.unity3d.com/index.php?title=CSVReader
			const string match = @"(((?<x>(?=[,\r\n]+))|""(?<x>([^""]|"""")+)""|(?<x>[^,\r\n]+)),?)";
			
			var matches = Regex.Matches(line, match, RegexOptions.ExplicitCapture);
			var ret = new string[matches.Count];
			
			for(var i = 0; i < matches.Count; i++) 
			{
				ret[i] = matches[i].Groups[1].Value;
			}

			return ret;
		}

		private static string SerializedKeyValuePair(string text)
		{
			var split = text.Split(_pairSplitChars);

			return $"{{\"Key\":\"{split[0].Trim()}\",\"Value\":\"{split[1].Trim()}\"}}";
		}

		// TODO: Refactor this after Unity 2020.1 release. Unity will allow Generic Type Serialization....FINALLY \o/
		private static bool IsKeyValuePairType(Type type)
		{
			if (!type.IsValueType)
			{
				return false;
			}

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
			{
				return true;
			}

			var fields = type.GetFields();

			return fields.Length == 2 && fields[0].Name == "Key" && fields[1].Name == "Value";
		}
	}
}