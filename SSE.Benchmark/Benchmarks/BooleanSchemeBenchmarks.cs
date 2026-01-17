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
    public class BooleanSchemeBenchmarks
    {
        private Database<(string Id, string Content)> _database;
        
        private BooleanQueryScheme _scheme;
        private BooleanEncryptedStorageServer _server;

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

            _scheme = new BooleanQueryScheme();
            var (_, db) = _scheme.Setup(_database);
            _server = new BooleanEncryptedStorageServer(db);
        }

        [Benchmark]
        public void SchemeSetup()
        {
            var scheme = new BooleanQueryScheme();
            scheme.Setup(_database);
        }

        [Benchmark]
        [Arguments("john crypto city")]
        [Arguments("whatever year")]
        [Arguments("maybe next time")]
        [Arguments("what year")]
        [Arguments("crypto")]
        [Arguments("whatever")]
        [Arguments("year")]
        public void Search(string searchTerms)
        {
            var consumer = new Consumer();
            _scheme.Search(_server, searchTerms.Split(" ")).Consume(consumer);
        }
    }
}
