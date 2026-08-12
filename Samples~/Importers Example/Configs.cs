using System;
using System.Collections.Generic;
using GameLovers.GameData;
using UnityEngine;

namespace Configs
{
	[Serializable]
	public struct DataConfig
	{
		public int Id;
		public string Name;
		public int Value;
	}

	[Serializable]
	public struct GameConfig
	{
		public string GameName;
		public int StartingCoins;
	}

	[CreateAssetMenu(menuName = "GameLovers Google Sheet Importer Samples/Data Configs")]
	public sealed class DataConfigs : ScriptableObject, IConfigsContainer<DataConfig>
	{
		[SerializeField] private List<DataConfig> configs = new();

		public List<DataConfig> Configs
		{
			get => configs;
			set => configs = value;
		}
	}

	[CreateAssetMenu(menuName = "GameLovers Google Sheet Importer Samples/Game Configs")]
	public sealed class GameConfigs : ScriptableObject, ISingleConfigContainer<GameConfig>
	{
		[SerializeField] private GameConfig config;

		public GameConfig Config
		{
			get => config;
			set => config = value;
		}
	}
}
