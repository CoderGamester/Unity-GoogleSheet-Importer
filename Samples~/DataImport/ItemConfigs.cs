using System;
using System.Collections.Generic;
using GameLovers.GameData;
using UnityEngine;

namespace GameLovers.DataImporter.Samples.DataImport
{
	public enum ItemRarity
	{
		Common,
		Rare,
		Epic
	}

	[Serializable]
	public struct ItemConfig
	{
		public int Id;
		public string Name;
		public ItemRarity Rarity;
		public float Power;
		public int Cost;
		public List<int> Tags;
	}

	// Unity binds an asset to its script through the file name, so each ScriptableObject type has its own file.
	public sealed class ItemConfigs : ScriptableObject, IConfigsContainer<ItemConfig>
	{
		[SerializeField] private List<ItemConfig> _configs = new();

		public List<ItemConfig> Configs
		{
			get => _configs;
			set => _configs = value;
		}
	}
}
