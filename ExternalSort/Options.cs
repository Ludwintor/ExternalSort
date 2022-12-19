using CommandLine;

public class Options
{
    [Value(0, Required = true, MetaName = "Source path")]
    public string SourcePath { get; set; } = "";

    [Option('o', "output", Default = "", HelpText = "Output file path")]
    public string OutputPath { get; set; } = "";

    [Option('r', "reverse", Default = false, HelpText = "Flag that indicates to sort in descending order")]
    public bool Reverse { get; set; } = false;

    [Option('t', "tapes", Default = 3, HelpText = "How many temp files to use (Min: 3)")]
    public int TapesCount { get; set; } = 3;
}

