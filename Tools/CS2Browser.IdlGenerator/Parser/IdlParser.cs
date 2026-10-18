using CS2Browser.IdlGenerator.Model;

namespace CS2Browser.IdlGenerator.Parser;

public sealed class IdlParseException : Exception
{
    public IdlParseException(string message, IdlToken token)
        : base($"{message} (line {token.Line}, token '{token.Text}')")
    {
    }
}

/// <summary>
/// Web IDL 재귀 하강 파서. DOM 바인딩에 필요한 부분집합
/// (interface/partial/mixin, attribute, operation, const, enum, dictionary, typedef)을 파싱한다.
/// </summary>
public sealed class IdlParser
{
    private readonly List<IdlToken> _tokens;
    private int _index;

    public IdlParser(string source)
    {
        _tokens = new IdlLexer(source).Tokenize();
    }

    public static IdlModule Parse(params string[] sources)
    {
        var module = new IdlModule();
        foreach (var source in sources)
        {
            new IdlParser(source).ParseInto(module);
        }

        MergePartials(module);
        return module;
    }

    public IdlModule Parse()
    {
        var module = new IdlModule();
        ParseInto(module);
        MergePartials(module);
        return module;
    }

    private void ParseInto(IdlModule module)
    {
        while (Current.Kind != IdlTokenKind.EndOfFile)
        {
            ParseTopLevel(module);
        }
    }

    // ---------------------------------------------------------------- top-level

    private void ParseTopLevel(IdlModule module)
    {
        var attrs = ParseExtendedAttributes();

        if (MatchKeyword("interface"))
        {
            var mixin = MatchKeyword("mixin");
            ParseInterfaceBody(module, attrs, partial: false, mixin: mixin);
        }
        else if (MatchKeyword("partial"))
        {
            if (MatchKeyword("interface"))
            {
                var mixin = MatchKeyword("mixin");
                ParseInterfaceBody(module, attrs, partial: true, mixin: mixin);
            }
            else
            {
                SkipUnknown();
            }
        }
        else if (MatchKeyword("enum"))
        {
            ParseEnum(module);
        }
        else if (MatchKeyword("dictionary"))
        {
            ParseDictionary(module);
        }
        else if (MatchKeyword("typedef"))
        {
            ParseTypedef(module);
        }
        else
        {
            SkipUnknown();
        }
    }

    private void ParseInterfaceBody(IdlModule module, List<string> attrs, bool partial, bool mixin)
    {
        var name = ExpectIdentifier();
        string? inheritance = null;
        if (Match(":"))
        {
            inheritance = ExpectIdentifier();
        }

        var iface = new IdlInterface
        {
            Name = name,
            Inheritance = inheritance,
            IsPartial = partial,
            IsMixin = mixin
        };
        iface.ExtendedAttributes.AddRange(attrs);

        Expect("{");
        while (!Match("}"))
        {
            if (Current.Kind == IdlTokenKind.EndOfFile)
            {
                break;
            }

            ParseInterfaceMember(iface);
        }

        Match(";");
        module.Interfaces.Add(iface);
    }

    private void ParseInterfaceMember(IdlInterface iface)
    {
        var attrs = ParseExtendedAttributes();
        var isStatic = false;

        if (MatchKeyword("readonly"))
        {
            ExpectKeyword("attribute");
            ParseAttribute(iface, attrs, isStatic, isReadonly: true);
            return;
        }

        if (MatchKeyword("attribute"))
        {
            ParseAttribute(iface, attrs, isStatic, isReadonly: false);
            return;
        }

        if (MatchKeyword("const"))
        {
            ParseConstant(iface);
            return;
        }

        if (MatchKeyword("static"))
        {
            isStatic = true;
            if (MatchKeyword("readonly"))
            {
                ExpectKeyword("attribute");
                ParseAttribute(iface, attrs, isStatic, isReadonly: true);
                return;
            }

            if (MatchKeyword("attribute"))
            {
                ParseAttribute(iface, attrs, isStatic, isReadonly: false);
                return;
            }

            ParseOperation(iface, attrs, isStatic);
            return;
        }

        if (MatchKeyword("stringifier"))
        {
            if (Match(";"))
            {
                return;
            }

            if (MatchKeyword("readonly"))
            {
                ExpectKeyword("attribute");
                ParseAttribute(iface, attrs, isStatic, isReadonly: true);
                return;
            }

            if (MatchKeyword("attribute"))
            {
                ParseAttribute(iface, attrs, isStatic, isReadonly: false);
                return;
            }
        }

        if (MatchKeyword("constructor"))
        {
            Expect("(");
            var ctorArgs = ParseArguments();
            Expect(")");
            Expect(";");
            var ctor = new IdlOperation
            {
                Name = "constructor",
                ReturnType = IdlType.Simple("void")
            };
            ctor.Arguments.AddRange(ctorArgs);
            iface.Operations.Add(ctor);
            return;
        }

        if (MatchKeyword("iterable") || MatchKeyword("maplike") || MatchKeyword("setlike"))
        {
            // iterable<...>; 등은 건너뛴다.
            SkipToSemicolon();
            return;
        }

        ParseOperation(iface, attrs, isStatic);
    }

