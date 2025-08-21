using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Cryptography;
using SSE.Server;

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
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (_, edb) = oxt.Setup(db);
            var server = new BooleanEncryptedStorageServer(edb);

            // Act
            var documentIds = oxt.Search(server, "orange").ToList();

            // Assert
            Assert.AreEqual(0, documentIds.Count);
        }

        [TestMethod]
        public void SingleKeywordOneResult()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (_, edb) = oxt.Setup(db);
            var server = new BooleanEncryptedStorageServer(edb);

            // Act
            var documentIds = oxt.Search(server, "grape").ToList();

            // Assert
            Assert.AreEqual(1, documentIds.Count);
            Assert.AreEqual("doc5", documentIds[0]);
        }

        [TestMethod]
        public void SingleKeywordMultipleResults()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (_, edb) = oxt.Setup(db);
            var server = new BooleanEncryptedStorageServer(edb);

            // Act
            var documentIds = oxt.Search(server, "apple").ToList();

            // Assert
            Assert.AreEqual(4, documentIds.Count);
            Assert.IsTrue(documentIds.Contains("doc1"));
            Assert.IsTrue(documentIds.Contains("doc3"));
            Assert.IsTrue(documentIds.Contains("doc4"));
            Assert.IsTrue(documentIds.Contains("doc5"));
        }

        [TestMethod]
        public void SimpleTest()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (_, edb) = oxt.Setup(db);
            var server = new BooleanEncryptedStorageServer(edb);
            var keywords = new List<string> { "apple" };

            // Act
            var documentIds = oxt.Search(server, "apple").ToList();

            // Assert
            Assert.AreEqual(4, documentIds.Count);
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
        public void TwoKeywordsMultipleResults()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (_, edb) = oxt.Setup(db);
            var server = new BooleanEncryptedStorageServer(edb);

            // Act
            var documentIds = oxt.Search(server, "apple", "banana").ToList();

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
            var (_, edb) = oxt.Setup(db);
            var server = new BooleanEncryptedStorageServer(edb);

            // Act
            var results = oxt.Search(server, "apple", "fig").ToList();

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
            var (_, edb) = oxt.Setup(db);
            var server = new BooleanEncryptedStorageServer(edb);

            // Act
            var results = oxt.Search(server, "apple", "date").ToList();

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void ThreeKeywordsSingleResult()
        {
            // Arrange
            var db = CreateTestDatabase();
            var oxt = new BooleanQueryScheme();
            var (_, edb) = oxt.Setup(db);
            var server = new BooleanEncryptedStorageServer(edb);

            // Act
            var documentIds = oxt.Search(server, "apple", "banana", "cherry").ToList();

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
            var (_, edb) = oxt.Setup(db);
            var server = new BooleanEncryptedStorageServer(edb);

            // Keyword order matters! 'fig' is more selective than 'apple'
            var moreEfficientOrder = new[] { "fig", "apple" };
            var lessEfficientOrder = new[] { "apple", "fig" };

            // Act
            var resultsEff = oxt.Search(server, moreEfficientOrder).ToList();
            var resultsLess = oxt.Search(server, lessEfficientOrder).ToList();

            // Assert
            CollectionAssert.AreEquivalent(resultsEff, resultsLess);
            Assert.AreEqual(1, resultsEff.Count);
            Assert.AreEqual("doc4", resultsEff[0]);
        }
    }
}
