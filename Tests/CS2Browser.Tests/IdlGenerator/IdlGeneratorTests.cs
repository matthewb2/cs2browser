using CS2Browser.IdlGenerator.Generation;
using CS2Browser.IdlGenerator.Model;
using CS2Browser.IdlGenerator.Parser;

namespace CS2Browser.Tests.IdlGenerator;

/// <summary>IDL 파서/타입 매핑/코드 생성기에 대한 단위 테스트.</summary>
[TestClass]
public class IdlParserTests
{
    [TestMethod]
    public void Lexer_SkipsCommentsAndTokenizesKeywords()
    {
        var tokens = new IdlLexer("/* c */ interface Node { }; // trailing").Tokenize();

        Assert.AreEqual(IdlTokenKind.Identifier, tokens[0].Kind);
        Assert.AreEqual("interface", tokens[0].Text);
        Assert.AreEqual("Node", tokens[1].Text);
        Assert.AreEqual("{", tokens[2].Text);
        Assert.AreEqual("}", tokens[3].Text);
        Assert.AreEqual(IdlTokenKind.EndOfFile, tokens[^1].Kind);
    }

    [TestMethod]
    public void Parser_RecordsInheritanceAndAttributeReadonly()
    {
        var module = IdlParser.Parse(
            """
            interface Node : EventTarget {
              readonly attribute unsigned short nodeType;
              attribute DOMString? nodeValue;
            };
            """);

        var node = module.FindInterface("Node");
        Assert.IsNotNull(node);
        Assert.AreEqual("EventTarget", node!.Inheritance);
        Assert.AreEqual(2, node.Attributes.Count);
        Assert.IsTrue(node.Attributes[0].Readonly);
        Assert.IsFalse(node.Attributes[1].Readonly);
    }

    [TestMethod]
    public void Parser_MergesPartialInterfaces()
    {
        var module = IdlParser.Parse(
            """
            interface Element {
              attribute DOMString id;
            };
            """,
            """
            partial interface Element {
              DOMString? getAttribute(DOMString name);
            };
            """);

        var element = module.FindInterface("Element");
        Assert.IsNotNull(element);
        Assert.AreEqual(1, element!.Attributes.Count);
        Assert.AreEqual(1, element.Operations.Count);
        Assert.AreEqual("getAttribute", element.Operations[0].Name);
    }

    [TestMethod]
    public void Parser_ParsesConstantsAndOperations()
    {
        var module = IdlParser.Parse(
            """
            interface Node {
              const unsigned short ELEMENT_NODE = 1;
              Node? appendChild(Node node);
              boolean contains(Node? other);
            };
            """);

        var node = module.FindInterface("Node");
        Assert.IsNotNull(node);
        Assert.AreEqual(1, node!.Constants.Count);
        Assert.AreEqual("ELEMENT_NODE", node.Constants[0].Name);
        Assert.AreEqual(2, node.Operations.Count);
        Assert.AreEqual(1, node.Operations[0].Arguments.Count);
        Assert.IsFalse(node.Operations[1].Arguments[0].Optional);
    }

    [TestMethod]
    public void Parser_ReadonlyAttributeWithStaticKeywordDoesNotThrow()
    {
        var module = IdlParser.Parse(
            """
            interface HTMLElement {
              static readonly attribute DOMString tagName;
            };
            """);

        var html = module.FindInterface("HTMLElement");
        Assert.IsNotNull(html);
        Assert.AreEqual(1, html!.Attributes.Count);
        Assert.IsTrue(html.Attributes[0].Static);
        Assert.IsTrue(html.Attributes[0].Readonly);
    }
}

[TestClass]
public class TypeMapperTests
{
    [TestMethod]
    public void Map_ConvertsPrimitiveIdlTypes()
    {
        Assert.AreEqual("string", TypeMapper.Map(IdlType.Simple("DOMString")));
        Assert.AreEqual("bool", TypeMapper.Map(IdlType.Simple("boolean")));
        Assert.AreEqual("ushort", TypeMapper.Map(IdlType.Simple("unsigned short")));
        Assert.AreEqual("int", TypeMapper.Map(IdlType.Simple("long")));
        Assert.AreEqual("object?", TypeMapper.Map(IdlType.Simple("any")));
    }

    [TestMethod]
    public void Map_AddsNullableSuffixForNullableValueTypes()
    {
        Assert.AreEqual("int?", TypeMapper.Map(new IdlType { Name = "long", Nullable = true }));
        Assert.AreEqual("ushort?", TypeMapper.Map(new IdlType { Name = "unsigned short", Nullable = true }));
        Assert.AreEqual("string", TypeMapper.Map(new IdlType { Name = "DOMString", Nullable = true }));
    }

