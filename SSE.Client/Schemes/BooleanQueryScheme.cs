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