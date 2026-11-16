using System;

namespace GameLovers.DataImporter.Editor
{
	/// <summary>Sets when an importer runs relative to the others; lower runs sooner, the default is last.</summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class ImportOrderAttribute : Attribute
	{
		/// <summary>The position of the importer in the run; lower runs sooner.</summary>
		public int ImportOrder { get; }

		public ImportOrderAttribute(int importOrder)
		{
			ImportOrder = importOrder;
		}
	}
}