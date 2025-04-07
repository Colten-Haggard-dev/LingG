using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace LingG;

public class Interpreter : Expression.IVisitor<object>, Statement.IVisitor<object>
{
    public readonly Environment Globals = new(null);
    private Environment _environment;
    private readonly Dictionary<Expression, int> _locals = [];

    public Interpreter()
    {
        _environment = Globals;

        Globals.Define("clock", new Clock());
    }

    public object Visit(Expression.Assign expression)
    {
        object value = Evaluate(expression.Value);

        if (_locals.TryGetValue(expression, out int distance))
        {
            _environment.AssignAt(distance, expression.Name, value);
        }
        else
        {
            Globals.Assign(expression.Name, value);
        }

        return value;
    }

    public object Visit(Expression.Binary expression)
    {
        object left = Evaluate(expression.Left);
        object right = Evaluate(expression.Right);

        switch(expression.Op.Type)
        {
            case TokenType.GREATER:
                CheckNumberOperands(expression.Op, left, right);
                return (double)left > (double)right;
            case TokenType.GREATER_EQUAL:
                CheckNumberOperands(expression.Op, left, right);
                return (double)left >= (double)right;
            case TokenType.LESS:
                CheckNumberOperands(expression.Op, left, right);
                return (double)left < (double)right;
            case TokenType.LESS_EQUAL:
                CheckNumberOperands(expression.Op, left, right);
                return (double)left <= (double)right;
            case TokenType.BANG_EQUAL:
                return !IsEqual(left, right);
            case TokenType.EQUAL_EQUAL:
                return IsEqual(left, right);
            case TokenType.MINUS:
                CheckNumberOperands(expression.Op, left, right);
                return (double)left - (double)right;
            case TokenType.PLUS:
                return EvalAdd(expression.Op, left, right);
            case TokenType.SLASH:
                CheckNumberOperands(expression.Op, left, right);
                return EvalDiv(expression.Op, left, right);
            case TokenType.STAR:
                CheckNumberOperands(expression.Op, left, right);
                return (double)left * (double)right;

        }

        return null;
    }

    public object Visit(Expression.Call expression)
    {
        object callee = Evaluate(expression.Callee);

        List<object> arguments = [];
        foreach (Expression argument in expression.Arguments)
        {
            arguments.Add(Evaluate(argument));
        }

        if (callee is not ILingCallable)
            throw new RuntimeError(expression.Paren, "Can only call functions and classes.");

        ILingCallable function = (ILingCallable)callee;

        if (arguments.Count != function.Arity())
        {
            throw new RuntimeError(expression.Paren, "Expected " + function.Arity() + " arguments but got " + arguments.Count + ".");
        }

        return function.Call(this, arguments);
    }

    public object Visit(Expression.Get expression)
    {
        object obj = Evaluate(expression.Obj);

        if (obj is LingInstance li)
            return li.Get(expression.Name);

        throw new RuntimeError(expression.Name, "Only instances have properties.");
    }

    public object Visit(Expression.Grouping expression)
    {
        return Evaluate(expression.Expression);
    }

    public object Visit(Expression.Literal expression)
    {
        return expression.Value;
    }

    public object Visit(Expression.Logical expression)
    {
        object left = Evaluate(expression.Left);

        if (expression.Op.Type == TokenType.OR)
        {
            if (IsTruthy(left))
                return left;
        }
        else
        {
            if (!IsTruthy(left))
                return left;
        }

        return Evaluate(expression.Right);
    }

    public object Visit(Expression.Set expression)
    {
        object obj = Evaluate(expression.Obj);

        if (obj is not LingInstance)
            throw new RuntimeError(expression.Name, "Only instances have fields.");

        object value = Evaluate(expression.Value);

        ((LingInstance)obj).Set(expression.Name, value);

        return value;
    }

    public object Visit(Expression.Super expression)
    {
        int distance = _locals[expression];

        LingClass superclass = (LingClass) _environment.GetAt(distance, "super");

        LingInstance obj = (LingInstance) _environment.GetAt(distance - 1, "this");

        LingFunction method = superclass.FindMethod(expression.Method.Lexeme);

        if (method == null)
            throw new RuntimeError(expression.Method, "Undefined property '" + expression.Method.Lexeme + "'.");

        return method.Bind(obj);
    }

    public object Visit(Expression.This expression)
    {
        return LookUpVariable(expression.Keyword, expression);
    }

    public object Visit(Expression.Unary expression)
    {
        object right = Evaluate(expression.Right);

        switch(expression.Op.Type)
        {
            case TokenType.MINUS:
                CheckNumberOperand(expression.Op, right);
                return -(double)right;
            case TokenType.BANG:
                return !IsTruthy(right);
        }

        return null;
    }

    public object Visit(Expression.Variable expression)
    {
        return LookUpVariable(expression.Name, expression);
    }

    public object Visit(Statement.Block statement)
    {
        ExecuteBlock(statement.Statements, new Environment(_environment));
        return null;
    }

