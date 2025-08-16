using SSE.Core.Models;
using SSE.Cryptography;
using System.Numerics;

namespace SSE.Client.Schemes
{
    /// <summary>
    /// This is an implementation of the Oblivious Cross-Tags (OXT) protocol for Searchable Symmetric Encryption as proposed by Cash et al. 2013.
    /// </summary>
    public class BooleanQueryScheme
    {
        private KeyCollection keys = new();


        public BooleanQueryScheme()
        {
            // Select key KS for PRF F , keys KX, KI, KZ for PRF Fp (with range in Zp∗)
            keys.MasterKey = CryptoUtils.GenerateRandomKey();
            keys.CrossTagKey = CryptoUtils.GenerateRandomKey();
            keys.IdentifierKey = CryptoUtils.GenerateRandomKey();
            keys.TagKey = CryptoUtils.GenerateRandomKey();
        }

        public (KeyCollection, BooleanEncryptedDatabase) Setup(Database<(string, string)> db)
        {
            // parse DB as (ind_i, Wi)^d_i=1
            Dictionary<string, List<string>> metadata = db.GetMetadata();
            List<string> keywords = metadata.Keys.ToList();

            //Initialize T to an empty array indexed by keywords from W
            Dictionary<string, List<(byte[], byte[])>> tokenMap = new();

            // Initialize XSet to an empty set
            HashSet<BigInteger> crossTags = new();

            // For each w ∈ W, build the tuple list T[w] and XSet elements as follows
            foreach (string keyword in keywords)
            {
                // Initialize t to be an empty list
                List<(byte[], byte[])> tokens = new();

                // and set Ke ← F (KS, w)
                byte[] labelKey = CryptoUtils.Randomize(keys.MasterKey, keyword);

                // For all ind in DB(w) in random order
                List<string> identifiers = metadata[keyword].OrderBy(_ => Guid.NewGuid()).ToList();
                //initialize a counter c ← 0
                int counter = 0;
                foreach (var identifier in identifiers)
                {
                    // Set xind ← Fp(KI, ind)
                    var crossIdentifier = CryptoUtils.Randomize(keys.IdentifierKey, identifier, CryptoUtils.Prime256Bit);

                    // z ← Fp(KZ, w||c) and y ← xind · z^−1
                    // Compute e ← Enc(Ke, ind),
                    var tokenTuple = GenerateTokenTuple(keyword, counter, crossIdentifier, labelKey, identifier);
                    // and append (e, y) to t
                    tokens.Add(tokenTuple);

                    // Set xtag ← g^{Fp(KX ,w)·xind}
                    var crossTag = GenerateCrossTag(keyword, crossIdentifier);
                    // and add xtag to XSet
                    crossTags.Add(crossTag);

                    counter++;
                }

                // Set T[w] ← t
                tokenMap.Add(keyword, tokens);
            }

            // (TSet, KT) ← TSetSetup(T)
            BooleanEncryptedDatabase edb = new(crossTags);
            keys.IndexKey = edb.Setup(tokenMap);

            // Output the key (KS, KX, KI, KZ, KT ) and EDB = (TSet, XSet)
            return (keys, edb);
        }

        private (byte[] encryptedIdentifier, byte[] inverseCrossIdentifier) GenerateTokenTuple(string keyword, int counter, BigInteger crossIdentifier, byte[] labelKey, string identifier)
        {
            // z ← Fp(KZ, w  c)
            var tag = CryptoUtils.DeriveKey(keys.TagKey, keyword, counter);

            var tagInverse = CryptoUtils.ModInverse(tag, CryptoUtils.Prime256Bit);

            // y ← xind · z^−1
            var y = CryptoUtils.ModMultiply(crossIdentifier, tagInverse, CryptoUtils.Prime256Bit).ToByteArray();
            // Compute e ← Enc(Ke, ind)
            var e = CryptoUtils.Encrypt(labelKey, identifier);

            return (e, y);
        }

        private BigInteger GenerateCrossTag(string keyword, BigInteger crossIdentifier)
        {
            var crossTagRandomizer = CryptoUtils.Randomize(keys.CrossTagKey, keyword);
            var randomizerValue = new BigInteger(crossTagRandomizer, isBigEndian: true, isUnsigned: true);

            // Compute crossTag as g^{crossTagRandomizer * crossIdentifier} mod Prime256Bit
            var crossTag = BigInteger.ModPow(
                CryptoUtils.Generator(CryptoUtils.Prime256Bit),
                CryptoUtils.ModMultiply(randomizerValue, crossIdentifier, CryptoUtils.Prime256Bit),
                CryptoUtils.Prime256Bit
            );

            return crossTag;
        }

