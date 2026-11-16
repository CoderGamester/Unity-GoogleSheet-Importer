using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace GameLovers.DataImporter.Editor.Tests
{
	public class GoogleSignInTest
	{
		[Test]
		// ADMIT: GoogleSignIn.CodeChallenge is the RFC 7636 S256 challenge, which Google checks against the verifier
		// sent with the code.
		// RCR: GoogleSignIn.cs Base64Url — change `.Replace('+', '-').Replace('/', '_')` to `` → RED (the challenge
		// keeps standard base64 characters). 2026-11-16
		public void CodeChallenge_Rfc7636Example_MatchesTheSpecification()
		{
			Assert.AreEqual("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
				GoogleSignIn.CodeChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
		}

		[Test]
		// ADMIT: GoogleSignIn.AuthorizationUrl asks for offline access with consent, so Google returns the refresh token
		// a later pull needs, and sends the PKCE challenge and state.
		// RCR: GoogleSignIn.cs AuthorizationUrl — change `["access_type"] = "offline"` to `["access_type"] = "online"`
		// → RED (access_type is online). 2026-11-16
		public void AuthorizationUrl_Parameters_RequestOfflineAccessWithPkce()
		{
			var url = GoogleSignIn.AuthorizationUrl("client.apps.googleusercontent.com", "http://127.0.0.1:5000/", "challenge", "state-1");
			var query = Query(url);

			Assert.AreEqual("offline", query["access_type"]);
			Assert.AreEqual("consent", query["prompt"]);
			Assert.AreEqual("S256", query["code_challenge_method"]);
			Assert.AreEqual("challenge", query["code_challenge"]);
			Assert.AreEqual("http://127.0.0.1:5000/", query["redirect_uri"]);
			Assert.AreEqual("state-1", query["state"]);
			StringAssert.Contains("spreadsheets.readonly", query["scope"]);
		}

		[Test]
		// ADMIT: GoogleSignIn.ReadClientFile reads the id and secret of a Desktop app client file.
		// RCR: GoogleSignIn.cs ReadClientFile — change `return (clientId, clientSecret);` to
		// `return (clientSecret, clientId);` → RED (the id is the secret). 2026-11-16
		public void ReadClientFile_DesktopClient_ReturnsIdAndSecret()
		{
			var client = GoogleSignIn.ReadClientFile(
				"{\"installed\":{\"client_id\":\"id.apps.googleusercontent.com\",\"client_secret\":\"test-secret\"}}");

			Assert.AreEqual("id.apps.googleusercontent.com", client.ClientId);
			Assert.AreEqual("test-secret", client.ClientSecret);
		}

		[Test]
		// ADMIT: GoogleSignIn.ReadClientFile rejects a Web application client file and says which client type to create,
		// because a web client cannot use the loopback redirect.
		// RCR: GoogleSignIn.cs ReadClientFile — change `file["installed"] is not JObject installed` to
		// `file.First?.First is not JObject installed` → RED (no InvalidDataException). 2026-11-16
		public void ReadClientFile_WebClient_ThrowsAskingForADesktopApp()
		{
			var exception = Assert.Throws<InvalidDataException>(() => GoogleSignIn.ReadClientFile(
				"{\"web\":{\"client_id\":\"id.apps.googleusercontent.com\",\"client_secret\":\"test-secret\"}}"));

			StringAssert.Contains("Desktop app", exception.Message);
		}

		[Test]
		// ADMIT: GoogleSignIn.ReadRedirect refuses a reply whose state differs from this sign-in's, so another page
		// cannot complete the sign-in with its own code.
		// RCR: GoogleSignIn.cs ReadRedirect — change `state != expectedState` to `state == null` → RED (the foreign
		// code is returned). 2026-11-16
		public void ReadRedirect_ForeignState_Throws()
		{
			var reply = new Dictionary<string, string> { ["code"] = "4/abc", ["state"] = "someone-else" };

			Assert.Throws<IOException>(() => GoogleSignIn.ReadRedirect(reply, "mine"));
		}

		[Test]
		// ADMIT: GoogleSignIn.ReadRedirect reports a declined consent screen in plain words.
		// RCR: GoogleSignIn.cs ReadRedirect — change `error == "access_denied"` to `error == "denied"` → RED (the
		// message is the raw error code). 2026-11-16
		public void ReadRedirect_AccessDenied_SaysTheSignInWasDeclined()
		{
			var reply = new Dictionary<string, string> { ["error"] = "access_denied", ["state"] = "mine" };

			var exception = Assert.Throws<IOException>(() => GoogleSignIn.ReadRedirect(reply, "mine"));

			StringAssert.Contains("declined", exception.Message);
		}

		[Test]
		// ADMIT: GoogleSignIn.EmailFromIdToken reads the e-mail claim from the id token's payload, shown as the
		// signed-in account.
		// RCR: GoogleSignIn.cs EmailFromIdToken — change `parts[1]` to `parts[0]` → RED (the header has no e-mail).
		// 2026-11-16
		public void EmailFromIdToken_Token_ReturnsTheEmailClaim()
		{
			var token = $"{Base64Url("{\"alg\":\"RS256\"}")}.{Base64Url("{\"email\":\"dev@example.com\",\"sub\":\"1\"}")}.signature";

			Assert.AreEqual("dev@example.com", GoogleSignIn.EmailFromIdToken(token));
		}

		private static Dictionary<string, string> Query(string url)
		{
			return new Uri(url).Query.TrimStart('?').Split('&').Select(pair => pair.Split('='))
				.ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));
		}

		private static string Base64Url(string text)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		}
	}
}