    [TestMethod]
    public void Map_HandlesGenericAndUnionTypes()
    {
        var sequence = new IdlType { Name = "sequence" };
        sequence.Parameters.Add(IdlType.Simple("long"));
        Assert.AreEqual("IReadOnlyList<int>", TypeMapper.Map(sequence));

        var promise = new IdlType { Name = "Promise" };
        promise.Parameters.Add(IdlType.Simple("DOMString"));
        Assert.AreEqual("Task<string>", TypeMapper.Map(promise));

        var union = new IdlType();
        union.Union.Add(IdlType.Simple("Node"));
        union.Union.Add(IdlType.Simple("DOMString"));
        Assert.AreEqual("object?", TypeMapper.Map(union));
    }

    [TestMethod]
    public void IsDomNodeType_DetectsWrappableInterfaces()
    {
        Assert.IsTrue(TypeMapper.IsDomNodeType(IdlType.Simple("Node")));
        Assert.IsTrue(TypeMapper.IsDomNodeType(IdlType.Simple("Element")));
        Assert.IsFalse(TypeMapper.IsDomNodeType(IdlType.Simple("DOMString")));
        Assert.IsFalse(TypeMapper.IsDomNodeType(IdlType.Simple("boolean")));
        Assert.IsFalse(TypeMapper.IsDomNodeType(IdlType.Simple("long")));
        Assert.IsFalse(TypeMapper.IsDomNodeType(IdlType.Simple("any")));
    }
}

[TestClass]
public class NameMapperTests
{
    [TestMethod]
    public void ToPascal_UppercasesFirstLetterAndStripsHyphens()
    {
        Assert.AreEqual("InnerHTML", NameMapper.ToPascal("innerHTML"));
        Assert.AreEqual("Node", NameMapper.ToPascal("node"));
        Assert.AreEqual("ClassList", NameMapper.ToPascal("class-list"));
    }

    [TestMethod]
    public void Escape_PrefixesAtForCSharpKeywords()
    {
        Assert.AreEqual("@class", NameMapper.Escape("class"));
        Assert.AreEqual("@event", NameMapper.Escape("event"));
        Assert.AreEqual("node", NameMapper.Escape("node"));
    }
}

[TestClass]
public class CSharpEmitterTests
{
    private static string Emit(string idl, string interfaceName = "Node")
    {
        var module = IdlParser.Parse(idl);
        var iface = module.FindInterface(interfaceName)
            ?? throw new InvalidOperationException($"'{interfaceName}' 인터페이스를 찾을 수 없습니다.");
        return new CSharpEmitter(module).EmitInterface(iface);
    }

    [TestMethod]
    public void Emit_UsesNullableDirectiveAndNoDefaultScriptAccess()
    {
        var source = Emit("interface Node { readonly attribute unsigned short nodeType; };");

        StringAssert.Contains(source, "#nullable enable");
        StringAssert.Contains(source, "[NoDefaultScriptAccess]");
        StringAssert.Contains(source, "internal class NodeBridge");
    }

    [TestMethod]
    public void Emit_ExposesCamelCaseIdlNameOnScriptMemberAttribute()
    {
        var source = Emit("interface Node { readonly attribute unsigned short nodeType; };");

        StringAssert.Contains(source, "[ScriptMember(\"nodeType\")]");
        Assert.IsFalse(source.Contains("ScriptableMember", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_UsesStaticBindingForKnownMembers()
    {
        var source = Emit(
            """
            interface Node {
              readonly attribute unsigned short nodeType;
              attribute Node? parentNode;
            };
            """);

        StringAssert.Contains(source, "((AngleSharp.Dom.INode)DomTarget).NodeType");
        StringAssert.Contains(source, "Wrap(((AngleSharp.Dom.INode)DomTarget).Parent)");
    }

    [TestMethod]
    public void Emit_FallsBackToRuntimeDispatcherForUnknownMembers()
    {
        var source = Emit("interface Node { object? cloneNode(optional boolean deep); };");

        StringAssert.Contains(source, "public object? CloneNode(object? deep = null)");
        StringAssert.Contains(source, "=> Call(\"cloneNode\", deep);");
    }

    [TestMethod]
    public void Emit_OmitsSetterForReadonlyAttributeEvenWhenWritableBindingExists()
    {
        // Element.id 는 StaticBindingMap 에 setter 가 정의되어 있으나,
        // IDL 에서 readonly 로 선언되었으므로 setter 는 생성되지 않아야 한다.
        var source = Emit(
            """
            interface Element {
              readonly attribute DOMString id;
            };
            """,
            interfaceName: "Element");

        var block = source[source.IndexOf("[ScriptMember(\"id\")", StringComparison.Ordinal)..];
        Assert.IsTrue(block.Contains("public string Id", StringComparison.Ordinal));
        Assert.IsFalse(block.Contains("set =>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_GeneratesOneFilePerNonMixinInterface()
    {
        var module = IdlParser.Parse(
            """
            interface Node {};
            interface mixin Host {};
            interface mixin NodeHost {};
            """);

        var files = new CSharpEmitter(module).EmitAll();

        Assert.AreEqual(1, files.Count);
        Assert.IsTrue(files.ContainsKey("Node.g.cs"));
    }
}