        /// <summary>
        /// Generates the search tokens for a boolean query.
        /// </summary>
        /// <param name="keywords">List of keywords where the first keyword is the s-term and the remaining are x-terms.</param>
        /// <returns>Search tag for the s-term and a function to generate cross tokens for each iteration.</returns>
        public (string stag, Func<int, List<BigInteger>> xtokenGenerator) GenerateSearchTokens(List<string> keywords)
        {
            if (keywords == null || keywords.Count == 0)
                throw new ArgumentException("Query must contain at least one keyword", nameof(keywords));

            // First keyword is the s-term (most selective term)
            string sterm = keywords[0];

            // stag ← TSetGetTag(KT, w1)
            string stag = Convert.ToBase64String(CryptoUtils.Randomize(keys.IndexKey, sterm));

            // Create a function that will generate xtokens for each counter value
            Func<int, List<BigInteger>> xtokenGenerator = counter =>
            {
                // For each iteration c, generate xtoken[c] = (xtoken[c,2], ..., xtoken[c,n])
                List<BigInteger> xtokens = new List<BigInteger>();

                // Skip the first keyword (s-term) and process each x-term
                for (int i = 1; i < keywords.Count; i++)
                {
                    string xterm = keywords[i];

                    // z ← Fp(KZ, w1||c) - Get the tag for sterm with counter c
                    var z = CryptoUtils.DeriveKey(keys.TagKey, sterm, counter);

                    // Fp(KX, wi) - Get the cross tag randomizer for the x-term
                    var xRandomizer = CryptoUtils.Randomize(keys.CrossTagKey, xterm);
                    var xRandomizerValue = new BigInteger(xRandomizer, isBigEndian: true, isUnsigned: true);

                    // xtoken[c,i] = g^(z * Fp(KX,wi))
                    var xtoken = BigInteger.ModPow(
                        CryptoUtils.Generator(CryptoUtils.Prime256Bit),
                        CryptoUtils.ModMultiply(
                            new BigInteger(z, isBigEndian: true, isUnsigned: true),
                            xRandomizerValue,
                            CryptoUtils.Prime256Bit
                        ),
                        CryptoUtils.Prime256Bit
                    );

                    xtokens.Add(xtoken);
                }

                return xtokens;
            };

            return (stag, xtokenGenerator);
        }

        /// <summary>
        /// Decrypts the search results using the client's master key
        /// </summary>
        public List<string> DecryptSearchResults(string sterm, List<byte[]> encryptedResults)
        {
            byte[] labelKey = CryptoUtils.Randomize(keys.MasterKey, sterm);

            var results = new List<string>();
            foreach (var e in encryptedResults)
            {
                string docId = CryptoUtils.Decrypt(labelKey, e);
                results.Add(docId);
            }

            return results;
        }

        /// <summary>
        /// Executes a boolean query and returns the matching document identifiers.
        /// </summary>
        /// <param name="edb">The encrypted database to search.</param>
        /// <param name="keywords">List of keywords where the first is the s-term and others are x-terms.</param>
        /// <returns>A list of document identifiers that match all keywords in the query.</returns>
        public List<string> ExecuteQuery(BooleanEncryptedDatabase edb, List<string> keywords)
        {
            if (keywords == null || keywords.Count == 0)
                throw new ArgumentException("Query must contain at least one keyword", nameof(keywords));

            string sterm = keywords[0];

            // Get the search tokens for s-term
            var stokens = edb.GetTag(keys.IndexKey, sterm);
            byte[] labelKey = CryptoUtils.Randomize(keys.MasterKey, sterm);

            // If there are no matching documents for the s-term, return empty result
            if (stokens.Count == 0)
                return new List<string>();

            // If it's a single-term query, just return all matching documents
            if (keywords.Count == 1)
            {
                return stokens.Select(token => CryptoUtils.Decrypt(labelKey, token.Item1)).ToList();
            }

            // For multi-term queries, check each document against all x-terms
            var results = new List<string>();

            // Process each document from s-term results
            int counter = 0;
            foreach (var (encrypted, y) in stokens)
            {
                // Decrypt the document ID
                string docId = CryptoUtils.Decrypt(labelKey, encrypted);
                bool matchesAllXTerms = true;

                // Check each x-term
                for (int i = 1; i < keywords.Count; i++)
                {
                    string xterm = keywords[i];

                    // Create cross tag for xterm and this document
                    var crossIdentifier = CryptoUtils.Randomize(keys.IdentifierKey, docId, CryptoUtils.Prime256Bit);
                    var crossTagRandomizer = CryptoUtils.Randomize(keys.CrossTagKey, xterm);
                    var randomizerValue = new BigInteger(crossTagRandomizer, isBigEndian: true, isUnsigned: true);

                    var crossTag = BigInteger.ModPow(
                        CryptoUtils.Generator(CryptoUtils.Prime256Bit),
                        CryptoUtils.ModMultiply(randomizerValue, crossIdentifier, CryptoUtils.Prime256Bit),
                        CryptoUtils.Prime256Bit
                    );

                    // If this document doesn't contain the x-term, exclude it
                    if (!edb.ContainsCrossTag(crossTag))
                    {
                        matchesAllXTerms = false;
                        break;
                    }
                }

                // If document matches all terms, add it to results
                if (matchesAllXTerms)
                {
                    results.Add(docId);
                }

                counter++;
            }

            return results;
        }
    }

    public record struct KeyCollection()
    {
        private const int KEY_SIZE = 32;

        public byte[] MasterKey { get; set; } = new byte[KEY_SIZE];
        public byte[] CrossTagKey { get; set; } = new byte[KEY_SIZE];
        public byte[] IdentifierKey { get; set; } = new byte[KEY_SIZE];
        public byte[] TagKey { get; set; } = new byte[KEY_SIZE];
        public byte[] IndexKey { get; set; } = new byte[KEY_SIZE];
    }
}