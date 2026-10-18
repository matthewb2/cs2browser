namespace CS2Browser.IdlGenerator.Model;

/// <summary>여러 IDL 파일을 파싱해 합친 결과. 인터페이스/열거형/딕셔너리/타입정의 모음.</summary>
public sealed class IdlModule
{
    public List<IdlInterface> Interfaces { get; } = new();
    public List<IdlEnum> Enums { get; } = new();
    public List<IdlDictionary> Dictionaries { get; } = new();
    public List<IdlTypedef> Typedefs { get; } = new();

    public IdlInterface? FindInterface(string name)
        => Interfaces.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.Ordinal));
}

/// <summary>Web IDL 인터페이스(또는 partial/mixin).</summary>
public sealed class IdlInterface
{
    public string Name { get; set; } = string.Empty;
    public string? Inheritance { get; set; }
    public bool IsPartial { get; set; }
    public bool IsMixin { get; set; }
    public List<string> ExtendedAttributes { get; } = new();
    public List<IdlAttribute> Attributes { get; } = new();
    public List<IdlOperation> Operations { get; } = new();
    public List<IdlConstant> Constants { get; } = new();

    public void Merge(IdlInterface other)
    {
        Attributes.AddRange(other.Attributes);
        Operations.AddRange(other.Operations);
        Constants.AddRange(other.Constants);
    }
}

public sealed class IdlAttribute
{
    public string Name { get; set; } = string.Empty;
    public IdlType Type { get; set; } = IdlType.Simple("any");
    public bool Readonly { get; set; }
    public bool Static { get; set; }
    public List<string> ExtendedAttributes { get; } = new();
}

public sealed class IdlOperation
{
    public string Name { get; set; } = string.Empty;
    public IdlType ReturnType { get; set; } = IdlType.Simple("void");
    public bool Static { get; set; }
    public List<IdlArgument> Arguments { get; } = new();
    public List<string> ExtendedAttributes { get; } = new();
}

public sealed class IdlArgument
{
    public string Name { get; set; } = string.Empty;
    public IdlType Type { get; set; } = IdlType.Simple("any");
    public bool Optional { get; set; }
    public bool Variadic { get; set; }
    public string? DefaultValue { get; set; }
}

public sealed class IdlConstant
{
    public string Name { get; set; } = string.Empty;
    public IdlType Type { get; set; } = IdlType.Simple("any");
    public string Value { get; set; } = string.Empty;
}

/// <summary>Web IDL 타입. 기본 이름 + null 허용 + 제네릭 인자 + 유니온.</summary>
public sealed class IdlType
{
    /// <summary>기본 타입 이름(예: DOMString, unsigned long, sequence, Element).</summary>
    public string Name { get; set; } = "any";

    public bool Nullable { get; set; }

    /// <summary>sequence/Promise/record 등 제네릭 인자.</summary>
    public List<IdlType> Parameters { get; } = new();

    /// <summary>유니온 멤버(A or B). 비어 있으면 단일 타입.</summary>
    public List<IdlType> Union { get; } = new();

    public bool IsUnion => Union.Count > 0;

    public bool IsVoid => string.Equals(Name, "void", StringComparison.Ordinal);

    public static IdlType Simple(string name) => new() { Name = name };

    public override string ToString()
        => IsUnion ? string.Join(" or ", Union) : Parameters.Count > 0
            ? $"{Name}<{string.Join(", ", Parameters)}>{(Nullable ? "?" : string.Empty)}"
            : Name + (Nullable ? "?" : string.Empty);
}

public sealed class IdlEnum
{
    public string Name { get; set; } = string.Empty;
    public List<string> Values { get; } = new();
}

public sealed class IdlDictionary
{
    public string Name { get; set; } = string.Empty;
    public string? Inheritance { get; set; }
    public List<IdlArgument> Members { get; } = new();
}

public sealed class IdlTypedef
{
    public string Name { get; set; } = string.Empty;
    public IdlType Type { get; set; } = IdlType.Simple("any");
}
