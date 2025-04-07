using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LingG;

public static class LingError
{
    public static bool HadError { get; private set; } = false;
    public static bool HadRuntimeError { get; private set; } = false;

    public static void Error(int line, string msg)
    {
        Report(line, "", msg);
    }

    public static void Error(Token token, string msg)
    {
        if (token.Type == TokenType.EOF)
        {
            Report(token.Line, " at end ", msg);
        }
        else
        {
            Report(token.Line, " at '" + token.Lexeme + "'", msg);
        }
    }

    public static void RuntimeError(RuntimeError error)
    {
        Console.Error.WriteLine(error.Message + "\n[line " + error.SourceToken.Line + "]");
        HadRuntimeError = true;
    }

    public static void Report(int line, string where, string msg)
    {
        Console.Error.WriteLine("[line " + line + "] Error" + where + ": " + msg);
        HadError = true;
    }
}

public class ParseError : Exception
{

}

public class RuntimeError(Token token, string message) : Exception(message)
{
    public readonly Token SourceToken = token;
}

public class Break(Token token) : RuntimeError(token, "Must use break inside of a loop.")
{

}

public class Continue(Token token) : RuntimeError(token, "Must use continue inside of a loop.")
{

}

public class Return(object value) : Exception
{
    public readonly object Value = value;
}
