using BenchmarkDotNet.Running;

namespace Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        new BenchmarkSwitcher(typeof(Program).Assembly).Run(args);
    }
}
