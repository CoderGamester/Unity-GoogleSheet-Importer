using System.Collections.Generic;

// ReSharper disable once CheckNamespace

namespace GameLovers.GoogleSheetImporter
{
	/// <summary>
	/// Generic container of the configs imported with a ConfigsImporter script
	/// The given <typeparamref name="T"/> type is the same of the config struct defined to be serialized in the scriptable object
	/// </summary>
	public interface IConfigsContainer<T> where T : struct
	{
		List<T> Configs { get; }
	}
	
	/// <summary>
	/// Generic container of the unique single config imported with a ConfigsImporter script
	/// The given <typeparamref name="T"/> type is the same of the config struct defined to be serialized in the scriptable object
	/// </summary>
	public interface ISingleConfigContainer<T> where T : struct
	{
		T Config { get; set; }
	}
}