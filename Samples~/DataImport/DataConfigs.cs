using System;
using System.Collections.Generic;
using GameLovers.GameData;
using UnityEngine;

namespace GameLovers.DataImporter.Samples.DataImport
{
	[Serializable]
	public struct DataConfig
	{
		public string Id;
		public int Int;
		public string String;
		public float Float;
		public List<int> List;
		public StructPair<int, int> IntPair;
		public StructPair<float, float> FloatPair;
		public string StringPair;
		public List<StructPair<int, int>> ListPair;
	}

	public sealed class DataConfigs : ScriptableObject, IConfigsContainer<DataConfig>
	{
		[SerializeField] private List<DataConfig> _configs = new();

		public List<DataConfig> Configs
		{
			get => _configs;
			set => _configs = value;
		}
	}
}
