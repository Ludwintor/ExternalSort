using System.Diagnostics;
using CommandLine;
using ExternalSort;

Parser.Default.ParseArguments<Options>(args)
    .WithParsed(Sort);

static void Sort(Options opts)
{
    if (!File.Exists(opts.SourcePath))
    {
        Console.WriteLine("There's no file with given path");
        return;
    }
    if (opts.TapesCount < 3)
    {
        Console.WriteLine("Minimum tapes count is 3");
        return;
    }
    Stopwatch sw = Stopwatch.StartNew();
    PolyphaseSort.Sort(opts.SourcePath, int.Parse, opts.OutputPath, opts.Reverse, opts.TapesCount);
    sw.Stop();
    Console.WriteLine($"Execution time {sw.ElapsedTicks / 10000f:F3}ms");
}

