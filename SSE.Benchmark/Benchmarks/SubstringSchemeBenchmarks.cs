#nullable disable

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Server;

namespace SSE.Benchmark.Benchmarks
{
    [ShortRunJob]
    public class SubstringSchemeBenchmarks
    {
        private Database<(string Id, string Content)> _database;
        private SubstringQueryScheme _scheme;

        [Params(10)] 
        public int DocumentCount;

        [Params(3,4,5)]
        public int QGramSize = 4;

        [GlobalSetup]
        public void Setup()
        {
            string documentsPath = BenchmarkHelper.GetTestDocumentsPath();
            
            var files = Directory.GetFiles(documentsPath)
                .Take(DocumentCount)
                .Select(file => (Path.GetFileName(file), File.ReadAllText(file)))
                .ToList();
            _database = new Database<(string, string)>(files, x => x.Item1, x => x.Item2);

            _scheme = new SubstringQueryScheme(QGramSize, QGramSize);
            
            _scheme.Setup(_database);
        }

        [Benchmark]
        public void Substring_Setup()
        {
            var scheme = new SubstringQueryScheme(QGramSize, QGramSize);
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
        public void Substring_Search(string searchTerm)
        {
            var consumer = new Consumer();
            _scheme.Search(searchTerm).Consume(consumer);
        }
    }
}
