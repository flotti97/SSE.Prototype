using SSE.Core.Models;
using SSE.Cryptography;
using SSE.Server;
using System.Numerics;

namespace SSE.Client.Schemes
{
    /// <summary>
    /// This is an implementation of the Oblivious Cross-Tags (OXT) protocol for Searchable Symmetric Encryption as proposed by Cash et al. 2013.
    /// </summary>
    public class BooleanQueryScheme
    {
        private KeyCollection keys = new();

        private static readonly BigInteger P = CryptoUtils.Prime256Bit;
        private static readonly BigInteger Pm1 = CryptoUtils.Prime256Bit - 1; // exponent group order

        public BooleanQueryScheme()
        {
            keys.DocumentEncryptionKeySeed = CryptoUtils.GenerateRandomKey();
            keys.KeywordTagKey = CryptoUtils.GenerateRandomKey();
            keys.DocumentIndexKey = CryptoUtils.GenerateRandomKey();
            keys.KeywordCounterKey = CryptoUtils.GenerateRandomKey();
        }

        public (KeyCollection, BooleanEncryptedDatabase) Setup(Database<(string, string)> db)
        {
            Dictionary<string, List<string>> metadata = db.GetMetadata();
            List<string> keywords = metadata.Keys.ToList();

            Dictionary<string, List<(byte[], byte[])>> tokenMap = new();
            HashSet<BigInteger> crossTags = new();

            foreach (string keyword in keywords)
            {
                List<(byte[], byte[])> tokens = new();
                byte[] labelKey = CryptoUtils.Randomize(keys.DocumentEncryptionKeySeed, keyword);

                List<string> identifiers = metadata[keyword].OrderBy(_ => Guid.NewGuid()).ToList();
                int counter = 0;
                foreach (var identifier in identifiers)
                {
                    var crossIdentifier = CryptoUtils.Randomize(keys.DocumentIndexKey, identifier, P);
                    var crossIdentifierExp = crossIdentifier % Pm1;
                    if (crossIdentifierExp.IsZero) crossIdentifierExp = 1;

                    var tokenTuple = GenerateTokenTuple(keyword, counter, crossIdentifierExp, labelKey, identifier);
                    tokens.Add(tokenTuple);

                    var crossTag = GenerateCrossTag(keyword, crossIdentifierExp);
                    crossTags.Add(crossTag);

                    counter++;
                }

                tokenMap.Add(keyword, tokens);
            }

            BooleanEncryptedDatabase edb = new(crossTags);
            keys.SearchTagKey = edb.Setup(tokenMap);
            return (keys, edb);
        }

        private static BigInteger DeriveZ(byte[] tagKey, string keyword, int counter)
        {
            // Deterministic base value
            var raw = CryptoUtils.DeriveKey(tagKey, keyword, counter);
            var z = new BigInteger(raw, isBigEndian: true, isUnsigned: true) % Pm1;
            if (z <= 1) z = 2;

            // Ensure gcd(z, p-1) == 1 (so inverse exists)
            while (BigInteger.GreatestCommonDivisor(z, Pm1) != 1)
            {
                z++;
                if (z >= Pm1) z = 2;
            }
            return z;
        }

        private (byte[] encryptedIdentifier, byte[] inverseCrossIdentifier) GenerateTokenTuple(string keyword, int counter, BigInteger crossIdentifierExp, byte[] labelKey, string identifier)
        {
            var zVal = DeriveZ(keys.KeywordCounterKey, keyword, counter);
            var zValBytes = zVal.ToByteArray(isUnsigned: true, isBigEndian: true);
            var zInverse = CryptoUtils.ModInverse(zValBytes, Pm1);

            var yBig = CryptoUtils.ModMultiply(crossIdentifierExp, zInverse, Pm1);
            var y = yBig.ToByteArray(isUnsigned: true, isBigEndian: true);

            var e = CryptoUtils.Encrypt(labelKey, identifier);
            return (e, y);
        }

        private BigInteger GenerateCrossTag(string keyword, BigInteger crossIdentifierExp)
        {
            var crossTagRandomizer = CryptoUtils.Randomize(keys.KeywordTagKey, keyword);
            var randomizerValue = new BigInteger(crossTagRandomizer, isBigEndian: true, isUnsigned: true) % Pm1;
            if (randomizerValue.IsZero) randomizerValue = 1;

            var exponent = CryptoUtils.ModMultiply(randomizerValue, crossIdentifierExp, Pm1);
            return BigInteger.ModPow(
                CryptoUtils.Generator(P),
                exponent,
                P
            );
        }

        public IEnumerable<string> Search(BooleanEncryptedStorageServer server, params string[] keywords)
        {
            QueryMessage query = GenerateQuery(keywords);
            var encryptedResults = server.ProcessQuery(query);
            return encryptedResults.Select(x => GetInd(keywords, x));
        }

        private string GetInd(string[] keywords, byte[] e)
        {
            var eKey = new EncryptedValue(keywords[0], keys.DocumentEncryptionKeySeed);
            return CryptoUtils.Decrypt(eKey.Value, e);
        }

        private byte[] ComputeTheThing(string firstKeyword, string currentKeyword, int counter)
        {
            var zVal = DeriveZ(keys.KeywordCounterKey, firstKeyword, counter);

            var kxBytes = CryptoUtils.Randomize(keys.KeywordTagKey, currentKeyword);
            var kx = new BigInteger(kxBytes, isBigEndian: true, isUnsigned: true) % Pm1;
            if (kx.IsZero) kx = 1;

            var exponent = CryptoUtils.ModMultiply(zVal, kx, Pm1);

            var xtoken = BigInteger.ModPow(
                CryptoUtils.Generator(P),
                exponent,
                P
            );

            return xtoken.ToByteArray(isUnsigned: true, isBigEndian: true);
        }

        public QueryMessage GenerateQuery(string[] keywords)
        {
            var w1 = keywords[0];
            var stag = new EncryptedValue(w1, keys.SearchTagKey).Value;
            var msg = new QueryMessage { Stag = stag };

            int c = 0;
            int limit = 2048;

            while (c < limit)
            {
                var xtokensForC = new List<byte[]>();
                for (int i = 1; i < keywords.Length; i++)
                {
                    var xtoken = ComputeTheThing(keywords[0], keywords[i], c);
                    xtokensForC.Add(xtoken);
                }
                msg.XTokens[c] = xtokensForC;

                if (keywords.Length == 1) break;
                c++;
            }

            return msg;
        }
    }

    public record struct KeyCollection()
    {
        private const int KEY_SIZE = 32;
        public byte[] DocumentEncryptionKeySeed { get; set; } = new byte[KEY_SIZE];
        public byte[] KeywordTagKey { get; set; } = new byte[KEY_SIZE];
        public byte[] DocumentIndexKey { get; set; } = new byte[KEY_SIZE];
        public byte[] KeywordCounterKey { get; set; } = new byte[KEY_SIZE];
        public byte[] SearchTagKey { get; set; } = new byte[KEY_SIZE];
    }
}