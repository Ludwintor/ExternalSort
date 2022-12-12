using ExternalSort;

PolyphaseSort sort = new();

sort.Sort("source.txt", int.Parse, "output.txt", false, 6);

