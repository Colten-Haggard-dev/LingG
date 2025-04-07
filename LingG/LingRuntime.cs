using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LingG;

public static class LingRuntime
{
    private static readonly Interpreter _interpreter = new();

    public static void RunFile(string filename)
    {
        byte[] bytes = File.ReadAllBytes(filename);
        Run(Encoding.Default.GetString(bytes));
    }

    private static void Run(string source)
    {
        Scanner scanner = new(source);
        List<Token> tokens = scanner.ScanTokens();

        Parser parser = new(tokens);
        List<Statement> statements = parser.Parse();

        if (LingError.HadError)
            System.Environment.Exit(-1);

        Resolver resolver = new(_interpreter);
        resolver.Resolve(statements);

        if (LingError.HadError)
            System.Environment.Exit(-1);

        _interpreter.Interpret(statements);
    }
}
