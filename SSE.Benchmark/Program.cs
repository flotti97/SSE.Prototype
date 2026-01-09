using BenchmarkDotNet.Running;
using SSE.Benchmark;
using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Server;
using System.Reflection;

namespace SSE.Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Run Performance Benchmarks
            var summary = BenchmarkRunner.Run<SseBenchmarks>();

            // Run Size Analysis
            Console.WriteLine();
            Console.WriteLine("------------------------------------------");
            Console.WriteLine("Data Storage Size Analysis (Estimated)");
            Console.WriteLine("------------------------------------------");

            var benchmarks = new SseBenchmarks();
            benchmarks.DocumentCount = 100; // Use a fixed size for size comparison
            Console.WriteLine($"Loading {benchmarks.DocumentCount} documents...");
            benchmarks.Setup();

            // 1. Basic Scheme Size
            // We need to re-run setup or access private fields of benchmarks if we want to reuse.
            // But SseBenchmarks fields are private.
            // Let's just create new instances here for clarity and access.

            var db = GetDatabase(benchmarks);
            
            // Basic
            var (basicKey, basicDb) = BasicScheme.Setup(db);
            long basicSize = SizeEstimator.EstimateSize(basicDb);
            Console.WriteLine($"Basic Scheme Storage Size: {FormatBytes(basicSize)}");

            // Boolean
            var booleanScheme = new BooleanQueryScheme();
            var (_, boolDb) = booleanScheme.Setup(db);
            long boolSize = SizeEstimator.EstimateSize(boolDb);
            Console.WriteLine($"Boolean OXT Scheme Storage Size: {FormatBytes(boolSize)}");

            // Substring
            var substringScheme = new SubstringQueryScheme();
            substringScheme.Setup(db);
            // Get the server field
            var serverField = typeof(SubstringQueryScheme).GetField("server", BindingFlags.Instance | BindingFlags.NonPublic);
            var subServer = serverField.GetValue(substringScheme);
            // The server wraps BooleanEncryptedDatabase. We want the size of that DB.
            // Actually EncryptedStorageServer and BooleanEncryptedStorageServer just wrap the DB. 
            // So measuring the server object should be close enough (just a reference overhead), 
            // but let's try to get the inner DB if possible.
            // BooleanEncryptedStorageServer has 'private readonly BooleanEncryptedDatabase db;'?
            // Let's check BooleanEncryptedStorageServer.cs
            
            long subSize = SizeEstimator.EstimateSize(subServer);
            Console.WriteLine($"Substring Scheme Storage Size: {FormatBytes(subSize)}");

            Console.WriteLine();
            Console.WriteLine("------------------------------------------");
            Console.WriteLine("Security Comparison");
            Console.WriteLine("------------------------------------------");
            Console.WriteLine("1. Basic Scheme (Cash et al. 2013 Basic):");
            Console.WriteLine("   - Leaks: Search Pattern (repetition of queries), Access Pattern (results of queries).");
            Console.WriteLine("   - Vulnerabilities: Susceptible to File-Injection Attacks if repeated queries are observed.");
            Console.WriteLine();
            Console.WriteLine("2. Boolean Query Scheme (OXT - Oblivious Cross-Tags):");
            Console.WriteLine("   - Leaks: Search Pattern, Access Pattern, Conditional Leakage on cross-terms.");
            Console.WriteLine("   - Security: Higher security for conjunctive queries than naive approaches. Scales well with TSet/XSet.");
            Console.WriteLine();
            Console.WriteLine("3. Substring Query Scheme (Faber et al.):");
            Console.WriteLine("   - Implementation: Uses OXT with q-grams.");
            Console.WriteLine("   - Leaks: Positional information of q-grams, Access Pattern, Search Pattern.");
            Console.WriteLine("   - Storage: Significantly larger due to q-gram expansion.");
            Console.WriteLine("------------------------------------------");

        }

        private static Database<(string, string)> GetDatabase(SseBenchmarks benchmarks)
        {
             // Reflection to get _database from benchmarks instance
             var field = typeof(SseBenchmarks).GetField("_database", BindingFlags.Instance | BindingFlags.NonPublic);
             return (Database<(string, string)>)field.GetValue(benchmarks);
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
