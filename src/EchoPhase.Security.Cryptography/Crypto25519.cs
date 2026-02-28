using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using EchoPhase.Configuration.Cryptography.Crypto25519;
using MessagePack;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using SystemAesGcm = System.Security.Cryptography.AesGcm;

namespace EchoPhase.Security.Cryptography
{
    public sealed class Crypto25519 : ICrypto25519
    {
        private readonly SecureRandom _random;
        private readonly Crypto25519Settings _settings;

        public Crypto25519(IOptions<Crypto25519Settings> settings)
        {
            _random = new SecureRandom(new CryptoApiRandomGenerator());
            _settings = settings.Value;
        }

        // ------------------ Key generation ------------------
        public (byte[] PublicKey, byte[] SecretKey) GenerateEd25519KeyPair()
        {
            var seed = new byte[32];
            _random.NextBytes(seed);
            var priv = new Ed25519PrivateKeyParameters(seed, 0);
            var pub = priv.GeneratePublicKey();
            return (pub.GetEncoded(), priv.GetEncoded());
        }

        public (byte[] PublicKey, byte[] SecretKey) GenerateX25519KeyPair()
        {
            var priv = new X25519PrivateKeyParameters(_random);
            var pub = priv.GeneratePublicKey();
            return (pub.GetEncoded(), priv.GetEncoded());
        }

        // ------------------ Signatures ------------------
        public byte[] SignDetached(byte[] message, byte[] ed25519SecretKey)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (ed25519SecretKey == null) throw new ArgumentNullException(nameof(ed25519SecretKey));
            var priv = new Ed25519PrivateKeyParameters(ed25519SecretKey, 0);
            var signer = new Ed25519Signer();
            signer.Init(true, priv);
            signer.BlockUpdate(message, 0, message.Length);
            return signer.GenerateSignature();
        }

