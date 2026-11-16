using UnityEditor;
using UnityEngine;

namespace GameLovers.DataImporter.Editor
{
	/// <summary>
	/// Project-wide Data Importer settings, saved in <c>ProjectSettings/</c> so the whole team shares them.
	/// </summary>
	[FilePath("ProjectSettings/GameLoversDataImporterSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	public sealed class DataImporterSettings : ScriptableSingleton<DataImporterSettings>
	{
		[SerializeField] private string _googleClientId;
		[SerializeField] private string _googleClientSecret;

		/// <summary>OAuth client id of the project's Google Desktop app client, used by Sign in with Google.</summary>
		public string GoogleClientId => _googleClientId;

		/// <summary>Secret of the same client; Google treats a Desktop client's secret as not confidential.</summary>
		public string GoogleClientSecret => _googleClientSecret;

		/// <summary>True when the project has a Google client, so team members can sign in.</summary>
		public bool HasGoogleClient => !string.IsNullOrEmpty(_googleClientId) && !string.IsNullOrEmpty(_googleClientSecret);

		/// <summary>Stores the Google client and saves the settings file.</summary>
		public void SetGoogleClient(string clientId, string clientSecret)
		{
			_googleClientId = clientId;
			_googleClientSecret = clientSecret;
			Save(true);
		}
	}
}
