using GameLovers.SpreadsheetImporter.Editor;

namespace GameLovers.SpreadsheetImporter.Samples.SpreadsheetImport.Editor
{
	// Source: one tab of a link-shared Google Sheet, pasted as copied from the browser. Pull rewrites
	// DataConfigs.csv from it; the cells show the list and pair formats the parser reads.
	public sealed class DataConfigsImporter : ConfigsImporter<DataConfig, DataConfigs>
	{
		public override string CsvPath => SampleData.Path("DataConfigs.csv");

		public override string Source =>
			"https://docs.google.com/spreadsheets/d/1095NlqM_3cvZfCTnQtTsCkOAXh0McypKZje47WtBzPY/edit?gid=558488968#gid=558488968";
	}
}