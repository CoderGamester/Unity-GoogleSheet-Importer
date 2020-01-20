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
		/// <summary>
		/// Deserializes the given CSV <paramref name="data"/> cell values to an object of the given <typeparamref name="T"/> type
		/// </summary>
		public static T DeserializeTo<T>(Dictionary<string, string> data)
		{
			var type = typeof(T);
			var dictionaryType = typeof(IDictionary);
			var listType = typeof(IList);
			var keyValueType = typeof(KeyValuePair<,>);
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
				
				if (dictionaryType.IsAssignableFrom(field.FieldType))
				{
					stringSerialized = JsonConvert.SerializeObject(DictionaryParse<string, string>(data[field.Name]));
				}
				else if (listType.IsAssignableFrom(field.FieldType))
				{
					stringSerialized = JsonConvert.SerializeObject(ArrayParse<string>(data[field.Name]));
				}
				else if(field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == keyValueType)
				{
					var pair = PairParse<string, string>(data[field.Name]);
						
					stringSerialized = $"{{\"Key\":\"{pair.Key}\",\"Value\":\"{pair.Value}\"}}";
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
		public static T[] ArrayParse<T>(string text)
		{
			const string match = @"([\w\-.@#%$€£]+\s*)*[^( \[* | \]* | \(* | \)* | \,*) | \{* | \}* ]";

			var matches = Regex.Matches(text, match, RegexOptions.ExplicitCapture);
			var ret = new T[matches.Count];

			for (int i = 0; i < ret.Length; i++)
			{
				ret[i] = Parse<T>(matches[i].Groups[0].Value);
			}

			return ret;
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
			var items = ArrayParse<string>(text);
			var dictionary = new Dictionary<TKey, TValue>();

			if (items.Length % 2 == 1)
			{
				throw new IndexOutOfRangeException($"Dictionary must have an even amount of values and the following text" +
				                                   $"has {items.Length.ToString()} values. \nText:{text}");
			}
				
			for(var i = 0; i < items.Length; i += 2)
			{
				var key = Parse<TKey>(items[i]);
				var value = Parse<TValue>(items[i + 1]);
					
				dictionary.Add(key, value);
			}

			return dictionary;
		}

		/// <summary>
		/// Parses the given <paramref name="text"/> into a <seealso cref="KeyValuePair{TKey, TValue}"/> type.
		/// A text is in <seealso cref="KeyValuePair{TKey, TValue}"/> type format if follows the same rules
		/// of <seealso cref="ArrayParse{T}"/> and has 2 elements inside
		/// </summary>
		/// <exception cref="IndexOutOfRangeException">
		/// Thrown if the given <paramref name="text"/> is not in <seealso cref="KeyValuePair{TKey, TValue}"/> type format
		/// </exception>
		/// <exception cref="FormatException">
		/// Thrown if the given <paramref name="text"/> is not in the given <typeparamref name="TKey"/> or <typeparamref name="TValue"/> type format
		/// </exception>
		public static KeyValuePair<TKey, TValue> PairParse<TKey, TValue>(string text)
		{
			var items = ArrayParse<string>(text);

			if (items.Length != 2)
			{
				throw new IndexOutOfRangeException($"The text {text} is not convertible to KeyValuePair type" +
				                                   "because it has more or less than 2 elements inside");
			}
			
			return new KeyValuePair<TKey, TValue>(Parse<TKey>(items[0]), Parse<TValue>(items[1]));
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
	}
}