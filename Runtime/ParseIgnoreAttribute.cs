using System;

namespace GameLovers.DataImporter
{
	/// <summary>
	/// Marks a config field the importer must leave untouched, even when the imported sheet has a column
	/// with the same name.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class ParseIgnoreAttribute : Attribute
	{
	}
}