using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LingG;

public class Parser(List<Token> tokens)
{
    //private readonly List<Token> _tokens = tokens;
    private int _current = 0;

    public List<Statement> Parse()
    {
        List<Statement> statements = [];

        while (!IsAtEnd())
        {
            statements.Add(ProcessDeclaration());
        }

        return statements;
    }

    private Statement ProcessDeclaration()
    {
        try
        {
            if (Match(TokenType.CLASS))
                return ProcessClassDeclaration();

            if (Match(TokenType.FUN))
                return ProcessFunction("function");

            if (Match(TokenType.VAR))
                return ProcessVarDeclaration();

            return ProcessStatement();
        }
        catch
        {
            Synchronize();
            return null;
        }
    }

    private Statement.Class ProcessClassDeclaration()
    {
        Token name = Consume(TokenType.IDENTIFIER, "Expect class name.");

        Expression.Variable superclass = null;
        if (Match(TokenType.LESS))
        {
            Consume(TokenType.IDENTIFIER, "Expect superclass name.");
            superclass = new Expression.Variable(Previous());
        }

        Consume(TokenType.LEFT_BRACE, "Expect '{' before class body.");

        List<Statement.Function> methods = [];
        while (!Check(TokenType.RIGHT_BRACE) && !IsAtEnd())
            methods.Add(ProcessFunction("method"));

        Consume(TokenType.RIGHT_BRACE, "Expect '}' after class body.");

        return new Statement.Class(name, superclass, methods);
    }


    private Statement.Function ProcessFunction(string kind)
    {
        Token name = Consume(TokenType.IDENTIFIER, "Expect " + kind + " name.");

        Consume(TokenType.LEFT_PAREN, "Expect '(' after " + kind + " name.");

        List<Token> parameters = [];

        if (!Check(TokenType.RIGHT_PAREN))
        {
            do
            {
                if (parameters.Count >= 255)
                    Error(Peek(), "Can't have more than 255 parameters.");

                parameters.Add(Consume(TokenType.IDENTIFIER, "Expect parameter name."));
            }
            while (Match(TokenType.COMMA));
        }

        Consume(TokenType.RIGHT_PAREN, "Expect ')' after parameters.");

        Consume(TokenType.LEFT_BRACE, "Expect '{' before " + kind + " body.");

        List<Statement> body = ProcessBlock();

        return new Statement.Function(name, parameters, body);
    }

    private Statement.Variable ProcessVarDeclaration()
    {
        Token name = Consume(TokenType.IDENTIFIER, "Expect variable name.");

        Expression initializer = null;

        if (Match(TokenType.EQUAL))
        {
            initializer = ProcessExpression();
        }

        Consume(TokenType.SEMICOLON, "Expect ';' after variable declaration.");
        return new Statement.Variable(name, initializer);
    }

    private Statement ProcessStatement()
    {
        if (Match(TokenType.BREAK))
            return ProcessBreakStatement();

        if (Match(TokenType.CONTINUE))
            return ProcessContinueStatement();

        if (Match(TokenType.FOR))
            return ProcessForStatement();

        if (Match(TokenType.IF))
            return ProcessIfStatement();

        if (Match(TokenType.PRINT))
            return ProcessPrintStatement();

        if (Match(TokenType.RETURN))
            return ProcessReturnStatement();

        if (Match(TokenType.WHILE))
            return ProcessWhileStatement();

        if (Match(TokenType.LEFT_BRACE))
            return new Statement.Block(ProcessBlock());

        return ProcessExpressionStatement();
    }

    private Statement.Break ProcessBreakStatement()
    {
        Token origin = Previous();
        Consume(TokenType.SEMICOLON, "Expect ';' after break statement.");

        return new Statement.Break(origin);
    }

    private Statement.Continue ProcessContinueStatement()
    {
        Token origin = Previous();
        Consume(TokenType.SEMICOLON, "Expect ';' after continue statement.");

        return new Statement.Continue(origin);
    }

