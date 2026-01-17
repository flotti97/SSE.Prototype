#nullable disable

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Server;

namespace SSE.Benchmark.Benchmarks
{
    [ShortRunJob]
    public class BasicSchemeBenchmarks
    {
        private Database<(string Id, string Content)> _database;
        private byte[] _masterKey;
        private EncryptedStorageServer _server;

        [Params(10, 50, 100)] 
        public int DocumentCount;

        [GlobalSetup]
        public void Init()
        {
            string documentsPath = BenchmarkHelper.GetTestDocumentsPath();
            var files = Directory.GetFiles(documentsPath)
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
