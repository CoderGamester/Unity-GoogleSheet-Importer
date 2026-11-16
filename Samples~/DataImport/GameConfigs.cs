using System;
using GameLovers.GameData;
using UnityEngine;

namespace GameLovers.DataImporter.Samples.DataImport
{
	[Serializable]
	public struct GameConfig
	{
		public StructPair<int, int> Random;
		public string GameVersion;
		public int MaxPlayers;
	}

	public sealed class GameConfigs : ScriptableObject, ISingleConfigContainer<GameConfig>
	{
		[SerializeField] private GameConfig _config;

		public GameConfig Config
		{
			get => _config;
			set => _config = value;
		}
	}
}
