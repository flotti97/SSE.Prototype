using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Server;
using System.Reflection;

namespace SSE.Benchmark.Benchmarks
{
    public class SizeBenchmarks
    {
        public void Run()
        {
            Console.WriteLine();
            Console.WriteLine("------------------------------------------");
            Console.WriteLine("Index Size Comparison (Fixed q-size = 4)");
            Console.WriteLine("------------------------------------------");

            // Setup Data
            int DocumentCount = 100;
            string documentsPath = "";
            try { documentsPath = BenchmarkHelper.GetTestDocumentsPath(); } catch {} 
            var allDocuments = new List<(string, string)>();

            if (!Directory.Exists(documentsPath))
            {
                 for (int i = 0; i < 1000; i++)
                {
                    allDocuments.Add((i.ToString(), $"This is document {i} with some content including encryption and security."));
                }
            }
            else
            {
                var files = Directory.GetFiles(documentsPath);
                foreach (var file in files)
                {
                    allDocuments.Add((Path.GetFileName(file), File.ReadAllText(file)));
                }
            }
            var subset = allDocuments.Take(DocumentCount).ToList();
            var db = new Database<(string, string)>(subset, x => x.Item1, x => x.Item2);

            Console.WriteLine($"Database size: {DocumentCount} documents.");

            // Basic
            var (basicKey, basicDb) = BasicScheme.Setup(db);
            long basicSize = SizeEstimator.EstimateSize(basicDb);
            Console.WriteLine($"Basic Scheme Index Size: {FormatBytes(basicSize)}");

            // Boolean
            var booleanScheme = new BooleanQueryScheme();
            var (_, boolDb) = booleanScheme.Setup(db);
            long boolSize = SizeEstimator.EstimateSize(boolDb);
            Console.WriteLine($"Boolean Scheme Index Size: {FormatBytes(boolSize)}");

            // Substring (Fixed q=4)
            int q = 4;
            var substringScheme = new SubstringQueryScheme(q, q);
            substringScheme.Setup(db);
            
            // Get private server field using reflection
            var serverField = typeof(SubstringQueryScheme).GetField("server", BindingFlags.Instance | BindingFlags.NonPublic);
            var subServer = serverField.GetValue(substringScheme);
            
            long subSize = SizeEstimator.EstimateSize(subServer);
            Console.WriteLine($"Substring Scheme Index Size (q={q}): {FormatBytes(subSize)}");
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.00} {sizes[order]}";
        }
    }
}
