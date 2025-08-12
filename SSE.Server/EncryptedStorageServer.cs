using SSE.Core.Models;
using SSE.Cryptography;

namespace SSE.Server
{
    public class EncryptedStorageServer
    {
        private readonly EncryptedDatabase edb;

        public EncryptedStorageServer(EncryptedDatabase edb)
        {
            this.edb = edb;
        }

        public IEnumerable<string> Find(byte[] labelKey, byte[] identifierKey)
        {
            int counter = 0;
            var randomLabel = CryptoUtils.Randomize(labelKey, counter);
            var encryptedIdentifier = edb.Get(randomLabel);

            while (encryptedIdentifier != null)
            {
                yield return CryptoUtils.Decrypt(identifierKey, encryptedIdentifier);
                counter++;
                randomLabel = CryptoUtils.Randomize(labelKey, counter);
                encryptedIdentifier = edb.Get(randomLabel);
            }
        }
    }
}
