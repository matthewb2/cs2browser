using CS2Browser.Engine;
using CS2Browser.Layout;
using CS2Browser.Network;

namespace CS2Browser.Tests;

[TestClass]
public sealed class TextSelectionTests
{
    private static async Task<LayoutBox> BuildAsync(string html, float width = 800f, ITextMeasurer? measurer = null)
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        var document = await dom.OpenHtmlAsync(html);
        return new LayoutTreeBuilder { TextMeasurer = measurer }.Build(document, width);
    }

    private sealed class FixedWidthMeasurer : ITextMeasurer
    {
        public float PerChar { get; init; } = 10f;

        public float MeasureWidth(string text, float fontSize, string fontFamily, bool bold, bool italic)
            => text.Length * PerChar;
    }

    [TestMethod]
    public async Task Build_InlineBoxes_ExposeBufferOffsets()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello World</p></body></html>");

        var p = root.Children[0].Children[0];
        var runs = p.Lines[0].Runs;

        Assert.IsNotNull(root.TextBuffer, "루트에 공유 텍스트 버퍼가 설정되어야 합니다.");
        Assert.IsTrue(runs.Count >= 2);

        foreach (var run in runs)
        {
            Assert.AreEqual(
                run.Text,
                root.TextBuffer!.Substring(run.TextStart, run.TextLength),
                "런 오프셋이 버퍼 텍스트와 일치하지 않습니다.");
        }
    }

    [TestMethod]
    public void Selection_NormalizesStartAndEnd_RegardlessOfDragDirection()
    {
        var forward = new TextSelection(2, 7);
        Assert.AreEqual(2, forward.Start);
        Assert.AreEqual(7, forward.End);
        Assert.IsFalse(forward.IsEmpty);

        var backward = new TextSelection(7, 2);
        Assert.AreEqual(2, backward.Start);
        Assert.AreEqual(7, backward.End);
    }

    [TestMethod]
    public void Selection_EmptyWhenAnchorEqualsFocus()
    {
        var selection = new TextSelection(4, 4);
        Assert.IsTrue(selection.IsEmpty);
        Assert.IsTrue(selection.Start == selection.End);
    }

    [TestMethod]
    public async Task HitTest_AtRunStartAndEnd_ReturnsRunBoundaries()
    {
        var measurer = new FixedWidthMeasurer { PerChar = 10f };
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hi</p></body></html>",
            800f, measurer);

        var p = root.Children[0].Children[0];
        var run = p.Lines[0].Runs[0];
        var lineY = p.Lines[0].Y + p.Lines[0].Height * 0.5f;

        Assert.IsTrue(TextSelectionReader.TryHitTest(root, run.X, lineY, measurer, out var atStart));
        Assert.AreEqual(run.TextStart, atStart);

        Assert.IsTrue(TextSelectionReader.TryHitTest(root, run.X + run.Width, lineY, measurer, out var atEnd));
        Assert.AreEqual(run.TextEnd, atEnd);
    }

    [TestMethod]
    public async Task HitTest_WithinRun_UsesPerCharacterPrecision()
    {
        var measurer = new FixedWidthMeasurer { PerChar = 10f };
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello</p></body></html>",
            800f, measurer);

        var p = root.Children[0].Children[0];
        var run = p.Lines[0].Runs[0];
        var lineY = p.Lines[0].Y + p.Lines[0].Height * 0.5f;

        // x = 25 이상 30 미만이면 앞의 두 글자("He") 뒤로 매핑된다.
        Assert.IsTrue(TextSelectionReader.TryHitTest(root, run.X + 25f, lineY, measurer, out var offset));
        Assert.AreEqual(run.TextStart + 2, offset);
    }

    [TestMethod]
    public async Task GetSelectedText_SingleWord()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello World</p></body></html>");

        var p = root.Children[0].Children[0];
        var hello = p.Lines[0].Runs[0];

        var text = TextSelectionReader.GetSelectedText(
            root, new TextSelection(hello.TextStart, hello.TextEnd));

        Assert.AreEqual("Hello", text);
    }

    [TestMethod]
    public async Task GetSelectedText_AcrossRuns_PreservesSkippedSpace()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello World</p></body></html>");

        var p = root.Children[0].Children[0];
        var runs = p.Lines[0].Runs;

        var text = TextSelectionReader.GetSelectedText(
            root, new TextSelection(runs[0].TextStart, runs[^1].TextEnd));

        Assert.AreEqual("Hello World", text);
    }

    [TestMethod]
    public async Task GetSelectedText_AcrossForcedLineBreak_InsertsNewline()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><div style='margin:0'>alpha<br>beta</div></body></html>");

        var div = root.Children[0].Children[0];
        Assert.AreEqual(2, div.Lines.Count);

        var text = TextSelectionReader.GetSelectedText(root, new TextSelection(2, 8));

        Assert.AreEqual("pha\nbe", text);
    }

    [TestMethod]
    public async Task GetSelectedText_PartialRuns()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello World</p></body></html>");

        var text = TextSelectionReader.GetSelectedText(root, new TextSelection(2, 8));
        Assert.AreEqual("llo Wo", text);
    }

    [TestMethod]
    public async Task GetSelectedText_EmptySelection_ReturnsEmpty()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello</p></body></html>");

        Assert.AreEqual(string.Empty, TextSelectionReader.GetSelectedText(root, new TextSelection(3, 3)));
        Assert.AreEqual(string.Empty, TextSelectionReader.GetSelectedText(root, null));
    }

    [TestMethod]
    public async Task GetLineHighlights_SpanningWords_FillsSpaceBetweenThem()
    {
        var measurer = new FixedWidthMeasurer { PerChar = 10f };
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello World</p></body></html>",
            800f, measurer);

        var line = root.Children[0].Children[0].Lines[0];
        var highlights = TextSelectionReader.GetLineHighlights(line, new TextSelection(0, 11), measurer);

        Assert.AreEqual(1, highlights.Count, "두 단어와 사이 공백이 하나의 구간으로 병합되어야 합니다.");
        Assert.AreEqual(line.X, highlights[0].X, 0.5f);
        // "Hello World" = 11글자 * 10px.
        Assert.AreEqual(110f, highlights[0].Width, 1f, "사이 공백이 하이라이트에 포함되지 않았습니다.");
    }

    [TestMethod]
    public async Task GetLineHighlights_TrailingSpaceExcluded_WhenSelectionEndsAtWord()
    {
        var measurer = new FixedWidthMeasurer { PerChar = 10f };
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello World</p></body></html>",
            800f, measurer);

        var line = root.Children[0].Children[0].Lines[0];

        // "Hello" 만 선택(공백 제외).
        var highlights = TextSelectionReader.GetLineHighlights(line, new TextSelection(0, 5), measurer);
        Assert.AreEqual(1, highlights.Count);
        Assert.AreEqual(50f, highlights[0].Width, 1f, "선택 끝의 공백은 제외되어야 합니다.");
    }

    [TestMethod]
    public async Task GetLineHighlights_IncludesTrailingSpace_WhenSelected()
    {
        var measurer = new FixedWidthMeasurer { PerChar = 10f };
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello World</p></body></html>",
            800f, measurer);

        var line = root.Children[0].Children[0].Lines[0];

        // "Hello " 선택(공백 포함).
        var highlights = TextSelectionReader.GetLineHighlights(line, new TextSelection(0, 6), measurer);
        Assert.AreEqual(1, highlights.Count);
        Assert.AreEqual(60f, highlights[0].Width, 1f, "선택된 공백이 하이라이트에 포함되어야 합니다.");
    }

    [TestMethod]
    public async Task GetRunSegments_FullySelected_SingleSelectedSegment()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello</p></body></html>");

        var run = root.Children[0].Children[0].Lines[0].Runs[0];
        var segments = TextSelectionReader.GetRunSegments(
            run, new TextSelection(run.TextStart, run.TextEnd));

        Assert.AreEqual(1, segments.Count);
        Assert.IsTrue(segments[0].Selected);
        Assert.AreEqual(0, segments[0].Offset);
        Assert.AreEqual(5, segments[0].Length);
    }

    [TestMethod]
    public async Task GetRunSegments_PartialSelection_SplitsIntoThree()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello</p></body></html>");

        var run = root.Children[0].Children[0].Lines[0].Runs[0];

        // 가운데 글자("ell")만 선택.
        var segments = TextSelectionReader.GetRunSegments(
            run, new TextSelection(run.TextStart + 1, run.TextStart + 4));

        Assert.AreEqual(3, segments.Count);
        Assert.IsFalse(segments[0].Selected);
        Assert.AreEqual(1, segments[0].Length);
        Assert.IsTrue(segments[1].Selected);
        Assert.AreEqual(1, segments[1].Offset);
        Assert.AreEqual(3, segments[1].Length);
        Assert.IsFalse(segments[2].Selected);
        Assert.AreEqual(4, segments[2].Offset);
        Assert.AreEqual(1, segments[2].Length);
    }

    [TestMethod]
    public async Task GetRunSegments_NoSelection_ReturnsWholeRunUnselected()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello</p></body></html>");

        var run = root.Children[0].Children[0].Lines[0].Runs[0];

        var segments = TextSelectionReader.GetRunSegments(run, null);
        Assert.AreEqual(1, segments.Count);
        Assert.IsFalse(segments[0].Selected);
        Assert.AreEqual(5, segments[0].Length);

        var empty = TextSelectionReader.GetRunSegments(run, new TextSelection(2, 2));
        Assert.AreEqual(1, empty.Count);
        Assert.IsFalse(empty[0].Selected);
    }

    [TestMethod]
    public async Task GetRunSegments_SelectionLeadingEdge_OnlySelectedPlusTrailing()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><p style='margin:0'>Hello</p></body></html>");

        var run = root.Children[0].Children[0].Lines[0].Runs[0];

        // 앞 두 글자("He")만 선택.
        var segments = TextSelectionReader.GetRunSegments(
            run, new TextSelection(run.TextStart, run.TextStart + 2));

        Assert.AreEqual(2, segments.Count);
        Assert.IsTrue(segments[0].Selected);
        Assert.AreEqual(0, segments[0].Offset);
        Assert.AreEqual(2, segments[0].Length);
        Assert.IsFalse(segments[1].Selected);
        Assert.AreEqual(2, segments[1].Offset);
        Assert.AreEqual(3, segments[1].Length);
    }
}
