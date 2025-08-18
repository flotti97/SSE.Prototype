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
        internal readonly BooleanEncryptedDatabase edb;

        public BooleanEncryptedStorageServer(BooleanEncryptedDatabase edb)
        {
            this.edb = edb;
        }

        public IEnumerable<byte[]> ProcessQuery(QueryMessage msg)
        {
            var tuples = edb.Retrieve(Convert.ToBase64String(msg.Stag));

            for (int c = 0; c < tuples.Count; c++)
            {
                var (e, y) = tuples[c];

                // xtokens are indexed by c on the client
                if (!msg.ConjunctiveTokenSets.TryGetValue(c, out var xtokens) || xtokens.Count == 0)
                {
                    // Single-keyword (or missing tokens) -> accept directly
                    yield return e;
                    continue;
                }

                bool allMatch = true;
                foreach (var xt in xtokens)
                {
                    var testVal = BigInteger.ModPow(xt.AsBigInteger(), y.AsBigInteger(), CryptoUtils.Prime256Bit);
                    if (!edb.ElementOfCrossSet(testVal))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    yield return e;
                }
            }
        }
    }
}
