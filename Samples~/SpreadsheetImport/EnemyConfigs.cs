using System;
using System.Collections.Generic;
using GameLovers.GameData;
using UnityEngine;

namespace GameLovers.SpreadsheetImporter.Samples.SpreadsheetImport
{
	[Serializable]
	public struct EnemyConfig
	{
		public int Id;
		public string Name;
		public int Health;
		public float Speed;
		public bool IsBoss;
		public string ReleaseDate;
	}

	public sealed class EnemyConfigs : ScriptableObject, IConfigsContainer<EnemyConfig>
	{
		[SerializeField] private List<EnemyConfig> _configs = new();

		public List<EnemyConfig> Configs
		{
			get => _configs;
			set => _configs = value;
		}
	}
}
