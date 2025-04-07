using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LingG;

public enum TokenType
{
    // Single-character tokens.
    LEFT_PAREN, RIGHT_PAREN, LEFT_BRACE, RIGHT_BRACE,
    COMMA, DOT, MINUS, PLUS, SEMICOLON, SLASH, STAR,

    // One or two character tokens.
    BANG, BANG_EQUAL,
    EQUAL, EQUAL_EQUAL,
    GREATER, GREATER_EQUAL,
    LESS, LESS_EQUAL,

    // Literals.
    IDENTIFIER, STRING, NUMBER,

    // Keywords.
    AND, BREAK, CLASS, CONTINUE, ELSE, FALSE, FUN, FOR, IF, NIL, OR,
    PRINT, RETURN, SUPER, THIS, TRUE, VAR, WHILE,

    EOF
}

public class Token(TokenType type, string lexeme, object literal, int line)
{
    public readonly TokenType Type = type;
    public readonly string Lexeme = lexeme;
    public readonly object Literal = literal;
    public readonly int Line = line;

    public override string ToString()
    {
        return Type + " " + Lexeme + " " + Literal;
    }
}

public class ReservedWords
{
    public static ReservedWords Instance { get; private set; } = new();

    public readonly Dictionary<string, TokenType> Keywords = [];

    private ReservedWords()
    {
        Keywords["and"] = TokenType.AND;
        Keywords["break"] = TokenType.BREAK;
        Keywords["class"] = TokenType.CLASS;
        Keywords["continue"] = TokenType.CONTINUE;
        Keywords["else"] = TokenType.ELSE;
        Keywords["false"] = TokenType.FALSE;
        Keywords["for"] = TokenType.FOR;
        Keywords["fun"] = TokenType.FUN;
        Keywords["if"] = TokenType.IF;
        Keywords["nil"] = TokenType.NIL;
        Keywords["or"] = TokenType.OR;
        Keywords["print"] = TokenType.PRINT;
        Keywords["return"] = TokenType.RETURN;
        Keywords["super"] = TokenType.SUPER;
        Keywords["this"] = TokenType.THIS;
        Keywords["true"] = TokenType.TRUE;
        Keywords["var"] = TokenType.VAR;
        Keywords["while"] = TokenType.WHILE;
    }
}

public class Scanner(string source)
{
    private readonly string _source = source;
    private readonly List<Token> _tokens = [];

    private int _start = 0;
    private int _current = 0;
    private int _line = 1;

    public List<Token> ScanTokens()
    {
        while (!IsAtEnd())
        {
            _start = _current;
            ScanToken();
        }

        _tokens.Add(new(TokenType.EOF, "", null, _line));

        return _tokens;
    }

    private void ScanToken()
    {
        char c = Advance();
        switch (c)
        {
            case '(': AddToken(TokenType.LEFT_PAREN); break;
            case ')': AddToken(TokenType.RIGHT_PAREN); break;
            case '{': AddToken(TokenType.LEFT_BRACE); break;
            case '}': AddToken(TokenType.RIGHT_BRACE); break;
            case ',': AddToken(TokenType.COMMA); break;
            case '.': AddToken(TokenType.DOT); break;
            case '-': AddToken(TokenType.MINUS); break;
            case '+': AddToken(TokenType.PLUS); break;
            case ';': AddToken(TokenType.SEMICOLON); break;
            case '*': AddToken(TokenType.STAR); break;
            case '!':
                AddToken(Match('=') ? TokenType.BANG_EQUAL : TokenType.BANG);
                break;
            case '=':
                AddToken(Match('=') ? TokenType.EQUAL_EQUAL : TokenType.EQUAL);
                break;
            case '<':
                AddToken(Match('=') ? TokenType.LESS_EQUAL : TokenType.LESS);
                break;
            case '>':
                AddToken(Match('=') ? TokenType.GREATER_EQUAL : TokenType.GREATER);
                break;
            case '/':
                if (Match('/'))
                {
                    while (Peek() != '\n' && !IsAtEnd())
                        Advance();
                }
                else
                {
                    AddToken(TokenType.SLASH);
                }
                break;
            case ' ':
            case '\r':
            case '\t':
                break;
            case '\n':
                _line++;
                break;
            case '"':
                GetString();
                break;
            default:
                if (IsDigit(c))
                {
                    Number();
                }
                else if (IsAlpha(c))
                {
                    Identifier();
                }
                else
                {
                    LingError.Error(_line, "Unexpected character.");
                }
                
                break;
        }
    }

    private void Identifier()
    {
        while (IsAlphaNumeric(Peek()))
            Advance();

        string text = _source[_start.._current];
        TokenType type;

        if (ReservedWords.Instance.Keywords.TryGetValue(text, out var value))
        {
            type = value;
        }
        else
        {
            type = TokenType.IDENTIFIER;
        }

        AddToken(type);
    }

    private static bool IsAlphaNumeric(char c)
    {
        return IsAlpha(c) || IsDigit(c);
    }

    private static bool IsAlpha(char c)
    {
        return (c >= 'a' && c <= 'z') ||
               (c >= 'A' && c <= 'Z') ||
               c == '_';
    }

    private static bool IsDigit(char c)
    {
        return c >= '0' && c <= '9';
    }

    private void Number()
    {
        while (IsDigit(Peek()))
            Advance();

        if (Peek() == '.' && IsDigit(PeekNext()))
        {
            Advance();

            while (IsDigit(Peek()))
                Advance();
        }

        AddToken(TokenType.NUMBER, double.Parse(_source[_start.._current]));
    }

    private void GetString()
    {
        while (Peek() != '"' && !IsAtEnd())
        {
            if (Peek() == '\n')
                _line++;

            Advance();
        }

        if (IsAtEnd())
        {
            LingError.Error(_line, "Unterminated string.");
            return;
        }

        Advance();

        string value = _source.Substring(_start + 1, _current - _start - 2);
        AddToken(TokenType.STRING, value);
    }

    private bool Match(char expected)
    {
        if (IsAtEnd() || _source[_current] != expected)
            return false;

        _current++;
        return true;
    }

    private char PeekNext()
    {
        if (_current + 1 >= _source.Length)
            return '\0';

        return _source[_current + 1];
    }

    private char Peek()
    {
        if (IsAtEnd())
            return '\0';

        return _source[_current];
    }

    private char Advance()
    {
        return _source[_current++];
    }

    private void AddToken(TokenType type)
    {
        AddToken(type, null);
    }

    private void AddToken(TokenType type, object literal)
    {
        string text = _source[_start.._current];
        _tokens.Add(new Token(type, text, literal, _line));
    }

    private bool IsAtEnd()
    {
        return _current >= _source.Length;
    }
}
