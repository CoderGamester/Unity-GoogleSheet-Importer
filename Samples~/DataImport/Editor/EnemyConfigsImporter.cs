using GameLovers.DataImporter.Editor;

namespace GameLovers.DataImporter.Samples.DataImport.Editor
{
	// Source: the "Enemies" worksheet of an Excel workbook. Pull rewrites Enemies.csv from it.
	public sealed class EnemyConfigsImporter : ConfigsImporter<EnemyConfig, EnemyConfigs>
	{
		public override string CsvPath => SampleData.Path("Enemies.csv");

		public override string Source => SampleData.Path("Enemies.xlsx") + "#Enemies";
	}
}
