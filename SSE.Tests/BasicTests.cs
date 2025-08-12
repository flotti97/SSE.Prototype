using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Server;

namespace SSE.Tests
{
    [TestClass]
    public class BasicTests
    {
        [TestMethod]
        public void BasicSchemeSetupTest()
        {
            // Arrange
            var data = new List<(string, string)>
            {
                ("doc1", "apple banana"),
                ("doc2", "banana cherry"),
                ("doc3", "apple cherry"),
                ("doc4", "banana apple")
            };
            var db = new Database<(string, string)>(data, item => item.Item1, item => item.Item2);
            // Act
            var (masterKey, encryptedDb) = BasicScheme.Setup(db);
            // Assert
            Assert.IsNotNull(masterKey);
            Assert.IsNotNull(encryptedDb);
        }

        [TestMethod]
        public void BasicSchemeSearchTest()
        {
            // Arrange
            var data = new List<(string, string)>
            {
                ("doc1", "apple banana"),
                ("doc2", "banana cherry"),
                ("doc3", "apple cherry"),
                ("doc4", "banana apple")
            };
            var db = new Database<(string, string)>(data, item => item.Item1, item => item.Item2);
            var (masterKey, encryptedDb) = BasicScheme.Setup(db);

            var searchTerm = "banana";
            var (labelKey, identifierKey) = BasicScheme.DeriveSearchKeys(masterKey, searchTerm);

            // Act
            var server = new EncryptedStorageServer(encryptedDb);
            var results = server.Find(labelKey, identifierKey).ToList();

            // Assert
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.Contains("doc1"));
            Assert.IsTrue(results.Contains("doc2"));
            Assert.IsTrue(results.Contains("doc4"));
        }
    }
}
