using BenchmarkDotNet.Running;
using SSE.Benchmark.Benchmarks;

namespace SSE.Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<BasicSchemeBenchmarks>();
            BenchmarkRunner.Run<BooleanSchemeBenchmarks>();
            BenchmarkRunner.Run<SubstringSchemeBenchmarks>();

        }
    }
}
