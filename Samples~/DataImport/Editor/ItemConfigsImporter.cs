using GameLovers.DataImporter.Editor;

namespace GameLovers.DataImporter.Samples.DataImport.Editor
{
	// Source: a CSV file edited directly in the repository, by hand or by an agent.
	// Output: the ItemConfigs asset, plus the opt-in JSON payload with the same content.
	public sealed class ItemConfigsImporter : ConfigsImporter<ItemConfig, ItemConfigs>
	{
		public override string CsvPath => SampleData.Path("Items.csv");

		public override bool WriteJsonPayload => true;
	}
}
