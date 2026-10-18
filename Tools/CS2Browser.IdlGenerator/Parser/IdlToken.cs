namespace CS2Browser.IdlGenerator.Parser;

public enum IdlTokenKind
{
    Identifier,
    Integer,
    Float,
    String,
    Symbol,
    EndOfFile
}

/// <summary>Web IDL 토큰. 텍스트와 위치 정보를 담는다.</summary>
public readonly struct IdlToken
{
    public IdlTokenKind Kind { get; }
    public string Text { get; }
    public int Position { get; }
    public int Line { get; }

    public IdlToken(IdlTokenKind kind, string text, int position, int line)
    {
        Kind = kind;
        Text = text;
        Position = position;
        Line = line;
    }

    public bool Is(string text) => string.Equals(Text, text, StringComparison.Ordinal);

    public override string ToString() => $"{Kind}:'{Text}' (line {Line})";
}
