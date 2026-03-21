// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

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
