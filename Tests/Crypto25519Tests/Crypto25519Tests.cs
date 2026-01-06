using System.Text;
using EchoPhase.Enums;
using EchoPhase.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoTests
{
	public class Crypto25519ServiceTests : IClassFixture<Crypto25519Fixture>
	{
		private readonly ITestOutputHelper _output;
		private readonly ICrypto25519Service _svc;

		public Crypto25519ServiceTests(Crypto25519Fixture fixture, ITestOutputHelper output)
		{
			_output = output;
			_svc = fixture.Provider.GetRequiredService<ICrypto25519Service>();
		}

		[Fact]
		public void SignVerify_Ed25519_Works()
		{
			var (pub, sec) = _svc.GenerateEd25519KeyPair();
			var msg = Encoding.UTF8.GetBytes("test-message");
			var sig = _svc.SignDetached(msg, sec);
			Assert.True(_svc.VerifyDetached(msg, sig, pub));
		}

		[Theory]
		[InlineData(AeadChoice.AesGcm)]
		[InlineData(AeadChoice.ChaCha20Poly1305)]
		[InlineData(AeadChoice.XChaCha20Poly1305)]
		public void EncryptDecrypt_X25519_MessagePack_Works(AeadChoice aead)
		{
			var (pub, sec) = _svc.GenerateX25519KeyPair();
			var msg = Encoding.UTF8.GetBytes("hello x25519! " + aead.ToString());

			var mp = _svc.EncryptToMessagePack(msg, pub, aead);
			var pt = _svc.DecryptFromMessagePack(mp, sec);
			Assert.Equal(msg, pt);
		}

		[Theory]
		[InlineData(AeadChoice.AesGcm)]
		[InlineData(AeadChoice.ChaCha20Poly1305)]
		[InlineData(AeadChoice.XChaCha20Poly1305)]
		public void EncryptDecrypt_X25519_Json_Works(AeadChoice aead)
		{
			var (pub, sec) = _svc.GenerateX25519KeyPair();
			var msg = Encoding.UTF8.GetBytes("hello x25519! " + aead.ToString());

			var json = _svc.EncryptToJson(msg, pub, aead);
			var pt = _svc.DecryptFromJson(json, sec);
			Assert.Equal(msg, pt);
		}
	}
}
