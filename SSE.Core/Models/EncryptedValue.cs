using SSE.Cryptography;

namespace SSE.Core.Models
{
    public class EncryptedValue
    {
        public readonly byte[] Value;

        public EncryptedValue(string value, byte[] encryptionKey)
        {
            Value = CryptoUtils.Randomize(encryptionKey, value);
        }

        public override string ToString()
        {
            return Convert.ToBase64String(Value);
        }
    }
}