    private void ParseAttribute(IdlInterface iface, List<string> attrs, bool isStatic, bool isReadonly)
    {
        var attr = new IdlAttribute
        {
            Type = ParseType(),
            Name = ExpectIdentifier(),
            Static = isStatic,
            Readonly = isReadonly
        };
        attr.ExtendedAttributes.AddRange(attrs);
        Expect(";");
        iface.Attributes.Add(attr);
    }

    private void ParseConstant(IdlInterface iface)
    {
        var constant = new IdlConstant
        {
            Type = ParseType(),
            Name = ExpectIdentifier()
        };
        Expect("=");
        constant.Value = ReadUntilSemicolon();
        Expect(";");
        iface.Constants.Add(constant);
    }

    private void ParseOperation(IdlInterface iface, List<string> attrs, bool isStatic)
    {
        var op = new IdlOperation
        {
            ReturnType = ParseType(),
            Name = ExpectIdentifier(),
            Static = isStatic
        };
        op.ExtendedAttributes.AddRange(attrs);
        Expect("(");
        op.Arguments.AddRange(ParseArguments());
        Expect(")");
        Expect(";");
        iface.Operations.Add(op);
    }

    private List<IdlArgument> ParseArguments()
    {
        var args = new List<IdlArgument>();
        if (Current.Is(")"))
        {
            return args;
        }

        while (true)
        {
            ParseExtendedAttributes();
            var arg = new IdlArgument
            {
                Optional = MatchKeyword("optional"),
                Type = ParseType()
            };
            arg.Variadic = Match("...");

            if (Current.Kind == IdlTokenKind.Identifier && !Current.Is("or"))
            {
                arg.Name = Advance().Text;
            }

            if (Match("="))
            {
                arg.DefaultValue = ParseDefaultValue();
            }

            if (string.IsNullOrEmpty(arg.Name))
            {
                arg.Name = "arg" + args.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            args.Add(arg);

            if (!Match(","))
            {
                break;
            }
        }

        return args;
    }

    // ---------------------------------------------------------------- types

    private IdlType ParseType()
    {
        var first = ParseSingleType();
        if (!MatchKeyword("or"))
        {
            return first;
        }

        var union = new IdlType { Name = "union" };
        union.Union.Add(first);
        while (true)
        {
            union.Union.Add(ParseSingleType());
            if (!MatchKeyword("or"))
            {
                break;
            }
        }

        return union;
    }

    private IdlType ParseSingleType()
    {
        var type = new IdlType { Name = ReadTypeName() };

        if (Match("<"))
        {
            type.Parameters.Add(ParseType());
            while (Match(","))
            {
                type.Parameters.Add(ParseType());
            }

            Expect(">");
        }

        if (Match("?"))
        {
            type.Nullable = true;
        }

        return type;
    }

    private string ReadTypeName()
    {
        var id = ExpectIdentifier();

        if (string.Equals(id, "unsigned", StringComparison.Ordinal))
        {
            var next = ExpectIdentifier();
            if (string.Equals(next, "long", StringComparison.Ordinal) && MatchKeyword("long"))
            {
                return "unsigned long long";
            }

            return "unsigned " + next;
        }

        if (string.Equals(id, "long", StringComparison.Ordinal) && MatchKeyword("long"))
        {
            return "long long";
        }

        if (string.Equals(id, "unrestricted", StringComparison.Ordinal))
        {
            return "unrestricted " + ExpectIdentifier();
        }

        return id;
    }

    // ---------------------------------------------------------------- enums / dictionaries / typedefs

    private void ParseEnum(IdlModule module)
    {
        var e = new IdlEnum { Name = ExpectIdentifier() };
        Expect("{");
        while (!Match("}"))
        {
            if (Current.Kind == IdlTokenKind.EndOfFile)
            {
                break;
            }

            if (Current.Kind == IdlTokenKind.String)
            {
                e.Values.Add(Advance().Text);
            }
            else
            {
                Advance();
            }

            Match(",");
        }

        Match(";");
        module.Enums.Add(e);
    }

    private void ParseDictionary(IdlModule module)
    {
        var d = new IdlDictionary { Name = ExpectIdentifier() };
        if (Match(":"))
        {
            d.Inheritance = ExpectIdentifier();
        }

        Expect("{");
        while (!Match("}"))
        {
            if (Current.Kind == IdlTokenKind.EndOfFile)
            {
                break;
            }

            ParseExtendedAttributes();
            MatchKeyword("required");
            var type = ParseType();
            var member = new IdlArgument { Name = ExpectIdentifier(), Type = type };
            if (Match("="))
            {
                member.DefaultValue = ParseDefaultValue();
            }

            Expect(";");
            d.Members.Add(member);
        }

        Match(";");
        module.Dictionaries.Add(d);
    }

    private void ParseTypedef(IdlModule module)
    {
        var type = ParseType();
        var name = ExpectIdentifier();
        Expect(";");
        module.Typedefs.Add(new IdlTypedef { Name = name, Type = type });
    }

    // ---------------------------------------------------------------- extended attributes

    private List<string> ParseExtendedAttributes()
    {
        var attrs = new List<string>();
        if (!Match("["))
        {
            return attrs;
        }

        while (true)
        {
            var name = ExpectIdentifier();
            if (Match(":"))
            {
                name += ":" + ExpectIdentifier();
            }

            attrs.Add(name);

            if (Match("="))
            {
                SkipAttributeValue();
            }

            if (Match("("))
            {
                SkipBalanced("(", ")");
            }

            if (!Match(","))
            {
                break;
            }
        }

        Expect("]");
        return attrs;
    }

    private void SkipAttributeValue()
    {
        if (Match("("))
        {
            SkipBalanced("(", ")");
            return;
        }

        if (Current.Kind != IdlTokenKind.EndOfFile)
        {
            Advance();
        }
    }

    // ---------------------------------------------------------------- helpers

    private string ParseDefaultValue()
    {
        var parts = new List<string>();
        var depth = 0;
        while (Current.Kind != IdlTokenKind.EndOfFile)
        {
            if (depth == 0 && (Current.Is(",") || Current.Is(")")))
            {
                break;
            }

            var token = Advance();
            if (token.Is("(") || token.Is("[") || token.Is("{"))
            {
                depth++;
            }
            else if (token.Is(")") || token.Is("]") || token.Is("}"))
            {
                depth--;
            }

            parts.Add(token.Kind == IdlTokenKind.String ? '"' + token.Text + '"' : token.Text);
        }

        return string.Concat(parts);
    }

    private string ReadUntilSemicolon()
    {
        var parts = new List<string>();
        var depth = 0;
        while (Current.Kind != IdlTokenKind.EndOfFile)
        {
            if (depth == 0 && Current.Is(";"))
            {
                break;
            }

            var token = Advance();
            if (token.Is("(") || token.Is("[") || token.Is("{"))
            {
                depth++;
            }
            else if (token.Is(")") || token.Is("]") || token.Is("}"))
            {
                depth--;
            }

            parts.Add(token.Kind == IdlTokenKind.String ? '"' + token.Text + '"' : token.Text);
        }

        return string.Concat(parts);
    }

    private void SkipUnknown()
    {
        var depth = 0;
        while (Current.Kind != IdlTokenKind.EndOfFile)
        {
            var token = Advance();
            if (token.Is("{"))
            {
                depth++;
            }
            else if (token.Is("}"))
            {
                depth--;
                if (depth <= 0)
                {
                    Match(";");
                    return;
                }
            }
            else if (token.Is(";") && depth == 0)
            {
                return;
            }
        }
    }

    private void SkipToSemicolon()
    {
        while (Current.Kind != IdlTokenKind.EndOfFile && !Current.Is(";"))
        {
            Advance();
        }

        Match(";");
    }

    private void SkipBalanced(string open, string close)
    {
        var depth = 1;
        while (Current.Kind != IdlTokenKind.EndOfFile && depth > 0)
        {
            var token = Advance();
            if (token.Is(open))
            {
                depth++;
            }
            else if (token.Is(close))
            {
                depth--;
            }
        }
    }

    private IdlToken Current => _tokens[_index];

    private IdlToken Advance() => _tokens[_index++];

    private bool Match(string symbol)
    {
        if (Current.Kind == IdlTokenKind.Symbol && Current.Is(symbol))
        {
            _index++;
            return true;
        }

        return false;
    }

    private bool MatchKeyword(string keyword)
    {
        if (Current.Kind == IdlTokenKind.Identifier && Current.Is(keyword))
        {
            _index++;
            return true;
        }

        return false;
    }

    private void Expect(string symbol)
    {
        if (!Match(symbol))
        {
            throw new IdlParseException($"'{symbol}' 기대", Current);
        }
    }

    private void ExpectKeyword(string keyword)
    {
        if (!MatchKeyword(keyword))
        {
            throw new IdlParseException($"키워드 '{keyword}' 기대", Current);
        }
    }

    private string ExpectIdentifier()
    {
        if (Current.Kind != IdlTokenKind.Identifier)
        {
            throw new IdlParseException("식별자 기대", Current);
        }

        return Advance().Text;
    }

    private static void MergePartials(IdlModule module)
    {
        var ordered = new List<IdlInterface>();
        var indexByName = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var iface in module.Interfaces)
        {
            if (indexByName.TryGetValue(iface.Name, out var existingIndex))
            {
                var existing = ordered[existingIndex];
                if (existing.IsPartial && !iface.IsPartial)
                {
                    iface.Merge(existing);
                    ordered[existingIndex] = iface;
                }
                else
                {
                    existing.Merge(iface);
                }
            }
            else
            {
                indexByName[iface.Name] = ordered.Count;
                ordered.Add(iface);
            }
        }

        module.Interfaces.Clear();
        module.Interfaces.AddRange(ordered);
    }
}
