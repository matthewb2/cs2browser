using CS2Browser.IdlGenerator.Generation;
using CS2Browser.IdlGenerator.Model;
using CS2Browser.IdlGenerator.Parser;

var options = GeneratorOptions.Parse(args);

if (options.ShowHelp)
{
    Console.WriteLine(GeneratorOptions.HelpText);
    return 0;
}

var definitionsDir = Path.GetFullPath(options.DefinitionsPath);
if (!Directory.Exists(definitionsDir))
{
    Console.Error.WriteLine($"[IdlGenerator] IDL 정의 폴더를 찾을 수 없습니다: {definitionsDir}");
    return 1;
}

var idlFiles = Directory.GetFiles(definitionsDir, "*.idl", SearchOption.TopDirectoryOnly)
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToArray();

if (idlFiles.Length == 0)
{
    Console.Error.WriteLine($"[IdlGenerator] {definitionsDir} 에 .idl 파일이 없습니다.");
    return 1;
}

Console.WriteLine($"[IdlGenerator] IDL {idlFiles.Length}개 파싱 중...");

List<string> sources = new();
foreach (var file in idlFiles)
{
    sources.Add(File.ReadAllText(file));
    Console.WriteLine($"  - {Path.GetFileName(file)}");
}

IdlModule module;
try
{
    module = IdlParser.Parse(sources.ToArray());
}
catch (IdlParseException ex)
{
    Console.Error.WriteLine($"[IdlGenerator] IDL 파싱 실패: {ex.Message}");
    return 1;
}

Console.WriteLine($"[IdlGenerator] 인터페이스 {module.Interfaces.Count}, 열거형 {module.Enums.Count}, " +
                  $"딕셔너리 {module.Dictionaries.Count}, 타입정의 {module.Typedefs.Count}");

var emitter = new CSharpEmitter(module, options.Namespace);
var files = emitter.EmitAll();

var outputDir = Path.GetFullPath(options.OutputPath);
Directory.CreateDirectory(outputDir);

// 이전 생성물을 지우고 다시 생성한다(오래된 .g.cs 잔존 방지).
foreach (var stale in Directory.GetFiles(outputDir, "*.g.cs", SearchOption.TopDirectoryOnly))
{
    File.Delete(stale);
}

foreach (var (fileName, content) in files)
{
    var path = Path.Combine(outputDir, fileName);
    File.WriteAllText(path, content);
    Console.WriteLine($"  + {fileName}");
}

Console.WriteLine($"[IdlGenerator] {files.Count}개 파일을 {outputDir} 에 생성했습니다.");
return 0;

internal sealed class GeneratorOptions
{
    public string DefinitionsPath { get; private init; } = "Definitions";
    public string OutputPath { get; private init; } = "Generated";
    public string Namespace { get; private init; } = "CS2Browser.Scripting.Generated";
    public bool ShowHelp { get; private init; }

    public const string HelpText =
        """
        사용법: CS2Browser.IdlGenerator [옵션]

          --definitions <dir>   IDL 파일(.idl)이 있는 폴더 (기본: Definitions)
          --output <dir>        생성된 .g.cs 를 출력할 폴더 (기본: Generated)
          --namespace <ns>      생성 코드의 C# 네임스페이스 (기본: CS2Browser.Scripting.Generated)
          -h, --help            도움말 출력
        """;

    public static GeneratorOptions Parse(string[] args)
    {
        string definitions = "Definitions";
        string output = "Generated";
        string ns = "CS2Browser.Scripting.Generated";
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--definitions" when i + 1 < args.Length:
                    definitions = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--namespace" when i + 1 < args.Length:
                    ns = args[++i];
                    break;
                case "-h" or "--help":
                    help = true;
                    break;
            }
        }

        // 기본 상대 경로는 실행 파일 기준으로 해석한다.
        if (!Path.IsPathRooted(definitions))
        {
            definitions = Path.Combine(AppContext.BaseDirectory, definitions);
        }

        return new GeneratorOptions
        {
            DefinitionsPath = definitions,
            OutputPath = output,
            Namespace = ns,
            ShowHelp = help
        };
    }
}