    private Statement ProcessForStatement()
    {
        Consume(TokenType.LEFT_PAREN, "Expect '(' after 'for'.");

        Statement initializer;
        if (Match(TokenType.SEMICOLON))
            initializer = null;
        else if (Match(TokenType.VAR))
            initializer = ProcessVarDeclaration();
        else
            initializer = ProcessExpressionStatement();

        Expression condition = null;

        if (!Check(TokenType.SEMICOLON))
            condition = ProcessExpression();

        Consume(TokenType.SEMICOLON, "Expect ';' after loop condition.");

        Expression increment = null;

        if (!Check(TokenType.RIGHT_PAREN))
            increment = ProcessExpression();

        Consume(TokenType.RIGHT_PAREN, "Expect ')' after for clauses.");

        Statement body = ProcessStatement();

        if (increment != null)
            body = new Statement.Block([body, new Statement.ExpressionStatement(increment)]);

        if (condition == null)
            condition = new Expression.Literal(true);

        body = new Statement.While(condition, body);

        if (initializer != null)
            body = new Statement.Block([initializer, body]);

        return body;
    }

    private Statement.If ProcessIfStatement()
    {
        Consume(TokenType.LEFT_PAREN, "Expect '(' after 'if'.");
        Expression condition = ProcessExpression();
        Consume(TokenType.RIGHT_PAREN, "Expect ')' after if condition.");

        Statement thenBranch = ProcessStatement();
        Statement elseBranch = null;

        if (Match(TokenType.ELSE))
            elseBranch = ProcessStatement();

        return new Statement.If(condition, thenBranch, elseBranch);
    }

    private Statement.Print ProcessPrintStatement()
    {
        Expression value = ProcessExpression();
        Consume(TokenType.SEMICOLON, "Expect ';' after value.");
        return new Statement.Print(value);
    }

    private Statement.Return ProcessReturnStatement()
    {
        Token keyword = Previous();

        Expression value = null;

        if (!Check(TokenType.SEMICOLON))
        {
            value = ProcessExpression();
        }

        Consume(TokenType.SEMICOLON, "Expect ';' after return value.");

        return new Statement.Return(keyword, value);
    }

    private Statement.While ProcessWhileStatement()
    {
        Consume(TokenType.LEFT_PAREN, "Expect '(' after 'while'.");
        Expression condition = ProcessExpression();
        Consume(TokenType.RIGHT_PAREN, "Expect ')' after condition.");
        Statement body = ProcessStatement();

        return new Statement.While(condition, body);
    }

    private Statement.ExpressionStatement ProcessExpressionStatement()
    {
        Expression expression = ProcessExpression();
        Consume(TokenType.SEMICOLON, "Expect ';' after expression.");
        return new Statement.ExpressionStatement(expression);
    }

    private List<Statement> ProcessBlock()
    {
        List<Statement> statements = [];

        while (!Check(TokenType.RIGHT_BRACE) && !IsAtEnd())
        {
            statements.Add(ProcessDeclaration());
        }

        Consume(TokenType.RIGHT_BRACE, "Expect '}' after block.");
        return statements;
    }

    private Expression ProcessExpression()
    {
        return ProcessAssignment();
    }

    private Expression ProcessAssignment()
    {
        Expression expression = ProcessOr();

        if (Match(TokenType.EQUAL))
        {
            Token equals = Previous();
            Expression value = ProcessAssignment();

            if (expression is Expression.Variable variable)
            {
                Token name = variable.Name;
                return new Expression.Assign(name, value);
            }
            else if (expression is Expression.Get get)
            {
                return new Expression.Set(get.Obj, get.Name, value);
            }

            Error(equals, "Invalid assignment target.");
        }

        return expression;
    }

    private Expression ProcessOr()
    {
        Expression expression = ProcessAnd();

        while (Match(TokenType.OR))
        {
            Token op = Previous();
            Expression right = ProcessAnd();
            expression = new Expression.Logical(expression, op, right);
        }

        return expression;
    }

    private Expression ProcessAnd()
    {
        Expression expression = ProcessEquality();

        while (Match(TokenType.AND))
        {
            Token op = Previous();
            Expression right = ProcessEquality();
            expression = new Expression.Logical(expression, op, right);
        }

        return expression;
    }

