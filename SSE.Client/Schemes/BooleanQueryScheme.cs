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

        private static readonly BigInteger PrimeModulus = CryptoUtils.Prime256Bit;
        private static readonly BigInteger ExponentGroupOrder = CryptoUtils.Prime256Bit - 1;

        public BooleanQueryScheme()
        {
            keys.DocumentEncryptionKeySeed = CryptoUtils.GenerateRandomKey();
            keys.KeywordTagKey = CryptoUtils.GenerateRandomKey();
            keys.DocumentIndexKey = CryptoUtils.GenerateRandomKey();
            keys.KeywordCounterKey = CryptoUtils.GenerateRandomKey();
        }

        public (KeyCollection, BooleanEncryptedDatabase) Setup(Database<(string, string)> db)
        {
            Dictionary<string, List<string>> keywordToDocumentIds = db.GetMetadata();
            List<string> keywords = keywordToDocumentIds.Keys.ToList();

            Dictionary<string, List<(byte[] EncryptedDocId, byte[] InvertedCounterAppliedIndex)>> keywordPostingLists = new();
            HashSet<BigInteger> conjunctiveMembershipTagSet = new();

            foreach (string keyword in keywords)
            {
                List<(byte[] EncryptedDocId, byte[] InvertedCounterAppliedIndex)> postingList = new();
                byte[] perKeywordEncryptionKey = CryptoUtils.Randomize(keys.DocumentEncryptionKeySeed, keyword);

                List<string> shuffledDocIds = keywordToDocumentIds[keyword];
                shuffledDocIds.ShuffleInPlace();

                int occurenceIndex = 0;
                foreach (var documentId in shuffledDocIds)
                {
                    var randomizedDocumentIndex = CryptoUtils.Randomize(keys.DocumentIndexKey, documentId, PrimeModulus);
                    var documentIndexExponent = randomizedDocumentIndex % ExponentGroupOrder;
                    if (documentIndexExponent.IsZero) documentIndexExponent = 1;

                    var postingTuple = BuildPostingTuple(keyword, occurenceIndex, documentIndexExponent, perKeywordEncryptionKey, documentId);
                    postingList.Add(postingTuple);

                    var membershipTag = BuildConjunctiveMembershipTag(keyword, documentIndexExponent);
                    conjunctiveMembershipTagSet.Add(membershipTag);

                    occurenceIndex++;
                }

                keywordPostingLists.Add(keyword, postingList);
            }

            BooleanEncryptedDatabase encryptedDatabase = new(conjunctiveMembershipTagSet);
            keys.SearchTagKey = encryptedDatabase.Setup(keywordPostingLists);
            return (keys, encryptedDatabase);
        }

        /// <summary>
        /// Derives the per-(keyword, occurrenceIndex) counter factor z in exponent domain ensuring invertibility mod (p-1).
        /// </summary>
        private static BigInteger DeriveKeywordOccurrenceFactor(byte[] keywordCounterKey, string keyword, int occurenceIndex)
        {
            // Deterministic base value
            var raw = CryptoUtils.DeriveKey(keywordCounterKey, keyword, occurenceIndex);
            var factor = new BigInteger(raw, isBigEndian: true, isUnsigned: true) % ExponentGroupOrder;
            if (factor <= 1) factor = 2;

            // Ensure gcd(z, p-1) == 1 (so inverse exists)
            while (BigInteger.GreatestCommonDivisor(factor, ExponentGroupOrder) != 1)
            {
                factor++;
                if (factor >= ExponentGroupOrder) factor = 2;
            }
            return factor;
        }

        /// <summary>
        /// Builds a posting list tuple (Enc(documentId), y) where y = (documentIndexExponent * z^{-1}) mod (p-1).
        /// </summary>
        private (byte[] EncryptedDocId, byte[] InvertedCounterAppliedIndex) BuildPostingTuple(string keyword, int counter, BigInteger crossIdentifierExp, byte[] labelKey, string identifier)
        {
            var occurenceFactor = DeriveKeywordOccurrenceFactor(keys.KeywordCounterKey, keyword, counter);
            var occurenceFactorBytes = occurenceFactor.ToByteArray(isUnsigned: true, isBigEndian: true);
            var occurenceFactorInverse = CryptoUtils.ModInverse(occurenceFactorBytes, ExponentGroupOrder);

            var yValue = CryptoUtils.ModMultiply(crossIdentifierExp, occurenceFactorInverse, ExponentGroupOrder);
            var yBytes = yValue.ToByteArray(isUnsigned: true, isBigEndian: true);

            var encryptedDocId = CryptoUtils.Encrypt(labelKey, identifier);
            return (encryptedDocId, yBytes);
        }

        /// <summary>
        /// Builds a conjunctive membership tag g^{ (keywordTag * documentIndexExponent) } mod p stored in XSet.
        /// </summary>
        private BigInteger BuildConjunctiveMembershipTag(string keyword, BigInteger crossIdentifierExp)
        {
            var obscuredKeywordBytes = CryptoUtils.Randomize(keys.KeywordTagKey, keyword);
            var obscuredKeyword = new BigInteger(obscuredKeywordBytes, isBigEndian: true, isUnsigned: true) % ExponentGroupOrder;
            if (obscuredKeyword.IsZero) obscuredKeyword = 1;

            var exponent = CryptoUtils.ModMultiply(obscuredKeyword, crossIdentifierExp, ExponentGroupOrder);
            return BigInteger.ModPow(
                CryptoUtils.Generator(PrimeModulus),
                exponent,
                PrimeModulus
            );
        }

        public IEnumerable<string> Search(BooleanEncryptedStorageServer server, params string[] keywords)
        {
            QueryMessage query = GenerateQuery(keywords);
            var encryptedResults = server.ProcessQuery(query);
            return encryptedResults.Select(x => DecryptDocumentIdentifier(keywords, x));
        }

        private string DecryptDocumentIdentifier(string[] keywords, byte[] encryptedDocId)
        {
            var keyWrapper = new EncryptedValue(keywords[0], keys.DocumentEncryptionKeySeed);
            return CryptoUtils.Decrypt(keyWrapper.Value, encryptedDocId);
        }

        /// <summary>
        /// Derives a conjunctive test token xtoken = g^{ occurrenceFactor * keywordTag } mod p
        /// used to test membership with y-values and XSet elements.
        /// </summary>
        private byte[] DeriveConjunctiveTestToken(string pivotKeyword, string secondaryKeyword, int occurenceIndex)
        {
            var occurenceFactor = DeriveKeywordOccurrenceFactor(keys.KeywordCounterKey, pivotKeyword, occurenceIndex);

            var keywordTagBytes = CryptoUtils.Randomize(keys.KeywordTagKey, secondaryKeyword);
            var keywordTag = new BigInteger(keywordTagBytes, isBigEndian: true, isUnsigned: true) % ExponentGroupOrder;
            if (keywordTag.IsZero) keywordTag = 1;

            var exponent = CryptoUtils.ModMultiply(occurenceFactor, keywordTag, ExponentGroupOrder);

            var testToken = BigInteger.ModPow(
                CryptoUtils.Generator(PrimeModulus),
                exponent,
                PrimeModulus
            );

            return testToken.ToByteArray(isUnsigned: true, isBigEndian: true);
        }

        /// <summary>
        /// Generates the query message:
        /// Stag = PRF(K_T, pivotKeyword) and a sequence of conjunctive test token sets for each occurrence index.
        /// </summary>
        public QueryMessage GenerateQuery(string[] keywords)
        {
            var pivotKeyword = keywords[0];
            var searchTag = new EncryptedValue(pivotKeyword, keys.SearchTagKey).Value;
            var query = new QueryMessage { Stag = searchTag };

            int occurenceIndex = 0;
            int limit = 2048;

            while (occurenceIndex < limit)
            {
                var testTokenSet = new List<byte[]>();
                for (int i = 1; i < keywords.Length; i++)
                {
                    var testToken = DeriveConjunctiveTestToken(keywords[0], keywords[i], occurenceIndex);
                    testTokenSet.Add(testToken);
                }
                query.ConjunctiveTokenSets[occurenceIndex] = testTokenSet;

                if (keywords.Length == 1) break;
                occurenceIndex++;
            }

            return query;
        }
    }

    public record struct KeyCollection()
    {
        private const int KEY_SIZE = 32;

        /// <summary>
        /// K_S: Seed to derive per-keyword document encryption keys.
        /// </summary>
        public byte[] DocumentEncryptionKeySeed { get; set; } = new byte[KEY_SIZE];

        /// <summary>
        /// K_X: Key for deriving per-keyword tag factors used in XSet membership tags.
        /// </summary>
        public byte[] KeywordTagKey { get; set; } = new byte[KEY_SIZE];

        /// <summary>
        /// K_I: Key for randomizing document identifiers into exponent-domain indices.
        /// </summary>
        public byte[] DocumentIndexKey { get; set; } = new byte[KEY_SIZE];

        /// <summary>
        /// K_Z: Key for deriving per-(keyword, occurrenceIndex) counter factors (invertible mod p-1).
        /// </summary>
        public byte[] KeywordCounterKey { get; set; } = new byte[KEY_SIZE];

        /// <summary>
        /// K_T: Key for deriving search tags (stag) for the pivot keyword.
        /// </summary>
        public byte[] SearchTagKey { get; set; } = new byte[KEY_SIZE];
    }
}