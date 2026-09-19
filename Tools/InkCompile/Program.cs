using Ink;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: InkCompile <story.ink>");
    return 1;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine("Missing: " + path);
    return 1;
}

var dir = Path.GetDirectoryName(path);
Directory.SetCurrentDirectory(dir);
var source = File.ReadAllText(path);
var errors = 0;
var compiler = new Compiler(source, new Compiler.Options
{
    sourceFilename = Path.GetFileName(path),
    errorHandler = (msg, type) =>
    {
        Console.Error.WriteLine($"{type}: {msg}");
        if (type == Error.Type.Error) errors++;
    }
});

var story = compiler.Compile();
if (story == null || errors > 0)
{
    Console.Error.WriteLine("Compile failed.");
    return 1;
}

var jsonPath = Path.ChangeExtension(path, ".json");
File.WriteAllText(jsonPath, story.ToJson());
Console.WriteLine("Wrote " + jsonPath);
return 0;
