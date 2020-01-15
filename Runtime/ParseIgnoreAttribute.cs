using System;

// ReSharper disable once CheckNamespace

namespace GameLovers.GoogleSheetImporter
{
	/// <summary>
	/// Attribute to ignore the parsing of a field in <seealso cref="CsvParse"/>
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class ParseIgnoreAttribute : Attribute
	{
	}
}