        public bool VerifyDetached(byte[] message, byte[] signature, byte[] ed25519PublicKey)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (signature == null) throw new ArgumentNullException(nameof(signature));
            if (ed25519PublicKey == null) throw new ArgumentNullException(nameof(ed25519PublicKey));
            var pub = new Ed25519PublicKeyParameters(ed25519PublicKey, 0);
            var verifier = new Ed25519Signer();
            verifier.Init(false, pub);
            verifier.BlockUpdate(message, 0, message.Length);
            return verifier.VerifySignature(signature);
        }

        // ------------------ Structured encrypt/decrypt ------------------
        public EncryptedMessage EncryptForRecipientStructured(
            byte[] plaintext,
            byte[] recipientX25519PublicKey,
            out byte[] ephemeralSenderPublicKey,
            AeadChoice? aead = null)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
            if (recipientX25519PublicKey == null) throw new ArgumentNullException(nameof(recipientX25519PublicKey));

            var ephPriv = new X25519PrivateKeyParameters(_random);
            var ephPub = ephPriv.GeneratePublicKey();
            ephemeralSenderPublicKey = ephPub.GetEncoded();

            var recipientPub = new X25519PublicKeyParameters(recipientX25519PublicKey, 0);
            var shared = new byte[32];
            var agree = new X25519Agreement();
            agree.Init(ephPriv);
            agree.CalculateAgreement(recipientPub, shared, 0);

            var aeadKey = HkdfSha256(shared, null, null, 32);

            var strategies = new Dictionary<AeadChoice, Func<EncryptedMessage>>()
            {
                [AeadChoice.AesGcm] = () =>
                {
                    var nonce = new byte[12]; _random.NextBytes(nonce);
                    var cipher = new byte[plaintext.Length];
                    var tag = new byte[16];
                    using (var aes = new SystemAesGcm(aeadKey, 16))
                    {
                        aes.Encrypt(nonce, plaintext, cipher, tag, null);
                    }
                    return new EncryptedMessage
                    {
                        EphemeralPublicKey = ephPub.GetEncoded(),
                        Nonce = nonce,
                        CipherText = cipher,
                        Tag = tag,
                        Aead = AeadChoice.AesGcm
                    };
                },
                [AeadChoice.ChaCha20Poly1305] = () =>
                {
                    var nonce = new byte[12]; _random.NextBytes(nonce);
                    var cipher = new byte[plaintext.Length];
                    var tag = new byte[16];
                    using (var ch = new ChaCha20Poly1305(aeadKey))
                    {
                        ch.Encrypt(nonce, plaintext, cipher, tag, null);
                    }
                    return new EncryptedMessage
                    {
                        EphemeralPublicKey = ephPub.GetEncoded(),
                        Nonce = nonce,
                        CipherText = cipher,
                        Tag = tag,
                        Aead = AeadChoice.ChaCha20Poly1305
                    };
                },
                [AeadChoice.XChaCha20Poly1305] = () =>
                {
                    var nonce24 = new byte[24]; _random.NextBytes(nonce24);
                    var subKey = HChaCha20(aeadKey, nonce24.AsSpan(0, 16));

                    var nonce12 = new byte[12];
                    Buffer.BlockCopy(nonce24, 16, nonce12, 4, 8);

                    var cipher = new byte[plaintext.Length];
                    var tag = new byte[16];
                    using (var ch = new ChaCha20Poly1305(subKey))
                    {
                        ch.Encrypt(nonce12, plaintext, cipher, tag, null);
                    }

                    return new EncryptedMessage
                    {
                        EphemeralPublicKey = ephPub.GetEncoded(),
                        Nonce = nonce24,
                        CipherText = cipher,
                        Tag = tag,
                        Aead = AeadChoice.XChaCha20Poly1305
                    };
                }
            };

            var strategy = aead ?? _settings.AeadChoice;
            if (!strategies.TryGetValue(strategy, out var encryptFunc))
                throw new NotSupportedException($"AEAD {strategy} не поддерживается");

            return encryptFunc();
        }

        public byte[] DecryptFromSenderStructured(EncryptedMessage box, byte[] recipientX25519SecretKey)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));
            if (recipientX25519SecretKey == null) throw new ArgumentNullException(nameof(recipientX25519SecretKey));

            var pub = new X25519PublicKeyParameters(box.EphemeralPublicKey, 0);
            var priv = new X25519PrivateKeyParameters(recipientX25519SecretKey, 0);
            var shared = new byte[32];
            var agree = new X25519Agreement();
            agree.Init(priv);
            agree.CalculateAgreement(pub, shared, 0);

            var aeadKey = HkdfSha256(shared, null, null, 32);
            var plaintext = new byte[box.CipherText.Length];

            var decryptors = new Dictionary<AeadChoice, Action>
            {
                [AeadChoice.AesGcm] = () =>
                {
                    using var aes = new SystemAesGcm(aeadKey, 16);
                    aes.Decrypt(box.Nonce, box.CipherText, box.Tag, plaintext, null);
                },
                [AeadChoice.ChaCha20Poly1305] = () =>
                {
                    using var ch = new ChaCha20Poly1305(aeadKey);
                    ch.Decrypt(box.Nonce, box.CipherText, box.Tag, plaintext, null);
                },
                [AeadChoice.XChaCha20Poly1305] = () =>
                {
                    if (box.Nonce == null || box.Nonce.Length != 24)
                        throw new ArgumentException("XChaCha20 nonce must be 24 bytes");

                    var subKey = HChaCha20(aeadKey, box.Nonce.AsSpan(0, 16));
                    var nonce12 = new byte[12];
                    Buffer.BlockCopy(box.Nonce, 16, nonce12, 4, 8);

                    using var ch = new ChaCha20Poly1305(subKey);
                    ch.Decrypt(nonce12, box.CipherText, box.Tag, plaintext, null);
                }
            };

            try
            {
                if (!decryptors.TryGetValue(box.Aead, out var decryptor))
                    throw new NotSupportedException($"Unsupported AEAD: {box.Aead}");

                decryptor();
                return plaintext;
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("Decryption/authentication failed", ex);
            }
        }

        // ------------------ Serialization conveniences ------------------
        public string EncryptToJson(byte[] plaintext, byte[] recipientX25519PublicKey, AeadChoice? aead = null)
        {
            var box = EncryptForRecipientStructured(plaintext, recipientX25519PublicKey, out var _, aead);
            return JsonSerializer.Serialize(box);
        }

        public byte[] DecryptFromJson(string json, byte[] recipientX25519SecretKey)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json));
            if (recipientX25519SecretKey == null)
                throw new ArgumentNullException(nameof(recipientX25519SecretKey));

            var box = JsonSerializer.Deserialize<EncryptedMessage>(json);
            if (box == null)
                throw new ArgumentException("Invalid or empty encrypted message JSON", nameof(json));

            return DecryptFromSenderStructured(box, recipientX25519SecretKey);
        }

        public byte[] EncryptToMessagePack(byte[] plaintext, byte[] recipientX25519PublicKey, AeadChoice? aead = null)
        {
            var box = EncryptForRecipientStructured(plaintext, recipientX25519PublicKey, out var _, aead);
            return MessagePackSerializer.Serialize(box);
        }

        public byte[] DecryptFromMessagePack(byte[] mp, byte[] recipientX25519SecretKey)
        {
            var box = MessagePackSerializer.Deserialize<EncryptedMessage>(mp);
            return DecryptFromSenderStructured(box, recipientX25519SecretKey);
        }

        // ------------------ Sealed-box (anonymous) ------------------
        public EncryptedMessage SealToRecipient(byte[] plaintext, byte[] recipientX25519PublicKey, AeadChoice? aead = null)
        {
            return EncryptForRecipientStructured(plaintext, recipientX25519PublicKey, out var _, aead);
        }

        public byte[] UnsealFromAnonymous(EncryptedMessage sealedBox, byte[] recipientX25519SecretKey)
        {
            return DecryptFromSenderStructured(sealedBox, recipientX25519SecretKey);
        }

        // ------------------ Conversions ------------------
        public byte[] ConvertEd25519SecretKeyToX25519(byte[] ed25519SecretKey)
        {
            if (ed25519SecretKey == null) throw new ArgumentNullException(nameof(ed25519SecretKey));
            byte[] seed;
            if (ed25519SecretKey.Length == 64)
            {
                seed = new byte[32]; Buffer.BlockCopy(ed25519SecretKey, 0, seed, 0, 32);
            }
            else if (ed25519SecretKey.Length == 32)
            {
                seed = ed25519SecretKey;
            }
            else
            {
                throw new ArgumentException("Ed25519 secret must be 32 or 64 bytes");
            }

            using (var sha = SHA512.Create())
            {
                var h = sha.ComputeHash(seed);
                var scalar = new byte[32]; Array.Copy(h, 0, scalar, 0, 32);
                scalar[0] &= 248; scalar[31] &= 127; scalar[31] |= 64;
                return scalar;
            }
        }

        // ------------------ Helpers ------------------
        private static byte[] HkdfSha256(byte[] ikm, byte[]? salt, byte[]? info, int length)
        {
            var hkdf = new HkdfBytesGenerator(new Sha256Digest());
            hkdf.Init(new HkdfParameters(ikm, salt, info));
            var okm = new byte[length]; hkdf.GenerateBytes(okm, 0, length);
            return okm;
        }

        private static byte[] HChaCha20(byte[] key32, ReadOnlySpan<byte> nonce16)
        {
            if (key32 == null || key32.Length != 32) throw new ArgumentException("key32 must be 32 bytes");
            if (nonce16.Length != 16) throw new ArgumentException("nonce16 must be 16 bytes");

            uint[] state = new uint[16];
            state[0] = 0x61707865;
            state[1] = 0x3320646e;
            state[2] = 0x79622d32;
            state[3] = 0x6b206574;

            for (int i = 0; i < 8; i++)
            {
                state[4 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key32.AsSpan(i * 4, 4));
            }

            for (int i = 0; i < 4; i++)
            {
                state[12 + i] = BinaryPrimitives.ReadUInt32LittleEndian(nonce16.Slice(i * 4, 4));
            }

            void QuarterRound(uint[] x, int a, int b, int c, int d)
            {
                x[a] += x[b]; x[d] ^= x[a]; x[d] = (x[d] << 16) | (x[d] >> 16);
                x[c] += x[d]; x[b] ^= x[c]; x[b] = (x[b] << 12) | (x[b] >> 20);
                x[a] += x[b]; x[d] ^= x[a]; x[d] = (x[d] << 8) | (x[d] >> 24);
                x[c] += x[d]; x[b] ^= x[c]; x[b] = (x[b] << 7) | (x[b] >> 25);
            }

            var working = new uint[16];
            Array.Copy(state, working, 16);

            for (int i = 0; i < 10; i++)
            {
                QuarterRound(working, 0, 4, 8, 12);
                QuarterRound(working, 1, 5, 9, 13);
                QuarterRound(working, 2, 6, 10, 14);
                QuarterRound(working, 3, 7, 11, 15);
                QuarterRound(working, 0, 5, 10, 15);
                QuarterRound(working, 1, 6, 11, 12);
                QuarterRound(working, 2, 7, 8, 13);
                QuarterRound(working, 3, 4, 9, 14);
            }

            var outBytes = new byte[32];
            BinaryPrimitives.WriteUInt32LittleEndian(outBytes.AsSpan(0, 4), working[0]);
            BinaryPrimitives.WriteUInt32LittleEndian(outBytes.AsSpan(4, 4), working[1]);
            BinaryPrimitives.WriteUInt32LittleEndian(outBytes.AsSpan(8, 4), working[2]);
            BinaryPrimitives.WriteUInt32LittleEndian(outBytes.AsSpan(12, 4), working[3]);
            BinaryPrimitives.WriteUInt32LittleEndian(outBytes.AsSpan(16, 4), working[12]);
            BinaryPrimitives.WriteUInt32LittleEndian(outBytes.AsSpan(20, 4), working[13]);
            BinaryPrimitives.WriteUInt32LittleEndian(outBytes.AsSpan(24, 4), working[14]);
            BinaryPrimitives.WriteUInt32LittleEndian(outBytes.AsSpan(28, 4), working[15]);
            return outBytes;
        }
    }
}

