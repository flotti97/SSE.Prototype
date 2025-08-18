using SSE.Core;
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
        internal readonly BooleanEncryptedDatabase encryptedDatabase;

        public BooleanEncryptedStorageServer(BooleanEncryptedDatabase encryptedDatabase)
        {
            this.encryptedDatabase = encryptedDatabase;
        }

        public IEnumerable<byte[]> ProcessQuery(QueryMessage query)
        {
            var postingList = encryptedDatabase.RetrievePostingList(Convert.ToBase64String(query.Stag));

            for (int occurenceIndex = 0; occurenceIndex < postingList.Count; occurenceIndex++)
            {
                var (encryptedDocumentId, CounterAdjustedIndexFactorY) = postingList[occurenceIndex];

                // xtokens are indexed by c on the client
                if (!query.ConjunctiveTokenSets.TryGetValue(occurenceIndex, out var testTokens) || testTokens.Count == 0)
                {
                    // Single-keyword (or missing tokens) -> accept directly
                    yield return encryptedDocumentId;
                    continue;
                }

                bool allSecondaryKeywordsMatch = true;
                foreach (var testToken in testTokens)
                {
                    var testVal = BigInteger.ModPow(
                        testToken.AsBigInteger(),
                        CounterAdjustedIndexFactorY.AsBigInteger(),
                        CryptoUtils.Prime256Bit);

                    if (!encryptedDatabase.ContainsMembershipTag(testVal))
                    {
                        allSecondaryKeywordsMatch = false;
                        break;
                    }
                }

                if (allSecondaryKeywordsMatch)
                {
                    yield return encryptedDocumentId;
                }
            }
        }
    }
}
