using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace LingG;

public enum FunctionType
{
    NONE,
    FUNCTION,
    INITIALIZER,
    METHOD
}

public enum ClassType
{
    NONE,
    CLASS,
    SUBCLASS
}

public class Resolver(Interpreter interpreter) : Expression.IVisitor<object>, Statement.IVisitor<object>
{
    private readonly Interpreter _interpreter = interpreter;
    private readonly Stack<Dictionary<string, bool>> _scopes = [];
    private FunctionType _currentFunction = FunctionType.NONE;
    private ClassType _currentClass = ClassType.NONE;

    public object Visit(Expression.Assign expression)
    {
        Resolve(expression.Value);
        ResolveLocal(expression, expression.Name);

        return null;
    }

    public object Visit(Expression.Binary expression)
    {
        Resolve(expression.Left);
        Resolve(expression.Right);

        return null;
    }

    public object Visit(Expression.Call expression)
    {
        Resolve(expression.Callee);

        foreach (Expression argument in expression.Arguments)
            Resolve(argument);

        return null;
    }

    public object Visit(Expression.Get expression)
    {
        Resolve(expression.Obj);

        return null;
    }

    public object Visit(Expression.Grouping expression)
    {
        Resolve(expression.Expression);

        return null;
    }

    public object Visit(Expression.Literal expression)
    {
        return null;
    }

    public object Visit(Expression.Logical expression)
    {
        Resolve(expression.Left);
        Resolve(expression.Right);

        return null;
    }

    public object Visit(Expression.Set expression)
    {
        Resolve(expression.Value);
        Resolve(expression.Obj);

        return null;
    }

    public object Visit(Expression.Super expression)
    {
        if (_currentClass == ClassType.NONE)
            LingError.Error(expression.Keyword, "Can't use 'super' outside of a class.");
        else if (_currentClass != ClassType.SUBCLASS)
            LingError.Error(expression.Keyword, "Can't use 'super' in a class with no superclass.");

        ResolveLocal(expression, expression.Keyword);

        return null;
    }

    public object Visit(Expression.This expression)
    {
        if (_currentClass == ClassType.NONE)
            LingError.Error(expression.Keyword, "Can't use 'this' outside of a class.");

        ResolveLocal(expression, expression.Keyword);

        return null;
    }

    public object Visit(Expression.Unary expression)
    {
        Resolve(expression.Right);

        return null;
    }

    public object Visit(Expression.Variable expression)
    {
        if (!(_scopes.Count == 0) && !_scopes.Peek()[expression.Name.Lexeme])
        {
            LingError.Error(expression.Name, "Can't read local variable in its own initializer.");
        }

        ResolveLocal(expression, expression.Name);

        return null;
    }

    public object Visit(Statement.Block statement)
    {
        BeginScope();
        Resolve(statement.Statements);
        EndScope();

        return null;
    }

    public object Visit(Statement.Break statement)
    {
        return null;
    }

    public object Visit(Statement.Class statement)
    {
        ClassType enclosingClass = _currentClass;
        _currentClass = ClassType.CLASS;

        Declare(statement.Name);
        Define(statement.Name);

        if (statement.Superclass != null && statement.Name.Lexeme == statement.Superclass.Name.Lexeme)
        {
            LingError.Error(statement.Superclass.Name, "A class can't inherit from itself.");
        }

        if (statement.Superclass != null)
        {
            _currentClass = ClassType.SUBCLASS;
            Resolve(statement.Superclass);
        }

        if (statement.Superclass != null)
        {
            BeginScope();
            _scopes.Peek()["super"] = true;
        }

        BeginScope();
        _scopes.Peek()["this"] = true;

        foreach (Statement.Function method in statement.Methods)
        {
            FunctionType declaration = FunctionType.METHOD;
            if (method.Name.Lexeme == "init")
                declaration = FunctionType.INITIALIZER;

            ResolveFunction(method, declaration);
        }

        EndScope();

        if (statement.Superclass != null)
            EndScope();

        _currentClass = enclosingClass;

        return null;
    }

    public object Visit(Statement.Continue statement)
    {
        return null;
    }

    public object Visit(Statement.ExpressionStatement statement)
    {
        Resolve(statement.Expr);

        return null;
    }

    public object Visit(Statement.Function statement)
    {
        Declare(statement.Name);
        Define(statement.Name);

        ResolveFunction(statement, FunctionType.NONE);

        return null;
    }

    public object Visit(Statement.If statement)
    {
        Resolve(statement.Condition);
        Resolve(statement.ThenBranch);

        if (statement.ElseBranch != null)
            Resolve(statement.ElseBranch);

        return null;
    }

    public object Visit(Statement.Print statement)
    {
        Resolve(statement.Expr);

        return null;
    }

    public object Visit(Statement.Return statement)
    {
        if (_currentFunction == FunctionType.NONE)
            LingError.Error(statement.Keyword, "Can't return from top-level code.");

        if (statement.Value != null)
        {
            if (_currentFunction == FunctionType.INITIALIZER)
                LingError.Error(statement.Keyword, "Can't return a value from an initializer.");

            Resolve(statement.Value);
        }
            

        return null;
    }

    public object Visit(Statement.Variable statement)
    {
        Declare(statement.Name);

        if (statement.Initializer != null)
        {
            Resolve(statement.Initializer);
        }

        Define(statement.Name);

        return null;
    }

    public object Visit(Statement.While statement)
    {
        Resolve(statement.Condition);
        Resolve(statement.Body);

        return null;
    }

    public void Resolve(List<Statement> statements)
    {
        foreach (Statement statement in statements)
            Resolve(statement);
    }

    private void Resolve(Statement statement)
    {
        statement.Accept(this);
    }

    private void Resolve(Expression expression)
    {
        expression.Accept(this);
    }

    private void BeginScope()
    {
        _scopes.Push([]);
    }

    private void EndScope()
    {
        _scopes.Pop();
    }

    private void Declare(Token name)
    {
        if (_scopes.Count == 0)
            return;

        Dictionary<string, bool> scope = _scopes.Peek();

        if (scope.ContainsKey(name.Lexeme))
            LingError.Error(name, "Already a variable with this name in this scope.");

        scope[name.Lexeme] = false;
    }

    private void Define(Token name)
    {
        if (_scopes.Count == 0)
            return;

        _scopes.Peek()[name.Lexeme] = true;
    }

    private void ResolveLocal(Expression expression, Token name)
    {
        Dictionary<string, bool>[] scopes = [.. _scopes];
        for (int i = _scopes.Count - 1; i >= 0; --i)
        {
            if (scopes[i].ContainsKey(name.Lexeme))
            {
                _interpreter.Resolve(expression, scopes.Length - 1 - i);
                return;
            }
        }
    }

    private void ResolveFunction(Statement.Function function, FunctionType type)
    {
        FunctionType enclosingFunction = _currentFunction;
        _currentFunction = type;

        BeginScope();

        foreach (Token param in function.Parameters)
        {
            Declare(param);
            Define(param);
        }

        Resolve(function.Body);

        EndScope();

        _currentFunction = enclosingFunction;
    }
}
