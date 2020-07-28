using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// ReSharper disable once CheckNamespace

namespace GameLovers.GoogleSheetImporter
{
	/// <summary>
	/// Provides all the Game's config static data, including the game design data
	/// Has the imported data from the Universal Google Sheet file on the web
	/// </summary>
	public interface IConfigsProvider
	{
		/// <summary>
		/// Requests the single unique Config of <typeparamref name="T"/> type
		/// </summary>
		T GetSingleConfig<T>();
		
		/// <summary>
		/// Requests the Config of <typeparamref name="T"/> type and with the given <paramref name="id"/>
		/// </summary>
		T GetConfig<T>(int id);

		/// <summary>
		/// Requests the Config List of <typeparamref name="T"/> type
		/// </summary>
		IReadOnlyList<T> GetConfigsList<T>();

		/// <summary>
		/// Requests the Config Dictionary of <typeparamref name="T"/> type
		/// </summary>
		IReadOnlyDictionary<int, T> GetConfigsDictionary<T>();
	}
	
	/// <inheritdoc />
	public class ConfigsProvider : IConfigsProvider
	{
		private const int _singleConfigId = 0;
		
		private readonly IDictionary<Type, IEnumerable> _configs = new Dictionary<Type, IEnumerable>();
		
		/// <inheritdoc />
		public T GetSingleConfig<T>()
		{
			return GetConfigsDictionary<T>()[_singleConfigId];
		}

		/// <inheritdoc />
		public T GetConfig<T>(int id)
		{
			return GetConfigsDictionary<T>()[id];
		}

		/// <inheritdoc />
		public IReadOnlyList<T> GetConfigsList<T>()
		{
			return new List<T>(GetConfigsDictionary<T>().Values);
		}

		/// <inheritdoc />
		public IReadOnlyDictionary<int, T> GetConfigsDictionary<T>() 
		{
			return _configs[typeof(T)] as IReadOnlyDictionary<int, T>;
		}

		/// <summary>
		/// Adds the given unique single <paramref name="config"/> to the container.
		/// Use the <seealso cref="GetSingletonConfig{T}"/> to retrieve it.
		/// </summary>
		public void AddSingletonConfig<T>(T config)
		{
			_configs.Add(typeof(T), new ReadOnlyDictionary<int, T>(new Dictionary<int, T> {{ _singleConfigId, config }}));
		}

		/// <summary>
		/// Adds the given <paramref name="configList"/> to the container.
		/// The configuration will use the given <paramref name="referenceIdResolver"/> to map each config to it's defined id.
		/// Use the <seealso cref="GetConfig{T}"/> to retrieve it.
		/// </summary>
		public void AddConfigs<T>(Func<T, int> referenceIdResolver, IList<T> configList) where T : struct
		{
			var dictionary = new Dictionary<int, T>();

			for (int i = 0; i < configList.Count; i++)
			{
				dictionary.Add(referenceIdResolver(configList[i]), configList[i]);
			}

			_configs.Add(typeof(T), new ReadOnlyDictionary<int, T>(dictionary));
		}
	}
}