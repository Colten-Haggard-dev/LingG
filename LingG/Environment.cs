using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LingG;

public class Environment(Environment enclosing)
{
    public readonly Environment Enclosing = enclosing;
    private readonly Dictionary<string, object> _values = [];

    public void Define(string name, object value)
    {
        // TODO: force runtime error if var already exists
        _values[name] = value;
    }

    public object Get(Token name)
    {
        if (_values.TryGetValue(name.Lexeme, out object value))
        {
            return value;
        }

        if (Enclosing != null)
            return Enclosing.Get(name);

        throw new RuntimeError(name, "Undefined variable '" + name.Lexeme + "'.");
    }

    public object GetAt(int distance, string name)
    {
        return Ancestor(distance)._values[name];
    }

    private Environment Ancestor(int distance)
    {
        Environment environment = this;

        for (int i = 0; i < distance; ++i)
            environment = environment.Enclosing;

        return environment;
    }

    public void Assign(Token name, object value)
    {
        if (_values.ContainsKey(name.Lexeme))
        {
            _values[name.Lexeme] = value;
            return;
        }

        if (Enclosing != null)
        {
            Enclosing.Assign(name, value);
            return;
        }


        throw new RuntimeError(name, "Undefined variable '" + name.Lexeme + "'.");
    }

    public void AssignAt(int distance, Token name, object value)
    {
        Ancestor(distance)._values[name.Lexeme] = value;
    }
}
