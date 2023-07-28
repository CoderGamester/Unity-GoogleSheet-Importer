using UnityEngine;

// ReSharper disable once CheckNamespace

namespace GameLoversEditor.GoogleSheetImporter
{
	/// <summary>
	/// Scriptable Object tool to import all or specific google sheet data
	/// </summary>
	[CreateAssetMenu(fileName = "GoogleSheetImporter", menuName = "ScriptableObjects/Editor/GoogleSheetImporter")]
	public class GoogleSheetImporter : ScriptableObject
	{
		public string ReplaceSpreadsheetId;
	}
}