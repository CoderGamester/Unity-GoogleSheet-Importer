using System;

namespace GameLovers.SpreadsheetImporter.Editor
{
	/// <summary>
	/// An import failure tied to a row of the CSV file, so tooling can report it as <c>path:line</c>.
	/// </summary>
	public sealed class SpreadsheetImportException : Exception
	{
		public const int HeaderRow = -1;

		/// <summary>Zero-based index of the failing data row, or <see cref="HeaderRow"/> for the header line.</summary>
		public int Row { get; }

		public SpreadsheetImportException(int row, string message, Exception innerException = null) : base(message, innerException)
		{
			Row = row;
		}
	}
}
