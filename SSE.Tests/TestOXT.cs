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
        public void SingleKeywordNoResult()
        {
            //Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            var keyword = "orange";

            //Act
            var tags = edb.GetTag(keys.IndexKey, keyword);

            //Assert
            Assert.IsTrue(tags.Count == 0);
        }

        [TestMethod]
        public void SingleKeywordOneResult()
        {
            //Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            var keyword = "grape";

            //Act
            var tags = edb.GetTag(keys.IndexKey, keyword);
            byte[] labelKey = CryptoUtils.Randomize(keys.MasterKey, keyword);
            var documentIds = DecryptDocIds(labelKey, tags).ToList();

            //Assert
            Assert.IsTrue(tags.Count == 1);
            Assert.IsTrue(documentIds.Contains("doc5"));
        }

        [TestMethod]
        public void SingleKeywordMultipleResults()
        {
            //Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            var keyword = "apple";

            //Act
            var tags = edb.GetTag(keys.IndexKey, keyword);
            byte[] labelKey = CryptoUtils.Randomize(keys.MasterKey, keyword);
            var documentIds = DecryptDocIds(labelKey, tags).ToList();

            //Assert
            Assert.IsTrue(tags.Count == 4);
            Assert.IsTrue(documentIds.Contains("doc1"));
            Assert.IsTrue(documentIds.Contains("doc3"));
            Assert.IsTrue(documentIds.Contains("doc4"));
            Assert.IsTrue(documentIds.Contains("doc5"));
        }

        private static IEnumerable<string> DecryptDocIds(byte[] labelKey, IEnumerable<(byte[] encrypted, byte[] inverseCrossIdentifier)> tokens)
        {
            foreach (var (encrypted, _) in tokens)
            {
                string docId = CryptoUtils.Decrypt(labelKey, encrypted);
                yield return docId;
            }
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

        [TestMethod]
        public void TwoKeywordsMultipleResults()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            var keywords = new List<string> { "apple", "banana" }; // First is s-term

            // Also test the convenience method
            var documentIds = oxt.ExecuteQuery(edb, keywords);

            // Assert
            Assert.AreEqual(3, documentIds.Count);
            CollectionAssert.Contains(documentIds, "doc1");
            CollectionAssert.Contains(documentIds, "doc4");
            CollectionAssert.Contains(documentIds, "doc5");
        }

        [TestMethod]
        public void TwoKeywordsSingleResult()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            var keywords = new List<string> { "apple", "fig" };

            // Act
            var results = oxt.ExecuteQuery(edb, keywords);

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("doc4", results[0]);
        }

        [TestMethod]
        public void TwoKeyworsNoResult()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            var keywords = new List<string> { "apple", "date" };

            // Act
            var results = oxt.ExecuteQuery(edb, keywords);

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void ThreeKeywordsSingleResult()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);
            var keywords = new List<string> { "apple", "banana", "cherry" };

            // Act
            var documentIds = oxt.ExecuteQuery(edb, keywords);

            // Assert - only doc1 contains all three terms
            Assert.AreEqual(1, documentIds.Count);
            Assert.AreEqual("doc1", documentIds[0]);
        }

        [TestMethod]
        public void OptimizedSearchBehaviour()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (keys, edb) = oxt.Setup(db);

            // Keyword order matters! 'fig' is more selective than 'apple'
            var moreEfficientOrder = new List<string> { "fig", "apple" };
            var lessEfficientOrder = new List<string> { "apple", "fig" };

            // Act
            // This will generate fewer stag lookups since 'fig' has fewer documents
            var (stagEff, _) = oxt.GenerateSearchTokens(moreEfficientOrder);
            var tuplesEff = edb.Retrieve(stagEff);

            // This generates more lookups since 'apple' appears in more documents
            var (stagLess, _) = oxt.GenerateSearchTokens(lessEfficientOrder);
            var tuplesLess = edb.Retrieve(stagLess);

            // Assert
            // The result should be the same document, but the efficient query requires fewer operations
            Assert.IsTrue(tuplesEff.Count < tuplesLess.Count);

            var resultsEff = oxt.ExecuteQuery(edb, moreEfficientOrder);
            var resultsLess = oxt.ExecuteQuery(edb, lessEfficientOrder);

            // Both should return the same document
            CollectionAssert.AreEquivalent(resultsEff, resultsLess);
            Assert.AreEqual(1, resultsEff.Count);
            Assert.AreEqual("doc4", resultsEff[0]);
        }
    }
}
