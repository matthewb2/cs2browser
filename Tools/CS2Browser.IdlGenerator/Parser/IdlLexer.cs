using System.Text;

namespace CS2Browser.IdlGenerator.Parser;

/// <summary>
/// Web IDL 토크나이저. 공백/주석을 건너뛰고 식별자, 숫자, 문자열, 기호를 토큰으로 나눈다.
/// </summary>
public sealed class IdlLexer
{
    private static readonly char[] Symbols =
    {
        '{', '}', '(', ')', '[', ']', '<', '>', ':', ';', ',', '=', '?', '*', '.', '|'
    };

    private readonly string _source;
    private int _index;
    private int _line = 1;

    public IdlLexer(string source) => _source = source ?? string.Empty;

    public List<IdlToken> Tokenize()
    {
        var tokens = new List<IdlToken>();

        while (true)
        {
            SkipTrivia();
            if (_index >= _source.Length)
            {
                tokens.Add(new IdlToken(IdlTokenKind.EndOfFile, string.Empty, _index, _line));
                return tokens;
            }

            var start = _index;
            var startLine = _line;
            var c = _source[_index];

            if (IsIdentifierStart(c))
            {
                tokens.Add(new IdlToken(IdlTokenKind.Identifier, ReadIdentifier(), start, startLine));
                continue;
            }

            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber(start, startLine));
                continue;
            }

            if (c == '"' || c == '\'')
            {
                tokens.Add(new IdlToken(IdlTokenKind.String, ReadString(c), start, startLine));
                continue;
            }

            if (c == '.' && Peek(1) == '.' && Peek(2) == '.')
            {
                _index += 3;
                tokens.Add(new IdlToken(IdlTokenKind.Symbol, "...", start, startLine));
                continue;
            }

            if (Array.IndexOf(Symbols, c) >= 0)
            {
                _index++;
                tokens.Add(new IdlToken(IdlTokenKind.Symbol, c.ToString(), start, startLine));
                continue;
            }

            // 알 수 없는 문자는 건너뛴다.
            _index++;
        }
    }

    private char Peek(int offset)
    {
        var i = _index + offset;
        return i < _source.Length ? _source[i] : '\0';
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

    private string ReadIdentifier()
    {
        var start = _index;
        while (_index < _source.Length && IsIdentifierPart(_source[_index]))
        {
            _index++;
        }

        return _source[start.._index];
    }

    private IdlToken ReadNumber(int start, int startLine)
    {
        var isFloat = false;
        while (_index < _source.Length)
        {
            var c = _source[_index];
            if (char.IsDigit(c))
            {
                _index++;
            }
            else if (c == '.' && char.IsDigit(Peek(1)))
            {
                isFloat = true;
                _index++;
            }
            else if ((c == 'e' || c == 'E') && (char.IsDigit(Peek(1)) || Peek(1) == '+' || Peek(1) == '-'))
            {
                isFloat = true;
                _index += 2;
            }
            else
            {
                break;
            }
        }

        var text = _source[start.._index];
        return new IdlToken(isFloat ? IdlTokenKind.Float : IdlTokenKind.Integer, text, start, startLine);
    }

    private string ReadString(char quote)
    {
        _index++; // opening quote
        var sb = new StringBuilder();
        while (_index < _source.Length)
        {
            var c = _source[_index++];
            if (c == '\\' && _index < _source.Length)
            {
                sb.Append(_source[_index++]);
                continue;
            }

            if (c == quote)
            {
                break;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private void SkipTrivia()
    {
        while (_index < _source.Length)
        {
            var c = _source[_index];
            if (c == '\n')
            {
                _line++;
                _index++;
            }
            else if (char.IsWhiteSpace(c))
            {
                _index++;
            }
            else if (c == '/' && Peek(1) == '/')
            {
                _index += 2;
                while (_index < _source.Length && _source[_index] != '\n')
                {
                    _index++;
                }
            }
            else if (c == '/' && Peek(1) == '*')
            {
                _index += 2;
                while (_index < _source.Length && !(_source[_index] == '*' && Peek(1) == '/'))
                {
                    if (_source[_index] == '\n')
                    {
                        _line++;
                    }

                    _index++;
                }

                _index += 2; // closing */
            }
            else
            {
                return;
            }
        }
    }
}
