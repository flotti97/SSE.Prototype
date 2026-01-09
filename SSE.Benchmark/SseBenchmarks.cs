using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Server;
using System.Security.Cryptography;

namespace SSE.Benchmark
{
    [MemoryDiagnoser]
    public class SseBenchmarks
    {
        private Database<(string Id, string Content)> _database;
        private List<(string Id, string Content)> _allDocuments;

        // Basic Scheme Artifacts
        private byte[] _basicMasterKey;
        private EncryptedStorageServer _basicServer;

        // Boolean Scheme Artifacts
        private BooleanQueryScheme _booleanScheme;
        private BooleanEncryptedStorageServer _booleanServer;

        // Substring Scheme Artifacts
        private SubstringQueryScheme _substringScheme;

        [Params(10, 30)] // Adjust based on dataset size for reasonable runtimes
        public int DocumentCount;

        // Search term that should exist in sci.crypt
        private const string SearchTerm = "encryption";

        [GlobalSetup]
        public void Setup()
        {
            // Path provided by user
            string documentsPath = @"C:\Users\florian\Documents\01_InformatikStudium\06_SS25\Bachelorarbeit\test_documents\20news-18828\sci.crypt";

            if (!Directory.Exists(documentsPath))
            {
                // Fallback for testing environment if path doesn't exist
                // Create dummy documents
                _allDocuments = new List<(string, string)>();
                for (int i = 0; i < 1000; i++)
                {
                    _allDocuments.Add((i.ToString(), $"This is document {i} with some content including encryption and security."));
                }
            }
            else
            {
                var files = Directory.GetFiles(documentsPath);
                _allDocuments = new List<(string, string)>();
                foreach (var file in files)
                {
                    string content = File.ReadAllText(file);
                    string id = Path.GetFileName(file);
                    _allDocuments.Add((id, content));
                }
            }

            // Create database subset
            var subset = _allDocuments.Take(DocumentCount).ToList();
            _database = new Database<(string, string)>(subset, x => x.Item1, x => x.Item2);

            // Pre-setup Basic
            var (basicKey, basicDb) = BasicScheme.Setup(_database);
            _basicMasterKey = basicKey;
            _basicServer = new EncryptedStorageServer(basicDb);

            // Pre-setup Boolean
            _booleanScheme = new BooleanQueryScheme();
            var (_, boolDb) = _booleanScheme.Setup(_database);
            _booleanServer = new BooleanEncryptedStorageServer(boolDb);

            // Pre-setup Substring
            _substringScheme = new SubstringQueryScheme();
            _substringScheme.Setup(_database);
        }

        // --- Setup (Indexing) Benchmarks ---

        [Benchmark]
        public void Basic_Setup()
        {
            BasicScheme.Setup(_database);
        }

        [Benchmark]
        public void Boolean_Setup()
        {
            var scheme = new BooleanQueryScheme();
            scheme.Setup(_database);
        }

        [Benchmark]
        public void Substring_Setup()
        {
            var scheme = new SubstringQueryScheme();
            scheme.Setup(_database);
        }

        // --- Search Benchmarks ---

        [Benchmark]
        public void Basic_Search()
        {
            var (labelKey, identifierKey) = BasicScheme.DeriveSearchKeys(_basicMasterKey, SearchTerm);
            var consumer = new Consumer();
            _basicServer.Find(labelKey, identifierKey).Consume(consumer);
        }

        [Benchmark]
        public void Boolean_Search()
        {
            // Boolean search with single term is equivalent to basic search conceptually
            var consumer = new Consumer();
            _booleanScheme.Search(_booleanServer, new[] { SearchTerm }).Consume(consumer);
        }

        [Benchmark]
        public void Substring_Search()
        {
            var consumer = new Consumer();
            _substringScheme.Search(SearchTerm).Consume(consumer);
        }
    }
}
