using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameLovers.DataImporter.Editor
{
	/// <summary>Reads a Google service-account key and builds the assertion it trades for a token.</summary>
	internal static class GoogleServiceAccount
	{
		/// <summary>Google's OAuth token endpoint.</summary>
		internal const string DefaultTokenUri = "https://oauth2.googleapis.com/token";

		private const string ReadOnlyScope = "https://www.googleapis.com/auth/spreadsheets.readonly";
		private const string PemHeader = "-----BEGIN PRIVATE KEY-----";
		private const string PemFooter = "-----END PRIVATE KEY-----";
		private const int AssertionLifetimeSeconds = 3600;
		private const byte SequenceTag = 0x30;
		private const byte IntegerTag = 0x02;
		private const byte OctetStringTag = 0x04;

		private static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		/// <summary>The service account's e-mail address, which a private sheet must be shared with.</summary>
		internal static string ClientEmail(string keyJson)
		{
			return Field(JObject.Parse(keyJson), "client_email");
		}

		/// <summary>The token endpoint of the key, or Google's default one when the key does not name it.</summary>
		internal static string TokenUri(string keyJson)
		{
			var tokenUri = JObject.Parse(keyJson).Value<string>("token_uri");

			return string.IsNullOrWhiteSpace(tokenUri) ? DefaultTokenUri : tokenUri;
		}

		/// <summary>Builds the RS256-signed assertion for a read-only token, issued at the given time.</summary>
		internal static string CreateAssertion(string keyJson, DateTime utcNow)
		{
			var key = JObject.Parse(keyJson);

			if (key.Value<string>("type") != "service_account")
			{
				throw new InvalidDataException("The key file is not a Google service-account key: its \"type\" is not \"service_account\"");
			}

			var issuedAt = (long)(utcNow - _unixEpoch).TotalSeconds;
			var claims = new JObject
			{
				["iss"] = Field(key, "client_email"),
				["scope"] = ReadOnlyScope,
				["aud"] = TokenUri(keyJson),
				["iat"] = issuedAt,
				["exp"] = issuedAt + AssertionLifetimeSeconds
			};
			var input = $"{Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"))}." +
						Base64Url(Encoding.UTF8.GetBytes(claims.ToString(Formatting.None)));

			using var rsa = RSA.Create();

			rsa.ImportParameters(ReadPrivateKey(Field(key, "private_key")));

			var signature = rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

			return $"{input}.{Base64Url(signature)}";
		}

		/// <summary>
		/// Reads an unencrypted PKCS#8 PEM RSA key. The Editor's Mono runtime cannot import PKCS#8 itself.
		/// </summary>
		internal static RSAParameters ReadPrivateKey(string pem)
		{
			var start = pem.IndexOf(PemHeader, StringComparison.Ordinal);
			var end = pem.IndexOf(PemFooter, StringComparison.Ordinal);

			if (start < 0 || end < start)
			{
				throw new InvalidDataException($"The private key is not a PKCS#8 PEM key: it has no {PemHeader} block");
			}

			var body = pem.Substring(start + PemHeader.Length, end - start - PemHeader.Length);
			var der = Convert.FromBase64String(body.Replace("\\n", string.Empty));
			var position = 0;

			Enter(der, ref position, SequenceTag);
			ReadInteger(der, ref position);

			var algorithmLength = Enter(der, ref position, SequenceTag);

			position += algorithmLength;
			Enter(der, ref position, OctetStringTag);
			Enter(der, ref position, SequenceTag);
			ReadInteger(der, ref position);

			var modulus = ReadInteger(der, ref position);
			var exponent = ReadInteger(der, ref position);
			var privateExponent = ReadInteger(der, ref position);
			var prime1 = ReadInteger(der, ref position);
			var prime2 = ReadInteger(der, ref position);
			var exponent1 = ReadInteger(der, ref position);
			var exponent2 = ReadInteger(der, ref position);
			var coefficient = ReadInteger(der, ref position);
			var half = (modulus.Length + 1) / 2;

			return new RSAParameters
			{
				Modulus = modulus,
				Exponent = exponent,
				D = LeftPad(privateExponent, modulus.Length),
				P = LeftPad(prime1, half),
				Q = LeftPad(prime2, half),
				DP = LeftPad(exponent1, half),
				DQ = LeftPad(exponent2, half),
				InverseQ = LeftPad(coefficient, half)
			};
		}

		private static string Field(JObject key, string name)
		{
			var value = key.Value<string>(name);

			if (string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidDataException($"The service-account key has no \"{name}\"");
			}

			return value;
		}

		private static int Enter(byte[] der, ref int position, byte tag)
		{
			if (position + 2 > der.Length || der[position] != tag)
			{
				throw new InvalidDataException($"The private key is not an RSA PKCS#8 key: unexpected data at byte {position}");
			}

			position++;

			var length = (int)der[position++];

			if (length == 0x80)
			{
				throw new InvalidDataException($"The private key is not an RSA PKCS#8 key: indefinite length at byte {position}");
			}

			if (length > 0x80)
			{
				var count = length & 0x7F;

				length = 0;

				for (var i = 0; i < count; i++)
				{
					length = (length << 8) | der[position++];
				}
			}

			if (position + length > der.Length)
			{
				throw new InvalidDataException($"The private key is not an RSA PKCS#8 key: bad length at byte {position}");
			}

			return length;
		}

		private static byte[] ReadInteger(byte[] der, ref int position)
		{
			var length = Enter(der, ref position, IntegerTag);
			var start = position;

			position += length;

			while (length > 1 && der[start] == 0)
			{
				start++;
				length--;
			}

			var value = new byte[length];

			Array.Copy(der, start, value, 0, length);

			return value;
		}

		private static byte[] LeftPad(byte[] value, int size)
		{
			if (value.Length >= size)
			{
				return value;
			}

			var padded = new byte[size];

			Array.Copy(value, 0, padded, size - value.Length, value.Length);

			return padded;
		}

		private static string Base64Url(byte[] bytes)
		{
			return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		}
	}
}
