using EchoPhase.Configuration.Cryptography.Crypto25519;

namespace EchoPhase.Security.Cryptography
{
    public interface ICrypto25519
    {
        (byte[] PublicKey, byte[] SecretKey) GenerateEd25519KeyPair();
        (byte[] PublicKey, byte[] SecretKey) GenerateX25519KeyPair();

        byte[] SignDetached(byte[] message, byte[] ed25519SecretKey);
        bool VerifyDetached(byte[] message, byte[] signature, byte[] ed25519PublicKey);

        EncryptedMessage EncryptForRecipientStructured(byte[] plaintext, byte[] recipientX25519PublicKey, out byte[] ephemeralSenderPublicKey, AeadChoice? aead = null);
        byte[] DecryptFromSenderStructured(EncryptedMessage box, byte[] recipientX25519SecretKey);

        string EncryptToJson(byte[] plaintext, byte[] recipientX25519PublicKey, AeadChoice? aead = null);
        byte[] DecryptFromJson(string json, byte[] recipientX25519SecretKey);

        byte[] EncryptToMessagePack(byte[] plaintext, byte[] recipientX25519PublicKey, AeadChoice? aead = null);
        byte[] DecryptFromMessagePack(byte[] mp, byte[] recipientX25519SecretKey);

        EncryptedMessage SealToRecipient(byte[] plaintext, byte[] recipientX25519PublicKey, AeadChoice? aead = null);
        byte[] UnsealFromAnonymous(EncryptedMessage sealedBox, byte[] recipientX25519SecretKey);

        byte[] ConvertEd25519SecretKeyToX25519(byte[] ed25519SecretKey);
    }
}
