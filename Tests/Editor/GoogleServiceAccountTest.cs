using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GameLovers.DataImporter.Editor.Tests
{
	public class GoogleServiceAccountTest
	{
		private const string Email = "importer@example-project.iam.gserviceaccount.com";
		private const string TokenUri = "https://oauth2.googleapis.com/token";

		private static readonly DateTime Now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

		private RSACryptoServiceProvider _rsa;
		private string _keyJson;

		[SetUp]
		public void SetUp()
		{
			_rsa = new RSACryptoServiceProvider(2048);
			_keyJson = KeyJson("service_account", Pem(Pkcs8(_rsa.ExportParameters(true))));
		}

		[TearDown]
		public void TearDown()
		{
			_rsa.Dispose();
		}

		[Test]
		// ADMIT: GoogleServiceAccount.ReadPrivateKey turns a PKCS#8 key into RSA parameters the Editor runtime can
		// sign with, so the assertion's signature verifies against the key's public half.
		// RCR: GoogleServiceAccount.cs ReadPrivateKey — change `P = LeftPad(prime1, half)` to `P = LeftPad(prime2, half)`
		// → RED (CryptographicException: Private/public key mismatch). Disabling the ReadInteger zero strip stayed
		// green: the Editor's Mono accepts a leading zero byte. 2026-11-16
		public void CreateAssertion_GeneratedKey_SignatureVerifiesWithThePublicKey()
		{
			var parts = GoogleServiceAccount.CreateAssertion(_keyJson, Now).Split('.');
			var signed = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");

			Assert.IsTrue(_rsa.VerifyData(signed, Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
		}

		[Test]
		// ADMIT: GoogleServiceAccount.CreateAssertion claims a one-hour read-only token for the key's account from the
		// key's token endpoint, which Google requires to accept the assertion.
		// RCR: GoogleServiceAccount.cs CreateAssertion — change `["aud"] = TokenUri(keyJson)` to
		// `["aud"] = ReadOnlyScope` → RED (aud is the scope URL). 2026-11-16
		public void CreateAssertion_Claims_NameTheAccountTokenEndpointAndReadOnlyScope()
		{
			var parts = GoogleServiceAccount.CreateAssertion(_keyJson, Now).Split('.');
			var header = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
			var claims = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
			var issuedAt = (long)(Now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

			Assert.AreEqual("RS256", header.Value<string>("alg"));
			Assert.AreEqual(Email, claims.Value<string>("iss"));
			Assert.AreEqual(TokenUri, claims.Value<string>("aud"));
			Assert.AreEqual("https://www.googleapis.com/auth/spreadsheets.readonly", claims.Value<string>("scope"));
			Assert.AreEqual(issuedAt, claims.Value<long>("iat"));
			Assert.AreEqual(issuedAt + 3600, claims.Value<long>("exp"));
		}

		[Test]
		// ADMIT: GoogleServiceAccount.CreateAssertion rejects a JSON file that is not a service-account key, such as
		// an OAuth client file, instead of failing later with a missing-field message.
		// RCR: GoogleServiceAccount.cs CreateAssertion — change `!= "service_account"` to `== "service_account"`
		// → RED (no exception for the OAuth client file). 2026-11-16
		public void CreateAssertion_OAuthClientFile_ThrowsNamingTheKeyType()
		{
			var oauthClient = KeyJson("authorized_user", Pem(Pkcs8(_rsa.ExportParameters(true))));

			var exception = Assert.Throws<InvalidDataException>(() => GoogleServiceAccount.CreateAssertion(oauthClient, Now));

			StringAssert.Contains("\"type\"", exception.Message);
		}

		[Test]
		// ADMIT: GoogleServiceAccount.ReadPrivateKey explains that a PKCS#1 `RSA PRIVATE KEY` is not the PKCS#8 key
		// Google issues, instead of failing on an index error.
		// RCR: GoogleServiceAccount.cs ReadPrivateKey — change `if (start < 0 || end < start)` to `if (end < start)`
		// → RED (ArgumentOutOfRangeException instead of InvalidDataException). 2026-11-16
		public void ReadPrivateKey_Pkcs1Pem_ThrowsNamingPkcs8()
		{
			var pkcs1 = "-----BEGIN RSA PRIVATE KEY-----\nMIIBOgIBAAJBAK\n-----END RSA PRIVATE KEY-----\n";

			var exception = Assert.Throws<InvalidDataException>(() => GoogleServiceAccount.ReadPrivateKey(pkcs1));

			StringAssert.Contains("PKCS#8", exception.Message);
		}

		[Test]
		// ADMIT: GoogleServiceAccount.Enter reads a DER length byte of 0x81 0x80 as 128 bytes; only a first length
		// byte of 0x80 is the unsupported indefinite form. 2048-bit keys often hold 128-byte values.
		// RCR: GoogleServiceAccount.cs Enter — change `if (position + length > der.Length)` to
		// `if (length == 0x80 || position + length > der.Length)` → RED (InvalidDataException: bad length). 2026-11-16
		public void ReadPrivateKey_128ByteValues_ReadsThemWhole()
		{
			var half = Enumerable.Repeat((byte)0x01, 128).ToArray();
			var key = new RSAParameters
			{
				Modulus = Enumerable.Repeat((byte)0x7F, 256).ToArray(),
				Exponent = new byte[] { 0x01, 0x00, 0x01 },
				D = Enumerable.Repeat((byte)0x02, 256).ToArray(),
				P = half, Q = half, DP = half, DQ = half, InverseQ = half
			};

			var read = GoogleServiceAccount.ReadPrivateKey(Pem(Pkcs8(key)));

			CollectionAssert.AreEqual(half, read.InverseQ);
			CollectionAssert.AreEqual(key.Modulus, read.Modulus);
		}

		private static string KeyJson(string type, string pem)
		{
			return new JObject
			{
				["type"] = type,
				["client_email"] = Email,
				["private_key"] = pem,
				["token_uri"] = TokenUri
			}.ToString();
		}

		private static string Pem(byte[] der)
		{
			var body = Convert.ToBase64String(der);
			var lines = Enumerable.Range(0, (body.Length + 63) / 64).Select(i => body.Substring(i * 64, Math.Min(64, body.Length - i * 64)));

			return $"-----BEGIN PRIVATE KEY-----\n{string.Join("\n", lines)}\n-----END PRIVATE KEY-----\n";
		}

		private static byte[] Pkcs8(RSAParameters key)
		{
			var rsaPrivateKey = Der(0x30, Integer(new byte[] { 0 }), Integer(key.Modulus), Integer(key.Exponent), Integer(key.D),
				Integer(key.P), Integer(key.Q), Integer(key.DP), Integer(key.DQ), Integer(key.InverseQ));
			var rsaEncryption = new byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01 };
			var algorithm = Der(0x30, Der(0x06, rsaEncryption), new byte[] { 0x05, 0x00 });

			return Der(0x30, Integer(new byte[] { 0 }), algorithm, Der(0x04, rsaPrivateKey));
		}

		private static byte[] Integer(byte[] value)
		{
			var trimmed = value.SkipWhile((b, i) => b == 0 && i < value.Length - 1).ToArray();

			return Der(0x02, (trimmed[0] & 0x80) != 0 ? new byte[] { 0 }.Concat(trimmed).ToArray() : trimmed);
		}

		private static byte[] Der(byte tag, params byte[][] parts)
		{
			var content = parts.SelectMany(part => part).ToArray();
			var length = content.Length < 0x80 ? new[] { (byte)content.Length }
						 : content.Length <= 0xFF ? new byte[] { 0x81, (byte)content.Length }
						 : new byte[] { 0x82, (byte)(content.Length >> 8), (byte)content.Length };

			return new[] { tag }.Concat(length).Concat(content).ToArray();
		}

		private static byte[] Base64UrlDecode(string text)
		{
			var base64 = text.Replace('-', '+').Replace('_', '/');

			return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
		}
	}
}
