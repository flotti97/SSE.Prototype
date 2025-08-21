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

            bool isConjunctive = query.ConjunctiveTokenSets.Values.Any(ts => ts.Count > 0);

            for (int occurrenceIndex = 0; occurrenceIndex < postingList.Count; occurrenceIndex++)
            {
                var (encryptedDocumentId, yBytes) = postingList[occurrenceIndex];

                if (!isConjunctive)
                {
                    // Single-keyword query: accept directly
                    yield return encryptedDocumentId;
                    continue;
                }

                // Conjunctive case: missing token set => treat as non-match (NOT auto-accept)
                if (!query.ConjunctiveTokenSets.TryGetValue(occurrenceIndex, out var testTokens) || testTokens.Count == 0)
                {
                    continue;
                }

                var y = yBytes.AsBigInteger(); // ensure consistent unsigned big-endian extension

                bool allMatch = true;
                foreach (var xtokenBytes in testTokens)
                {
                    var xtoken = xtokenBytes.AsBigInteger();
                    var testVal = BigInteger.ModPow(xtoken, y, CryptoUtils.Prime256Bit);
                    if (!encryptedDatabase.ContainsMembershipTag(testVal))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    yield return encryptedDocumentId;
                }
            }
        }
    }
}
