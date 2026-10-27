using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CS2Browser.Layout;

/// <summary>레이아웃 트리를 사람이 읽을 수 있는 텍스트로 덤프한다(디버깅용).</summary>
public static class LayoutDebug
{
    private const int MaxTextPreview = 48;

    public static string Dump(LayoutBox root)
    {
        var builder = new StringBuilder();
        AppendBox(builder, root, 0);
        return builder.ToString();
    }

    /// <summary>디버그 빌드에서만 레이아웃 트리를 디버그 출력 창에 쓴다.</summary>
    [Conditional("DEBUG")]
    public static void WriteToDebug(LayoutBox root)
    {
        foreach (var line in Dump(root).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Debug.WriteLine(line.TrimEnd('\r'), "Layout");
        }
    }

    private static void AppendBox(StringBuilder builder, LayoutBox box, int depth)
    {
        var indent = new string(' ', depth * 2);
        builder.Append(indent)
            .Append('<').Append(box.TagName ?? "?").Append('>')
            .Append(" content=").Append(Rect(box.X, box.Y, box.Width, box.Height))
            .Append(" border=").Append(Rect(box.BorderBoxLeft, box.BorderBoxTop, box.BorderBoxWidth, box.BorderBoxHeight))
            .Append(" m=").Append(Edges(box.MarginTop, box.MarginRight, box.MarginBottom, box.MarginLeft))
            .Append(" b=").Append(Edges(box.BorderTop, box.BorderRight, box.BorderBottom, box.BorderLeft))
            .Append(" p=").Append(Edges(box.PaddingTop, box.PaddingRight, box.PaddingBottom, box.PaddingLeft));

        if (box.IsTextInput)
        {
            builder.Append(" input=\"").Append(Preview(box.InputText)).Append('"');
        }

        builder.Append('\n');

        foreach (var line in box.Lines)
        {
            var text = string.Join(' ', line.Runs.Select(r => r.Text));
            builder.Append(indent).Append("  | line ")
                .Append(Rect(line.X, line.Y, line.Width, line.Height))
                .Append(" \"").Append(Preview(text)).Append("\"\n");
        }

        foreach (var child in box.Children)
        {
            AppendBox(builder, child, depth + 1);
        }
    }

    private static string Rect(float x, float y, float width, float height)
        => string.Create(CultureInfo.InvariantCulture, $"({x:0.#},{y:0.#} {width:0.#}x{height:0.#})");

    private static string Edges(float top, float right, float bottom, float left)
        => string.Create(CultureInfo.InvariantCulture, $"({top:0.#},{right:0.#},{bottom:0.#},{left:0.#})");

    private static string Preview(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var flat = text.Replace("\r", "\\r").Replace("\n", "\\n");
        return flat.Length <= MaxTextPreview ? flat : flat[..MaxTextPreview] + "...";
    }
}