    private Expression ProcessEquality()
    {
        Expression expr = ProcessComparison();

        while (Match(TokenType.BANG_EQUAL, TokenType.EQUAL_EQUAL))
        {
            Token op = Previous();
            Expression right = ProcessComparison();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression ProcessComparison()
    {
        Expression expr = ProcessTerm();

        while (Match(TokenType.GREATER, TokenType.GREATER_EQUAL, TokenType.LESS, TokenType.LESS_EQUAL))
        {
            Token op = Previous();
            Expression right = ProcessTerm();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression ProcessTerm()
    {
        Expression expr = ProcessFactor();

        while (Match(TokenType.MINUS, TokenType.PLUS))
        {
            Token op = Previous();
            Expression right = ProcessFactor();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression ProcessFactor()
    {
        Expression expr = ProcessUnary();

        while (Match(TokenType.SLASH, TokenType.STAR))
        {
            Token op = Previous();
            Expression right = ProcessUnary();
            expr = new Expression.Binary(expr, op, right);
        }

        return expr;
    }

    private Expression ProcessUnary()
    {
        if (Match(TokenType.BANG, TokenType.MINUS))
        {
            Token op = Previous();
            Expression right = ProcessUnary();
            return new Expression.Unary(op, right);
        }

        return ProcessCall();
    }

    private Expression ProcessCall()
    {
        Expression expression = ProcessPrimary();

        while (true)
        {
            if (Match(TokenType.LEFT_PAREN))
                expression = ProcessFinishCall(expression);
            else if (Match(TokenType.DOT))
            {
                Token name = Consume(TokenType.IDENTIFIER, "Expect property name after '.'.");

                expression = new Expression.Get(expression, name);
            }
            else
                break;
        }
        return expression;
    }

    private Expression.Call ProcessFinishCall(Expression callee)
    {
        List<Expression> arguments = [];

        if (!Check(TokenType.RIGHT_PAREN))
        {
            do
            {
                if (arguments.Count >= 255)
                    Error(Peek(), "Can't have more than 255 arguments.");

                arguments.Add(ProcessExpression());
            }
            while (Match(TokenType.COMMA));
        }

        Token paren = Consume(TokenType.RIGHT_PAREN, "Expect ')' after arguments.");

        return new Expression.Call(callee, paren, arguments);
    }

    private Expression ProcessPrimary()
    {
        if (Match(TokenType.FALSE))
            return new Expression.Literal(false);

        if (Match(TokenType.TRUE))
            return new Expression.Literal(true);

        if (Match(TokenType.NIL))
            return new Expression.Literal(null);

        if (Match(TokenType.NUMBER, TokenType.STRING))
            return new Expression.Literal(Previous().Literal);

        if (Match(TokenType.SUPER))
        {
            Token keyword = Previous();
            Consume(TokenType.DOT, "Expect '.' after 'super'.");
            Token method = Consume(TokenType.IDENTIFIER, "Expect superclass method name.");

            return new Expression.Super(keyword, method);
        }

        if (Match(TokenType.THIS))
            return new Expression.This(Previous());

        if (Match(TokenType.IDENTIFIER))
            return new Expression.Variable(Previous());

        if (Match(TokenType.LEFT_PAREN))
        {
            Expression expr = ProcessExpression();
            Consume(TokenType.RIGHT_PAREN, "Expect ')' after expression");
            return new Expression.Grouping(expr);
        }

        throw Error(Peek(), "Expect expression.");
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type))
            return Advance();

        throw Error(Peek(), message);
    }

    private bool Match(params TokenType[] types)
    {
        foreach (TokenType type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }

        return false;
    }

    private Token Advance()
    {
        if (!IsAtEnd())
            _current++;

        return Previous();
    }

    private bool Check(TokenType type)
    {
        if (IsAtEnd())
            return false;

        return Peek().Type == type;
    }

    private bool IsAtEnd()
    {
        return Peek().Type == TokenType.EOF;
    }

    private Token Peek()
    {
        return tokens[_current];
    }

    private Token Previous()
    {
        return tokens[_current - 1];
    }

    private static ParseError Error(Token token, string message)
    {
        LingError.Error(token, message);
        return new ParseError();
    }

    private void Synchronize()
    {
        Advance();

        while (!IsAtEnd())
        {
            if (Previous().Type == TokenType.SEMICOLON)
                return;

            switch (Peek().Type)
            {
                case TokenType.CLASS:
                case TokenType.FUN:
                case TokenType.VAR:
                case TokenType.FOR:
                case TokenType.IF:
                case TokenType.WHILE:
                case TokenType.PRINT:
                case TokenType.RETURN:
                    return;
            }

            Advance();
        }
    }
}

public abstract class Expression
{
    public interface IVisitor<T>
    {
        T Visit(Assign expression);
        T Visit(Binary expression);
        T Visit(Call expression);
        T Visit(Get expression);
        T Visit(Grouping expression);
        T Visit(Literal expression);
        T Visit(Logical expression);
        T Visit(Set expression);
        T Visit(Super expression);
        T Visit(This expression);
        T Visit(Unary expression);
        T Visit(Variable expression);
    }

    public class Assign(Token name, Expression value) : Expression
    {
        public readonly Token Name = name;
        public readonly Expression Value = value;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Binary(Expression left, Token op, Expression right) : Expression
    {
        public readonly Expression Left = left;
        public readonly Token Op = op;
        public readonly Expression Right = right;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Call(Expression callee, Token paren, List<Expression> arguments) : Expression
    {
        public readonly Expression Callee = callee;
        public readonly Token Paren = paren;
        public readonly List<Expression> Arguments = arguments;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Get(Expression obj, Token name) : Expression
    {
        public readonly Expression Obj = obj;
        public readonly Token Name = name;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Grouping(Expression expression) : Expression
    {
        public readonly Expression Expression = expression;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Literal(object value) : Expression
    {
        public readonly object Value = value;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Logical(Expression left, Token op, Expression right) : Expression
    {
        public readonly Expression Left = left;
        public readonly Token Op = op;
        public readonly Expression Right = right;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Set(Expression obj, Token name, Expression value) : Expression
    {
        public readonly Expression Obj = obj;
        public readonly Token Name = name;
        public readonly Expression Value = value;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Super(Token keyword, Token method) : Expression
    {
        public readonly Token Keyword = keyword;
        public readonly Token Method = method;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class This(Token keyword) : Expression
    {
        public readonly Token Keyword = keyword;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Unary(Token op, Expression right) : Expression
    {
        public readonly Token Op = op;
        public readonly Expression Right = right;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Variable(Token name) : Expression
    {
        public readonly Token Name = name;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public abstract T Accept<T>(IVisitor<T> visitor);
}

public abstract class Statement
{
    public interface IVisitor<T>
    {
        T Visit(Block statement);
        T Visit(Break statement);
        T Visit(Class statement);
        T Visit(Continue statement);
        T Visit(ExpressionStatement statement);
        T Visit(Function statement);
        T Visit(If statement);
        T Visit(Print statement);
        T Visit(Return statement);
        T Visit(Variable statement);
        T Visit(While statement);
    }

    public class Block(List<Statement> statements) : Statement
    {
        public readonly List<Statement> Statements = statements;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Break(Token origin) : Statement
    {
        public readonly Token Origin = origin;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Class(Token name, Expression.Variable superclass, List<Statement.Function> methods) : Statement
    {
        public readonly Token Name = name;
        public readonly Expression.Variable Superclass = superclass;
        public readonly List<Statement.Function> Methods = methods;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Continue(Token origin) : Statement
    {
        public readonly Token Origin = origin;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class ExpressionStatement(Expression expr) : Statement
    {
        public readonly Expression Expr = expr;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Function(Token name, List<Token> parameters, List<Statement> body) : Statement
    {
        public readonly Token Name = name;
        public readonly List<Token> Parameters = parameters;
        public readonly List<Statement> Body = body;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class If(Expression condition, Statement thenBranch, Statement elseBranch) : Statement
    {
        public readonly Expression Condition = condition;
        public readonly Statement ThenBranch = thenBranch;
        public readonly Statement ElseBranch = elseBranch;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Print(Expression expr) : Statement
    {
        public readonly Expression Expr = expr;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Return(Token keyword, Expression value) : Statement
    {
        public readonly Token Keyword = keyword;
        public readonly Expression Value = value;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class Variable(Token name, Expression initializer) : Statement
    {
        public readonly Token Name = name;
        public readonly Expression Initializer = initializer;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class While(Expression condition, Statement body) : Statement
    {
        public readonly Expression Condition = condition;
        public readonly Statement Body = body;

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public abstract T Accept<T>(IVisitor<T> visitor);
}