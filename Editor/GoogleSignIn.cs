using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameLovers.SpreadsheetImporter.Editor
{
	/// <summary>
	/// Signs the Editor user in to Google with the project's Desktop OAuth client, so pulls can read the private
	/// sheets that user can open. The refresh token is kept per user and project in EditorPrefs.
	/// </summary>
	internal static class GoogleSignIn
	{
		internal const string Scope = "openid email https://www.googleapis.com/auth/spreadsheets.readonly";

		private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
		private const string RefreshTokenPrefsPrefix = "GameLovers.SpreadsheetImporter.GoogleRefreshToken:";
		private const string AccountPrefsPrefix = "GameLovers.SpreadsheetImporter.GoogleAccount:";
		private const int SignInTimeoutSeconds = 180;
		private const string BrowserReply =
			"<html><body style=\"font-family:sans-serif;margin:3em\"><h2>{0}</h2><p>You can close this tab and return to Unity.</p></body></html>";

		private static HttpListener _listener;

		/// <summary>The signed-in Google account, or null when nobody is signed in.</summary>
		internal static string Account => SignedIn ? EditorPrefs.GetString(AccountPrefsKey, null) : null;

		/// <summary>True when a refresh token is stored for this user and project.</summary>
		internal static bool SignedIn => !string.IsNullOrEmpty(EditorPrefs.GetString(RefreshTokenPrefsKey, null));

		/// <summary>True while the browser sign-in is waiting for Google's reply.</summary>
		internal static bool InProgress => _listener != null;

		private static string RefreshTokenPrefsKey => RefreshTokenPrefsPrefix + Application.dataPath;

		private static string AccountPrefsKey => AccountPrefsPrefix + Application.dataPath;

		/// <summary>
		/// Opens the browser on Google's consent page and completes the sign-in when Google redirects back. Calls
		/// <paramref name="done"/> on the main thread with null on success or the error message.
		/// </summary>
		internal static void Begin(Action<string> done)
		{
			var settings = SpreadsheetImporterSettings.instance;

			if (!settings.HasGoogleClient)
			{
				done("Set up Google sign-in for this project first: load the Desktop client file");

				return;
			}

			Cancel();

			var port = FreeLoopbackPort();
			var redirectUri = $"http://127.0.0.1:{port}/";
			var verifier = RandomToken();
			var state = RandomToken();
			var listener = new HttpListener();

			listener.Prefixes.Add(redirectUri);
			listener.Start();
			_listener = listener;
			Application.OpenURL(AuthorizationUrl(settings.GoogleClientId, redirectUri, CodeChallenge(verifier), state));

			Task.Run(() => WaitForRedirect(listener)).ContinueWith(task => EditorApplication.delayCall += () =>
			{
				if (_listener != listener)
				{
					return;
				}

				Cancel();

				try
				{
					var code = ReadRedirect(task.IsFaulted ? null : task.Result, state);
					var tokens = SpreadsheetImporterEditorUtils.PostToken(new Dictionary<string, string>
					{
						["grant_type"] = "authorization_code",
						["code"] = code,
						["code_verifier"] = verifier,
						["redirect_uri"] = redirectUri,
						["client_id"] = settings.GoogleClientId,
						["client_secret"] = settings.GoogleClientSecret
					}, "Finishing the Google sign-in");
					var refreshToken = tokens.Value<string>("refresh_token");

					if (string.IsNullOrEmpty(refreshToken))
					{
						throw new IOException("Google returned no refresh token; sign in again");
					}

					EditorPrefs.SetString(RefreshTokenPrefsKey, refreshToken);
					EditorPrefs.SetString(AccountPrefsKey, EmailFromIdToken(tokens.Value<string>("id_token")) ?? "your Google account");
					done(null);
				}
				catch (Exception e)
				{
					done(e.Message);
				}
			});
		}

		/// <summary>Stops a sign-in that is waiting for the browser.</summary>
		internal static void Cancel()
		{
			var listener = _listener;

			_listener = null;

			if (listener != null)
			{
				listener.Close();
			}
		}

		/// <summary>Forgets the stored sign-in for this user and project.</summary>
		internal static void SignOut()
		{
			EditorPrefs.DeleteKey(RefreshTokenPrefsKey);
			EditorPrefs.DeleteKey(AccountPrefsKey);
		}

		/// <summary>Trades the stored refresh token for an access token, or returns null when signed out.</summary>
		internal static string AccessToken()
		{
			var refreshToken = EditorPrefs.GetString(RefreshTokenPrefsKey, null);
			var settings = SpreadsheetImporterSettings.instance;

			if (string.IsNullOrEmpty(refreshToken) || !settings.HasGoogleClient)
			{
				return null;
			}

			try
			{
				var tokens = SpreadsheetImporterEditorUtils.PostToken(new Dictionary<string, string>
				{
					["grant_type"] = "refresh_token",
					["refresh_token"] = refreshToken,
					["client_id"] = settings.GoogleClientId,
					["client_secret"] = settings.GoogleClientSecret
				}, "Signing in with your Google account");

				return tokens.Value<string>("access_token");
			}
			catch (IOException e) when (e.Data[SpreadsheetImporterEditorUtils.GoogleErrorKey] as string == "invalid_grant")
			{
				SignOut();

				throw new IOException("Your Google sign-in has expired or was revoked. Sign in again in the Spreadsheet Importer window");
			}
		}

		/// <summary>Reads the client id and secret from the JSON file Google offers for a Desktop app client.</summary>
		internal static (string ClientId, string ClientSecret) ReadClientFile(string json)
		{
			var file = JObject.Parse(json);

			if (file["installed"] is not JObject installed)
			{
				var kind = file.Properties().FirstOrDefault()?.Name ?? "unknown";

				throw new InvalidDataException(
					$"This is a '{kind}' OAuth client file. Create a client of type 'Desktop app' and download its JSON");
			}

			var clientId = installed.Value<string>("client_id");
			var clientSecret = installed.Value<string>("client_secret");

			if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
			{
				throw new InvalidDataException("The client file has no client_id or client_secret");
			}

			return (clientId, clientSecret);
		}

		/// <summary>Builds Google's consent URL for an offline, PKCE-protected sign-in.</summary>
		internal static string AuthorizationUrl(string clientId, string redirectUri, string codeChallenge, string state)
		{
			var query = new Dictionary<string, string>
			{
				["client_id"] = clientId,
				["redirect_uri"] = redirectUri,
				["response_type"] = "code",
				["scope"] = Scope,
				["code_challenge"] = codeChallenge,
				["code_challenge_method"] = "S256",
				["access_type"] = "offline",
				["prompt"] = "consent",
				["state"] = state
			};

			return $"{AuthorizationEndpoint}?{string.Join("&", query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"))}";
		}

		/// <summary>The PKCE S256 challenge of <paramref name="verifier"/>.</summary>
		internal static string CodeChallenge(string verifier)
		{
			using var sha = SHA256.Create();

			return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
		}

		/// <summary>
		/// Returns the authorization code from Google's redirect query, or throws when the user declined, the state
		/// does not match, or no redirect arrived.
		/// </summary>
		internal static string ReadRedirect(IReadOnlyDictionary<string, string> query, string expectedState)
		{
			if (query == null)
			{
				throw new TimeoutException("The Google sign-in did not finish in the browser");
			}

			if (query.TryGetValue("error", out var error))
			{
				throw new IOException(error == "access_denied" ? "The Google sign-in was declined" : $"Google sign-in failed: {error}");
			}

			if (!query.TryGetValue("state", out var state) || state != expectedState)
			{
				throw new IOException("The Google sign-in reply does not belong to this sign-in; try again");
			}

			if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
			{
				throw new IOException("The Google sign-in reply has no authorization code");
			}

			return code;
		}

		/// <summary>The e-mail claim of an id token, for display only; the token is not verified.</summary>
		internal static string EmailFromIdToken(string idToken)
		{
			var parts = idToken?.Split('.');

			if (parts == null || parts.Length < 2)
			{
				return null;
			}

			var payload = parts[1].Replace('-', '+').Replace('_', '/');

			payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

			return JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload))).Value<string>("email");
		}

		private static IReadOnlyDictionary<string, string> WaitForRedirect(HttpListener listener)
		{
			var request = listener.GetContextAsync();

			if (!request.Wait(TimeSpan.FromSeconds(SignInTimeoutSeconds)))
			{
				return null;
			}

			var context = request.Result;
			var query = context.Request.QueryString;
			var values = query.AllKeys.Where(key => key != null).ToDictionary(key => key, key => query[key]);
			var reply = Encoding.UTF8.GetBytes(string.Format(BrowserReply,
				values.ContainsKey("code") ? "Signed in to the Spreadsheet Importer" : "The Spreadsheet Importer sign-in did not finish"));

			context.Response.ContentType = "text/html; charset=utf-8";
			context.Response.ContentLength64 = reply.Length;
			context.Response.OutputStream.Write(reply, 0, reply.Length);
			context.Response.Close();

			return values;
		}

		private static int FreeLoopbackPort()
		{
			var probe = new TcpListener(IPAddress.Loopback, 0);

			probe.Start();

			var port = ((IPEndPoint)probe.LocalEndpoint).Port;

			probe.Stop();

			return port;
		}

		private static string RandomToken()
		{
			var bytes = new byte[32];

			using (var random = RandomNumberGenerator.Create())
			{
				random.GetBytes(bytes);
			}

			return Base64Url(bytes);
		}

		private static string Base64Url(byte[] bytes)
		{
			return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		}
	}
}
