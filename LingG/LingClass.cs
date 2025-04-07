using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LingG;

public class LingClass(string name, LingClass superclass, Dictionary<string, LingFunction> methods) : ILingCallable
{
    public readonly LingClass SuperClass = superclass;
    public readonly string Name = name;
    private readonly Dictionary<string, LingFunction> _methods = methods;

    public LingFunction FindMethod(string name)
    {
        if (_methods.TryGetValue(name, out LingFunction method))
            return method;

        if (SuperClass != null)
            return SuperClass.FindMethod(name);

        return null;
    }

    public int Arity()
    {
        LingFunction initializer = FindMethod("init");

        if (initializer == null)
            return 0;

        return initializer.Arity();
    }

    public object Call(Interpreter interpreter, List<object> arguments)
    {
        LingInstance instance = new(this);

        LingFunction initializer = FindMethod("init");

        if (initializer != null)
            initializer.Bind(instance).Call(interpreter, arguments);

        return instance;
    }

    public override string ToString()
    {
        return Name;
    }
}

public class LingInstance(LingClass clazz)
{
    private readonly LingClass _clazz = clazz;
    private readonly Dictionary<string, object> _fields = [];

    public object Get(Token name)
    {
        if (_fields.TryGetValue(name.Lexeme, out object value))
            return value;

        LingFunction method = _clazz.FindMethod(name.Lexeme);

        if (method != null)
            return method.Bind(this);

        throw new RuntimeError(name, "Undefined property '" + name.Lexeme + "'.");
    }

    public void Set(Token name, object value)
    {
        _fields[name.Lexeme] = value;
    }

    public override string ToString()
    {
        return _clazz.Name + " instance";
    }
}
