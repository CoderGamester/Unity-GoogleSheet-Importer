using System.Collections.Generic;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.GoogleSheetImporter
{
	/// <summary>
	/// Implement this interface to import a single Google Sheet.
	/// All the process is done in Editor time 
	/// </summary>
	/// <remarks>
	/// It is required to have different implementations of this interface for as many sheets needed to be imported
	/// </remarks>
	public interface IGoogleSheetImporter
	{
		/// <summary>
		/// The complete GoogleSheet Url
		/// </summary>
		string GoogleSheetUrl { get; }
		
		/// <summary>
		/// Imports the <paramref name="data"/> that was processed in <seealso cref="CsvParser.ConvertCsv"/> into the game
		/// </summary>
		// ReSharper disable once ParameterTypeCanBeEnumerable.Global
		void Import(List<Dictionary<string, string>> data);
	}
}