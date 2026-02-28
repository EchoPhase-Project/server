using EchoPhase.Configuration.Cryptography.Crypto25519;
using MessagePack;

namespace EchoPhase.Security.Cryptography
{
    [MessagePackObject]
    public class EncryptedMessage
    {
        [Key(0)] public byte[] EphemeralPublicKey { get; set; } = Array.Empty<byte>();
        [Key(1)] public byte[] Nonce { get; set; } = Array.Empty<byte>();
        [Key(2)] public byte[] CipherText { get; set; } = Array.Empty<byte>();
        [Key(3)] public byte[] Tag { get; set; } = Array.Empty<byte>();
        [Key(4)] public AeadChoice Aead { get; set; } = AeadChoice.ChaCha20Poly1305;
    }
}
