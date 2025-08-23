using Microsoft.VisualStudio.TestTools.UnitTesting;
using SSE.Client.Schemes;
using SSE.Core.Models;

namespace SSE.Tests
{
    [TestClass]
    public class SubstringTests
    {
        private Database<(string id, string content)> CreateDatabase()
        {
            var data = new List<(string id, string content)>
            {
                ("doc1", "apple banana cherry"),
                ("doc2", "banana cherry date"),
                ("doc3", "apple cherry elderberry"),
                ("doc4", "banana apple fig"),
                ("doc5", "grape apple banana"),
                ("doc6", "appliance repair guide")
            };
            return new Database<(string id, string content)>(data, t => t.id, t => t.content);
        }

        [TestMethod]
        public void Substring_Setup_BuildsIndex()
        {
            var db = CreateDatabase();
            var scheme = new SubstringQueryScheme();
            scheme.Setup(db);
            var results = scheme.Search("apple").ToList();
            // Expect docs containing the exact substring "apple" (doc6 only has "appl" prefix, so not included)
            CollectionAssert.AreEquivalent(new[] { "doc1", "doc3", "doc4", "doc5" }, results);
            Assert.IsFalse(results.Contains("doc6"));
        }

        [TestMethod]
        public void Substring_Search_InternalSubstring()
        {
            var db = CreateDatabase();
            var scheme = new SubstringQueryScheme();
            scheme.Setup(db);
            var results = scheme.Search("nan").ToList(); // substring inside banana
            CollectionAssert.AreEquivalent(new[] { "doc1", "doc2", "doc4", "doc5" }, results);
        }

        [TestMethod]
        public void Substring_Search_ShortPatternReturnsEmpty()
        {
            var db = CreateDatabase();
            var scheme = new SubstringQueryScheme();
            scheme.Setup(db);
            var results = scheme.Search("ap").ToList(); // length < qMin (3)
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void Substring_Search_NoMatch()
        {
            var db = CreateDatabase();
            var scheme = new SubstringQueryScheme();
            scheme.Setup(db);
            var results = scheme.Search("xyz").ToList();
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void Substring_Search_PatternWithElder()
        {
            var db = CreateDatabase();
            var scheme = new SubstringQueryScheme();
            scheme.Setup(db);
            var results = scheme.Search("elder").ToList();
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("doc3", results[0]);
        }

        [TestMethod]
        public void Substring_Search_PartialPrefixAppl()
        {
            var db = CreateDatabase();
            var scheme = new SubstringQueryScheme();
            scheme.Setup(db);
            var results = scheme.Search("appl").ToList(); // prefix present in apple / appliance
            // Expect docs 1,3,4,5 (apple) and 6 (appliance)
            CollectionAssert.AreEquivalent(new[] { "doc1", "doc3", "doc4", "doc5", "doc6" }, results);
        }
    }
}
