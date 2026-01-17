#nullable disable

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Server;

namespace SSE.Benchmark.Benchmarks
{
    [ShortRunJob]
    [WarmupCount(1)]
    [IterationCount(1)]
    public class BasicSchemeBenchmarks
    {
        private Database<(string Id, string Content)> _database;
        private byte[] _masterKey;
        private EncryptedStorageServer _server;

        [Params(10, 100, 1000, 10000)] 
        public int DocumentCount;

        [GlobalSetup]
        public void Init()
        {
            string basePath = "/home/florian/Documents/01_Studium/Bachelorarbeit/SSE.Prototype/test_documents/20news-18828";
            var topics = Directory.EnumerateDirectories(basePath);

            var files = topics
                .SelectMany(topic => Directory.EnumerateFiles(Path.Combine(basePath, topic)))
                .Take(DocumentCount)
                .Select(file => (Path.GetFileName(file), File.ReadAllText(file)))
                .ToList();

            _database = new Database<(string, string)>(files, x => x.Item1, x => x.Item2);

            var (key, db) = BasicScheme.Setup(_database);
            _masterKey = key;
            _server = new EncryptedStorageServer(db);
        }

        [Benchmark]
        public void SchemeSetup()
        {
            BasicScheme.Setup(_database);
        }

        [Benchmark]
        [Arguments("encryption")]
        [Arguments("whatever")]
        [Arguments("year")]
        public void Search(string searchTerm)
        {
            var (labelKey, identifierKey) = BasicScheme.DeriveSearchKeys(_masterKey, searchTerm);
            var consumer = new Consumer();
            _server.Find(labelKey, identifierKey).Consume(consumer);
        }
    }
}
