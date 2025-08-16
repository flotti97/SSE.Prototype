using SSE.Core.Models;
using SSE.Cryptography;
using System.Numerics;

namespace SSE.Server
{
    /// <summary>
    /// Server-side implementation of the OXT protocol for boolean queries
    /// </summary>
    public class BooleanEncryptedStorageServer
    {
        private readonly BooleanEncryptedDatabase edb;

        public BooleanEncryptedStorageServer(BooleanEncryptedDatabase edb)
        {
            this.edb = edb;
        }

        /// <summary>
        /// Processes a search request according to the OXT protocol
        /// </summary>
        /// <param name="stag">Search token for the s-term</param>
        /// <param name="xtokenGenerator">Function to generate x-tokens for each iteration</param>
        /// <returns>List of encrypted document identifiers matching the query</returns>
        public List<byte[]> Search(string stag, Func<int, List<BigInteger>> xtokenGenerator)
        {
            // Retrieve tuples for the s-term using the stag
            var tuples = GetTuples(stag);
            var results = new List<byte[]>();

            // For each tuple (iteration c)
            for (int c = 0; c < tuples.Count; c++)
            {
                var (e, y) = tuples[c];

                // Get xtoken for this iteration
                var xtokens = xtokenGenerator(c);
                bool allMatch = true;

                // Check each xtoken
                for (int i = 0; i < xtokens.Count; i++)
                {
                    // Compute xtoken[c,i]^y and check if it's in XSet
                    BigInteger yValue = new BigInteger(y, isUnsigned: true);
                    BigInteger crossTagToCheck = BigInteger.ModPow(xtokens[i], yValue, CryptoUtils.Prime256Bit);

                    if (!edb.ContainsCrossTag(crossTagToCheck))
                    {
                        allMatch = false;
                        break;
                    }
                }

                // If all xtokens matched, add the encrypted document ID to results
                if (allMatch)
                {
                    results.Add(e);
                }
            }

            return results;
        }

        private List<(byte[] e, byte[] y)> GetTuples(string stag)
        {
            // In a real implementation, this would use some secure lookup method
            // Here we're simply returning the stored tuples associated with this stag
            return edb.Retrieve(stag);
        }
    }
}
