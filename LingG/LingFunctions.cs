using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LingG;

public interface ILingCallable
{
    int Arity();
    object Call(Interpreter interpreter, List<object> arguments);
}

public class LingFunction(Statement.Function declaration, Environment closure, bool isInitializer) : ILingCallable
{
    private readonly Statement.Function _declaration = declaration;
    private readonly Environment _closure = closure;
    private readonly bool _isInitializer = isInitializer;

    public LingFunction Bind(LingInstance instance)
    {
        Environment environment = new(_closure);
        environment.Define("this", instance);

        return new LingFunction(_declaration, environment, _isInitializer);
    }

    public int Arity()
    {
        return _declaration.Parameters.Count;
    }

    public object Call(Interpreter interpreter, List<object> arguments)
    {
        Environment environment = new(_closure);

        for (int i = 0; i < _declaration.Parameters.Count; ++i)
            environment.Define(_declaration.Parameters[i].Lexeme, arguments[i]);

        try
        {
            interpreter.ExecuteBlock(_declaration.Body, environment);
        }
        catch (Return returnValue)
        {
            if (_isInitializer)
                return _closure.GetAt(0, "this");

            return returnValue.Value;
        }
        
        if (_isInitializer)
            return _closure.GetAt(0, "this");

        return null;
    }

    public override string ToString()
    {
        return "<fn " + _declaration.Name.Lexeme + ">";
    }
}

public class Clock : ILingCallable
{
    public int Arity()
    {
        return 0;
    }

    public object Call(Interpreter interpreter, List<object> arguments)
    {
        return (double)DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    public override string ToString()
    {
        return "<native fn>";
    }
}
