using System;
using GameLovers.SpreadsheetImporter.Editor;

namespace GameLovers.SpreadsheetImporter.Samples.SpreadsheetImport.Editor
{
	// Source: a Key/Value tab of the same Google Sheet, read into a single config instead of a list.
	public sealed class GameConfigsImporter : SingleConfigSubListImporter<GameConfig, GameConfigs>
	{
		public override string CsvPath => SampleData.Path("GameConfig.csv");

		public override string Source =>
			"https://docs.google.com/spreadsheets/d/1095NlqM_3cvZfCTnQtTsCkOAXh0McypKZje47WtBzPY/edit?gid=1963922464#gid=1963922464";

		protected override Func<string, Type, object>[] GetDeserializers()
		{
			return Array.Empty<Func<string, Type, object>>();
		}
	}
}