#nullable disable

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Server;

namespace SSE.Benchmark.Benchmarks
{
    [ShortRunJob]
    public class BooleanSchemeBenchmarks
    {
        private Database<(string Id, string Content)> _database;
        
        private BooleanQueryScheme _scheme;
        private BooleanEncryptedStorageServer _server;

        [Params(10)] 
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
