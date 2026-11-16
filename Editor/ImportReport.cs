using System;
using System.Collections.Generic;

namespace GameLovers.DataImporter.Editor
{
	/// <summary>
	/// Outcome of one import run, one result per importer. Its field names are the JSON report format that
	/// <see cref="DataImporterEditorUtils.RunFromCommandLine"/> writes.
	/// </summary>
	[Serializable]
	public sealed class ImportReport
	{
		/// <summary>Number of importers that reported an error.</summary>
		public int Failed;

		/// <summary>One result per importer, in the order they ran.</summary>
		public List<ImportResult> Results = new List<ImportResult>();
	}

	/// <summary>
	/// Outcome of one importer within an <see cref="ImportReport"/>.
	/// </summary>
	[Serializable]
	public sealed class ImportResult
	{
		/// <summary>Type name of the importer.</summary>
		public string Importer;

		/// <summary>Project-relative path of the CSV file the importer read.</summary>
		public string CsvPath;

		/// <summary>The importer's upstream source, or empty when the CSV file is authored directly.</summary>
		public string Source;

		/// <summary>True when the CSV file was rewritten from the source before the import.</summary>
		public bool Pulled;

		/// <summary>Number of data rows imported; zero when the importer failed.</summary>
		public int Rows;

		/// <summary>The <c>path:line: message</c> error, or empty when the importer succeeded.</summary>
		public string Error;
	}
}
