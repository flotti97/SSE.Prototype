using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Cryptography;
using System.Numerics;

namespace SSE.Tests
{
    [TestClass]
    public class TestOXT
    {
        private Database<(string, string)> CreateTestDatabase()
        {
            var data = new List<(string, string)>
            {
                ("doc1", "apple banana cherry"),
                ("doc2", "banana cherry date"),
                ("doc3", "apple cherry elderberry"),
                ("doc4", "banana apple fig"),
                ("doc5", "grape apple banana")
            };
            return new Database<(string, string)>(data, item => item.Item1, item => item.Item2);
        }

        [TestMethod]
        public void OXTSetup_CreatesValidStructures()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();

            // Act
            var (keys, edb) = oxt.Setup(db);

            // Assert
            Assert.IsNotNull(keys);
            Assert.IsNotNull(keys.MasterKey);
            Assert.IsNotNull(keys.CrossTagKey);
            Assert.IsNotNull(keys.IdentifierKey);
            Assert.IsNotNull(keys.TagKey);
            Assert.IsNotNull(keys.IndexKey);
            Assert.IsNotNull(edb);
        }

        [TestMethod]
        public void GetTag_RetrievesCorrectTokens_ForExistingKeyword()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            string testKeyword = "apple";

            // Act
            var tokens = edb.GetTag(keys.IndexKey, testKeyword);

            // Assert
            Assert.IsNotNull(tokens);
            Assert.IsTrue(tokens.Count > 0);
        }

        [TestMethod]
        public void GetTag_ReturnsEmptyList_ForNonexistentKeyword()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            string nonexistentKeyword = "nonexistent";

            // Act
            var tokens = edb.GetTag(keys.IndexKey, nonexistentKeyword);

            // Assert
            Assert.IsNotNull(tokens);
            Assert.AreEqual(0, tokens.Count);
        }

        [TestMethod]
        public void TokenDecryption_YieldsCorrectDocuments()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            string testKeyword = "apple";

            // Act
            var tokens = edb.GetTag(keys.IndexKey, testKeyword);
            var documentIds = new List<string>();

            byte[] labelKey = CryptoUtils.Randomize(keys.MasterKey, testKeyword);
            foreach (var (encrypted, _) in tokens)
            {
                string docId = CryptoUtils.Decrypt(labelKey, encrypted);
                documentIds.Add(docId);
            }

            // Assert
            Assert.IsTrue(documentIds.Contains("doc1"));
            Assert.IsTrue(documentIds.Contains("doc3"));
            Assert.IsTrue(documentIds.Contains("doc4"));
            Assert.IsTrue(documentIds.Contains("doc5"));
            Assert.IsFalse(documentIds.Contains("doc2"));
        }

        [TestMethod]
        public void ContainsCrossTag_ReturnsTrueForValidCrossTags()
        {
            // Arrange - We'll create a specific test case with a known keyword and identifier
            var testData = new List<(string, string)>
            {
                ("docX", "uniqueword")
            };
            var testDb = new Database<(string, string)>(testData, item => item.Item1, item => item.Item2);
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(testDb);

            // Get the keyword and recreate the cross tag
            string keyword = "uniqueword";
            string identifier = "docX";

            // Recreate the crossIdentifier
            var crossIdentifier = CryptoUtils.Randomize(keys.IdentifierKey, identifier, CryptoUtils.Prime256Bit);

            // Manually compute the cross tag using the same process as in BooleanQueryScheme
            var crossTagRandomizer = CryptoUtils.Randomize(keys.CrossTagKey, keyword);
            var randomizerValue = new BigInteger(crossTagRandomizer, isBigEndian: true, isUnsigned: true);

            var crossTag = BigInteger.ModPow(
                CryptoUtils.Generator(CryptoUtils.Prime256Bit),
                CryptoUtils.ModMultiply(randomizerValue, crossIdentifier, CryptoUtils.Prime256Bit),
                CryptoUtils.Prime256Bit
            );

            // Act
            bool result = edb.ContainsCrossTag(crossTag);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ContainsCrossTag_ReturnsFalseForInvalidCrossTags()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);

            // Create a random value that should not be in the cross tag set
            var invalidCrossTag = new BigInteger(CryptoUtils.GenerateRandomKey(), isUnsigned: true);

            // Act
            bool result = edb.ContainsCrossTag(invalidCrossTag);

            // Assert
            Assert.IsFalse(result);
        }
    }
}
