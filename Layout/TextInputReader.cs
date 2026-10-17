namespace CS2Browser.Layout;

/// <summary>
/// 레이아웃 트리 위의 텍스트 입력 상자(&lt;input&gt;/&lt;textarea&gt;)를 다루는 헬퍼.
/// 좌표 → 입력 상자 히트 테스트와, 텍스트 안의 캐럿 위치(문자 인덱스 ↔ X 좌표) 계산을 담당한다.
/// 모든 좌표는 문서 기준(스크롤 반영)이다.
/// </summary>
public static class TextInputReader
{
    private const string DefaultFontFamily = "Malgun Gothic";

    /// <summary>문서 좌표 (x, y) 아래에 있는 텍스트 입력 상자를 찾는다.</summary>
    public static bool TryHitTest(LayoutBox? root, float x, float y, out LayoutBox? input)
    {
        input = null;
        return TryHitTestCore(root, x, y, ref input);
    }

    private static bool TryHitTestCore(LayoutBox? box, float x, float y, ref LayoutBox? found)
    {
        if (box is null)
        {
            return false;
        }

        // 안쪽(자식)이 우선하도록 자식을 먼저 검사한다.
        foreach (var child in box.Children)
        {
            if (TryHitTestCore(child, x, y, ref found))
            {
                return true;
            }
        }

        if (box.IsTextInput && Contains(box, x, y))
        {
            found = box;
            return true;
        }

        return false;
    }

    private static bool Contains(LayoutBox box, float x, float y)
    {
        var left = box.BorderBoxLeft;
        var top = box.BorderBoxTop;
        return x >= left && x <= left + box.BorderBoxWidth
            && y >= top && y <= top + box.BorderBoxHeight;
    }

    /// <summary>입력 상자 안의 X 좌표를 가장 가까운 캐럿 문자 인덱스로 변환한다.</summary>
    public static int CaretIndexFromX(LayoutBox box, float x, ITextMeasurer? measurer)
    {
        var text = box.InputText ?? string.Empty;
        var localX = x - box.X;
        if (localX <= 0f)
        {
            return 0;
        }

        if (localX >= Measure(box, text, measurer))
        {
            return text.Length;
        }

        var closest = 0;
        var minDistance = float.MaxValue;
        for (var i = 0; i <= text.Length; i++)
        {
            var prefixWidth = Measure(box, text[..i], measurer);
            var distance = MathF.Abs(localX - prefixWidth);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = i;
            }
        }

        return closest;
    }

    /// <summary>현재 캐럿 위치(문자 인덱스 + 조합 중 텍스트)의 문서 X 좌표를 반환한다.</summary>
    public static float CaretX(LayoutBox box, ITextMeasurer? measurer)
    {
        var text = box.InputText ?? string.Empty;
        var index = Math.Clamp(box.CaretIndex, 0, text.Length);
        var prefix = text[..index] + (box.CompositionText ?? string.Empty);
        return box.X + Measure(box, prefix, measurer);
    }

    /// <summary>입력 상자에 표시할 전체 문자열(캐럿 앞 텍스트 + 조합 중 텍스트 + 캐럿 뒤 텍스트).</summary>
    public static string DisplayText(LayoutBox box)
    {
        var text = box.InputText ?? string.Empty;
        var index = Math.Clamp(box.CaretIndex, 0, text.Length);
        var composition = box.CompositionText ?? string.Empty;
        return index >= text.Length
            ? text + composition
            : text[..index] + composition + text[index..];
    }

    /// <summary>문자열의 렌더링 폭을 측정한다(측정기가 없으면 근사).</summary>
    internal static float Measure(LayoutBox box, string text, ITextMeasurer? measurer)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        if (measurer is not null)
        {
            return measurer.MeasureWidth(text, box.FontSize, box.FontFamily ?? DefaultFontFamily, false, false);
        }

        var width = 0f;
        foreach (var ch in text)
        {
            width += LayoutTreeBuilder.IsCjkChar(ch) ? box.FontSize : box.FontSize * 0.5f;
        }

        return width;
    }
}
