using SSE.Core.Models;
using SSE.Cryptography;
using System.Collections;

namespace SSE.Client.Schemes
{
    /// <summary>
    /// Implementation of the Cash et al. 2013 Searchable Symmetric Encryption basic scheme
    /// </summary>
    public class BasicScheme
    {
        public static (byte[], EncryptedDatabase) Setup(Database<(string, string)> db)
        {
            byte[] masterKey = CryptoUtils.GenerateRandomKey();
            var result = new List<(byte[] label, byte[] identifier)>();
            var metadata = db.GetMetadata();

            foreach (var keyword in metadata.Keys)
            {
                var labelKey = GetLabelKey(masterKey, keyword);
                var identifierKey = GetIdentifierKey(masterKey, keyword);

                int counter = 0;
                foreach (var identifier in metadata[keyword])
                {
                    var randomLabel = CryptoUtils.Randomize(labelKey, counter);
                    var encryptedIdentifier = CryptoUtils.Encrypt(identifierKey, identifier);
                    counter++;

                    result.Add((randomLabel, encryptedIdentifier));
                }
            }

            result.Sort((a, b) => StructuralComparisons.StructuralComparer.Compare(a.label, b.label));

            // Create a dictionary from the result list
            var dict = new Dictionary<byte[], byte[]>(new ByteArrayComparer());
            foreach ((var randomLabel, var encryptedIdentifier) in result)
            {
                dict.TryAdd(randomLabel, encryptedIdentifier);
            }

            return (masterKey, new EncryptedDatabase(dict));
        }

        public static (byte[] labelKey, byte[] identifierKey) DeriveSearchKeys(byte[] masterKey, string keyword)
        {
            var labelKey = GetLabelKey(masterKey, keyword);
            var identifierKey = GetIdentifierKey(masterKey, keyword);
            return (labelKey, identifierKey);
        }

        private static byte[] GetLabelKey(byte[] masterKey, string keyword)
        {
            return CryptoUtils.DeriveKey(masterKey, keyword, 1);
        }

        private static byte[] GetIdentifierKey(byte[] masterKey, string keyword)
        {
            return CryptoUtils.DeriveKey(masterKey, keyword, 2);
        }
    }
}