    public object Visit(Statement.Break statement)
    {
        throw new Break(statement.Origin);
    }

    public object Visit(Statement.Class statement)
    {
        object superclass = null;

        if (statement.Superclass != null)
        {
            superclass = Evaluate(statement.Superclass);

            if (superclass is not LingClass)
                throw new RuntimeError(statement.Superclass.Name, "Superclass must be a class.");
        }

        _environment.Define(statement.Name.Lexeme, null);

        if (statement.Superclass != null)
        {
            _environment = new(_environment);
            _environment.Define("super", superclass);
        }

        Dictionary<string, LingFunction> methods = [];
        foreach (Statement.Function method in statement.Methods)
        {
            LingFunction function = new(method, _environment, method.Name.Lexeme == "init");
            methods[method.Name.Lexeme] = function;
        }

        LingClass clazz = new(statement.Name.Lexeme, (LingClass) superclass, methods);

        if (superclass != null)
            _environment = _environment.Enclosing;

        _environment.Assign(statement.Name, clazz);

        return null;
    }

    public object Visit(Statement.Continue statement)
    {
        throw new Continue(statement.Origin);
    }

    public object Visit(Statement.ExpressionStatement statement)
    {
        Evaluate(statement.Expr);
        return null;
    }

    public object Visit(Statement.Function statement)
    {
        LingFunction function = new(statement, _environment, false);
        _environment.Define(statement.Name.Lexeme, function);
        return null;
    }

    public object Visit(Statement.If statement)
    {
        if (IsTruthy(Evaluate(statement.Condition)))
            Execute(statement.ThenBranch);
        else if (statement.ElseBranch != null)
            Execute(statement.ElseBranch);

        return null;
    }

    public object Visit(Statement.Print statement)
    {
        object value = Evaluate(statement.Expr);
        Console.WriteLine(Stringify(value));
        return null;
    }

    public object Visit(Statement.Return statement)
    {
        object value = null;

        if (statement.Value != null)
            value = Evaluate(statement.Value);

        throw new Return(value);
    }

    public object Visit(Statement.Variable statement)
    {
        object value = null;

        if (statement.Initializer != null)
            value = Evaluate(statement.Initializer);

        _environment.Define(statement.Name.Lexeme, value);
        return null;
    }

    public object Visit(Statement.While statement)
    {
        while (IsTruthy(Evaluate(statement.Condition)))
        {
            try
            {
                Execute(statement.Body);
            }
            catch (Break)
            {
                break;
            }
            catch (Continue)
            {
                continue;
            }
        }  

        return null;
    }

    public void Interpret(List<Statement> statements)
    {
        try
        {
            foreach (Statement statement in statements)
                Execute(statement);
        }
        catch (RuntimeError e)
        {
            LingError.RuntimeError(e);
        }
    }

    public object Evaluate(Expression expression)
    {
        return expression.Accept(this);
    }

    public void Execute(Statement statement)
    {
        statement.Accept(this);
    }

    public void Resolve(Expression expression, int depth)
    {
        _locals[expression] = depth;
    }

    public void ExecuteBlock(List<Statement> statements, Environment environment)
    {
        Environment previous = _environment;

        try
        {
            _environment = environment;

            foreach (Statement statement in statements)
            {
                Execute(statement);
            }
        }
        finally
        {
            _environment = previous;
        }
    }

    private object LookUpVariable(Token name, Expression expression)
    {
        if (_locals.TryGetValue(expression, out int distance))
        {
            return _environment.GetAt(distance, name.Lexeme);
        }
        else
        {
            return Globals.Get(name);
        }
    }

    private static string Stringify(object obj)
    {
        if (obj == null)
            return "nil";

        return obj.ToString();
    }

    private static object EvalAdd(Token op, object left, object right)
    {
        if (left is double ld && right is double rd)
        {
            return ld + rd;
        }

        if (left is string ls && right is string rs)
        {
            return ls + rs;
        }

        if (left is string lls && right is double rrd)
        {
            return lls + rrd;
        }

        if (left is double lld && right is string rrs)
        {
            return lld + rrs;
        }

        throw new RuntimeError(op, "Operands must be two numbers or two strings.");
    }

    private static object EvalDiv(Token op, object left, object right)
    {
        if ((double)right != 0)
            return (double)left / (double)right;

        throw new RuntimeError(op, "Cannot divide by 0.");
    }

    private static bool IsTruthy(object obj)
    {
        if (obj == null)
            return false;

        if (obj is bool b)
            return b;

        return true;
    }

    private static bool IsEqual(object a, object b)
    {
        if (a == null && b == null)
            return true;

        if (a == null)
            return false;

        return a.Equals(b);
    }

    private static void CheckNumberOperand(Token op, object operand)
    {
        if (operand is double)
            return;

        throw new RuntimeError(op, "Operand must be a number.");
    }

    private static void CheckNumberOperands(Token op, object left, object right)
    {
        if (left is double && right is double)
            return;

        throw new RuntimeError(op, "Operand must be a number.");
    }
}
