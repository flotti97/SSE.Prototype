using SSE.Cryptography;
using System.Numerics;

namespace SSE.Core.Models
{
    public class BooleanEncryptedDatabase
    {
        private readonly HashSet<BigInteger> crossSet;
        private Dictionary<string, List<(byte[], byte[])>> secureIndex;

        public BooleanEncryptedDatabase(HashSet<BigInteger> crossSet)
        {
            this.crossSet = crossSet;
            this.secureIndex = new Dictionary<string, List<(byte[], byte[])>>();
        }

        public byte[] Setup(Dictionary<string, List<(byte[], byte[])>> tokenMap)
        {
            // Generate index key KT
            byte[] key = CryptoUtils.GenerateRandomKey();

            // For each keyword, derive a secure token and store its associated tuples
            foreach (var entry in tokenMap)
            {
                string keyword = entry.Key;
                List<(byte[], byte[])> tuples = entry.Value;

                // Generate secure lookup token using the index key
                string secureToken = Convert.ToBase64String(
                    CryptoUtils.Randomize(key, keyword));

                // Store the mapping from secure token to tuples
                secureIndex.Add(secureToken, tuples);
            }

            return key;
        }

        public List<(byte[], byte[])> GetTag(string encryptedKeyword)
        {

            // Retrieve the tuples associated with this token
            if (secureIndex.TryGetValue(encryptedKeyword, out var tuples))
            {
                return tuples;
            }

            return new List<(byte[], byte[])>();
        }



        public bool ContainsCrossTag(BigInteger crossTag)
        {
            return crossSet.Contains(crossTag);
        }

        /// <summary>
        /// Retrieves tuples associated with a specific search tag
        /// </summary>
        /// <param name="stag">The search tag (base64 encoded)</param>
        /// <returns>List of tuples (encrypted identifier, inverse cross identifier)</returns>
        public List<(byte[] e, byte[] y)> Retrieve(string stag)
        {
            // Lookup tuples using the secure token
            if (secureIndex.TryGetValue(stag, out var tuples))
            {
                return tuples;
            }

            return new List<(byte[], byte[])>();
        }
    }
}
