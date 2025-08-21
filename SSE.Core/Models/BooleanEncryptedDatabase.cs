using SSE.Cryptography;
using System.Numerics;

namespace SSE.Core.Models
{
    /// <summary>
    /// Stores the OXT encrypted index:
    ///  - TSet: mapping from search tag (stag) -> posting list of (encryptedDocId, yValue)
    ///  - XSet: set of conjunctive membership tags (g^{ keywordTag * docIndex } mod p)
    /// </summary>
    public class BooleanEncryptedDatabase
    {
        private static List<(byte[], byte[])> EMPTY_LIST = new();
        private readonly HashSet<BigInteger> membershipTagSet;
        private Dictionary<string, List<(byte[] encryptedDocumentId, byte[] counterAdjustedIndexFactor)>> searchTagPostingListMap;

        public BooleanEncryptedDatabase(HashSet<BigInteger> membershipTagSet)
        {
            this.membershipTagSet = membershipTagSet;
            this.searchTagPostingListMap = new();
        }

        /// <summary>
        /// Initializes the TSet from keyword posting lists.
        /// </summary>
        /// <returns>
        /// K_T (SearchTagKey) used client-side to derive stags.
        /// </returns>
        public byte[] Setup(Dictionary<string, List<(byte[] encryptedDocumentId, byte[] counterAdjustedIndexFactor)>> keywordPostingLists)
        {
            // Generate index key KT
            byte[] searchTagDerivationKey = CryptoUtils.GenerateRandomKey();

            // For each keyword, derive a secure token and store its associated tuples
            foreach (var entry in keywordPostingLists)
            {
                string keyword = entry.Key;
                List<(byte[], byte[])> postingList = entry.Value;

                // Generate secure lookup token using the index key
                string secureToken = Convert.ToBase64String(
                    CryptoUtils.Randomize(searchTagDerivationKey, keyword));

                // Store the mapping from secure token to tuples
                searchTagPostingListMap.Add(secureToken, postingList);
            }

            return searchTagDerivationKey;
        }

        /// <returns>
        /// True if a computed test value matches an existing membership tag in XSet.
        /// </returns>
        public bool ContainsMembershipTag(BigInteger membershipTagCandidate)
        {
            return membershipTagSet.Contains(membershipTagCandidate);
        }

        /// <summary>
        /// Retrieve posting list for a given pivot keyword search tag (stag).
        /// </summary>
        /// <param name="searchTag">Base64 encoded stag</param>
        public List<(byte[] encryptedDocumentId, byte[] counterAdjustedIndexFactor)> RetrievePostingList(string searchTag)
        {
            // Lookup tuples using the secure token
            if (searchTagPostingListMap.TryGetValue(searchTag, out var postingList))
            {
                return postingList;
            }

            return EMPTY_LIST;
        }
    }
}
