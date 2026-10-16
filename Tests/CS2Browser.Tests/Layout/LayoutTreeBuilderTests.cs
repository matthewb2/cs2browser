using CS2Browser.Engine;
using CS2Browser.Layout;
using CS2Browser.Network;
using CS2Browser.Rendering;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using DwFactoryType = Vortice.DirectWrite.FactoryType;

namespace CS2Browser.Tests;

[TestClass]
public sealed class LayoutTreeBuilderTests
{
    private static async Task<LayoutBox> BuildAsync(string html, float width = 800f)
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        var document = await dom.OpenHtmlAsync(html);
        return new LayoutTreeBuilder().Build(document, width);
    }

    private sealed class FixedWidthMeasurer : ITextMeasurer
    {
        public float PerChar { get; init; }

        public float MeasureWidth(string text, float fontSize, string fontFamily, bool bold, bool italic)
            => text.Length * PerChar;
    }

    [TestMethod]
    public async Task Build_ViewportSize_MatchesWidth()
    {
        var root = await BuildAsync("<html><body>x</body></html>", 640f);

        Assert.AreEqual(640f, root.Width, 0.01f);
        Assert.AreEqual("body", root.Children[0].TagName);
    }

    [TestMethod]
    public async Task Build_AppliesBodyMargin()
    {
        var root = await BuildAsync("<html><body style='margin:25px'><div>A</div></body></html>");

        var body = root.Children[0];
        Assert.AreEqual(25f, body.X, 0.01f);
        Assert.AreEqual(25f, body.Y, 0.01f);
        Assert.AreEqual(25f, body.Children[0].X, 0.01f);
        Assert.AreEqual(25f, body.Children[0].Y, 0.01f);
    }

    [TestMethod]
    public async Task Build_ParsesBackgroundColor()
    {
        var root = await BuildAsync("<html><body><div style='background-color:#ff8800'>A</div></body></html>");

        var div = root.Children[0].Children[0];
        Assert.IsNotNull(div.BackgroundColor);
        ColorAssert.AreEqual(new Color4(1f, 136f / 255f, 0f, 1f), div.BackgroundColor!.Value);
    }

    [TestMethod]
    public async Task Build_EmitsTextRuns()
    {
        var root = await BuildAsync("<html><body><div>Hello</div></body></html>");

        var div = root.Children[0].Children[0];
        Assert.IsTrue(div.Lines.Count >= 1);
        Assert.AreEqual("Hello", div.Lines[0].Runs[0].Text);
    }

    [TestMethod]
    public async Task Build_FlexRow_PlacesChildrenHorizontally()
    {
        var root = await BuildAsync(TestDocuments.Flex);

        var flex = root.Children[0].Children[0];
        Assert.AreEqual(3, flex.Children.Count);
        Assert.IsTrue(flex.Children[1].X > flex.Children[0].X);
        Assert.IsTrue(flex.Children[2].X > flex.Children[1].X);
        Assert.AreEqual(flex.Children[0].Y, flex.Children[1].Y, 0.01f);
    }

    [TestMethod]
    public async Task Build_LongText_WrapsToMultipleLines()
    {
        var words = string.Join(' ', Enumerable.Repeat("word", 60));
        var root = await BuildAsync(
            $"<html><body><p style='font-size:16px'>{words}</p></body></html>",
            160f);

        var paragraph = root.Children[0].Children[0];
        Assert.IsTrue(paragraph.Lines.Count > 1,
            $"줄 수가 1 이하입니다: {paragraph.Lines.Count}");
    }

    [TestMethod]
    public async Task Build_BrElement_ForcesLineBreak()
    {
        var root = await BuildAsync("<html><body><div>Line1<br>Line2</div></body></html>");

        var div = root.Children[0].Children[0];
        Assert.AreEqual(2, div.Lines.Count, $"줄 수가 2가 아닙니다: {div.Lines.Count}");
        Assert.AreEqual("Line1", div.Lines[0].Runs[0].Text);
        Assert.AreEqual("Line2", div.Lines[1].Runs[0].Text);
        Assert.IsTrue(div.Lines[1].Y > div.Lines[0].Y, "두 번째 줄이 아래에 있어야 합니다.");
    }

    [TestMethod]
    public async Task Build_NewlineInText_ForcesLineBreak()
    {
        // 개행은 공백이 아니라 줄바꿈으로 처리되어야 한다.
        var root = await BuildAsync("<html><body><div>alpha\nbeta</div></body></html>");

        var div = root.Children[0].Children[0];
        Assert.AreEqual(2, div.Lines.Count, $"줄 수가 2가 아닙니다: {div.Lines.Count}");
        Assert.AreEqual("alpha", div.Lines[0].Runs[0].Text);
        Assert.AreEqual("beta", div.Lines[1].Runs[0].Text);
        Assert.AreEqual(div.Lines[0].X, div.Lines[1].X, 0.01f);
    }

    [TestMethod]
    public async Task Build_ConsecutiveNewlines_PreserveBlankLine()
    {
        var root = await BuildAsync("<html><body><div>a\n\nb</div></body></html>");

        var div = root.Children[0].Children[0];
        Assert.AreEqual(2, div.Lines.Count, $"줄 수가 2가 아닙니다: {div.Lines.Count}");
        // 빈 줄 하나만큼 간격이 벌어져야 한다.
        Assert.IsTrue(div.Lines[1].Y - div.Lines[0].Y > div.Lines[0].Height * 1.5f,
            "연속 개행이 빈 줄로 반영되지 않았습니다.");
    }

    [TestMethod]
    public async Task Build_LeadingAndTrailingNewlines_AreTrimmed()
    {
        // 블록 선두/말미의 공백 개행은 빈 줄을 만들지 않아야 한다.
        var root = await BuildAsync("<html><body><div>\n  only\n  </div></body></html>");

        var div = root.Children[0].Children[0];
        Assert.AreEqual(1, div.Lines.Count, $"줄 수가 1이 아닙니다: {div.Lines.Count}");
        Assert.AreEqual(div.Y, div.Lines[0].Y, 0.01f);
        Assert.AreEqual("only", div.Lines[0].Runs[0].Text);
    }

    [TestMethod]
    public async Task Build_KoreanText_WrapsPerCharacter()
    {
        // 공백 없는 한글은 글자 단위로 줄바꿈되어야 한다. (16px * 10글자 = 160px)
        var text = string.Concat(Enumerable.Repeat("안녕하세요", 20));
        var root = await BuildAsync(
            $"<html><body style='margin:0'><p style='font-size:16px;margin:0'>{text}</p></body></html>",
            160f);

        var paragraph = root.Children[0].Children[0];
        Assert.IsTrue(paragraph.Lines.Count > 1,
            $"줄 수가 1 이하입니다: {paragraph.Lines.Count}");

        // 한글은 글자 단위로 나뉘므로 각 런은 한 글자여야 한다.
        Assert.IsTrue(
            paragraph.Lines.All(line => line.Runs.All(run => run.Text.Length == 1)),
            "한글이 글자 단위로 줄바꿈되지 않았습니다.");

        // 160px 폭에 16px 글자이므로 줄마다 정확히 10글자가 들어가야 한다.
        Assert.IsTrue(
            paragraph.Lines.All(line => line.Runs.Count == 10),
            string.Join(", ", paragraph.Lines.Select(l => l.Runs.Count)));
    }

    [TestMethod]
    public async Task Build_LatinText_DoesNotBreakMidWord()
    {
        // 라틴 단어는 글자 단위로 쪼개지지 않아야 한다.
        var root = await BuildAsync(
            "<html><body><p style='font-size:16px'>internationalization</p></body></html>",
            60f);

        var paragraph = root.Children[0].Children[0];
        Assert.AreEqual(1, paragraph.Lines.Count);
        Assert.AreEqual("internationalization", paragraph.Lines[0].Runs[0].Text);
    }

    [TestMethod]
    public async Task Build_SpacesBetweenWords_ArePreserved()
    {
        var root = await BuildAsync("<html><body><h1>Wisp Browser</h1></body></html>", 800f);

        var h1 = root.Children[0].Children[0];
        var runs = h1.Lines[0].Runs;
        var dump = string.Join(", ", runs.Select(r => $"'{r.Text}'@{r.X:0.#}"));
        Assert.AreEqual(2, runs.Count, $"런 수가 2가 아닙니다: {dump}");

        var gap = runs[1].X - (runs[0].X + runs[0].Width);
        Assert.IsTrue(gap > 0.5f, $"단어 사이 간격이 없습니다: {dump}");
    }

    [TestMethod]
    public async Task Build_UsesMeasurer_ForRunWidthsAndPositions()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        var document = await dom.OpenHtmlAsync(
            "<html><body style='margin:0'><p style='font-size:20px;margin:0'>Wisp Browser</p></body></html>");

        var builder = new LayoutTreeBuilder { TextMeasurer = new FixedWidthMeasurer { PerChar = 30f } };
        var root = builder.Build(document, 800f);

        var p = root.Children[0].Children[0];
        var runs = p.Lines[0].Runs;

        Assert.AreEqual(2, runs.Count);
        // "Wisp" = 4글자 * 30 = 120 (근사치 4*0.5*20=40 과 달라야 한다).
        Assert.AreEqual(120f, runs[0].Width, 0.01f, "측정기가 런 폭에 반영되지 않았습니다.");
        // 다음 런은 이전 런 폭 + 측정된 공백 폭(30) 뒤에서 시작해 겹치지 않아야 한다.
        Assert.IsTrue(runs[1].X >= runs[0].X + runs[0].Width, "단어가 겹칩니다.");
    }

    [TestMethod]
    public async Task Build_WithDirectWriteMeasurer_PreservesWordSpace()
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        var document = await dom.OpenHtmlAsync(
            "<html><body style='margin:0'><h1 style='font-size:44.8px;margin:0'>Wisp Browser</h1></body></html>");

        using var factory = DWrite.DWriteCreateFactory<IDWriteFactory>(DwFactoryType.Shared);
        using var measurer = new DirectWriteTextRenderer(factory);
        var root = new LayoutTreeBuilder { TextMeasurer = measurer }.Build(document, 1024f);

        var h1 = root.Children[0].Children[0];
        var runs = h1.Lines[0].Runs;

        Assert.AreEqual(2, runs.Count, "단어가 두 개의 런으로 나뉘어야 합니다.");

        // 실제 글꼴 메트릭 기준으로 이전 단어 뒤에 공백 간격이 남아야 한다.
        var gap = runs[1].X - (runs[0].X + runs[0].Width);
        Assert.IsTrue(gap > 3f, $"단어 사이 간격이 너무 작습니다: {gap}");

        // 근사치(4 * 0.5 * 44.8 = 89.6)보다 큰 실제 측정 폭이어야 한다.
        Assert.IsTrue(runs[0].Width > 4f * 0.5f * 44.8f, "DirectWrite 측정기가 사용되지 않았습니다.");
    }

    [TestMethod]
    public async Task Build_AnchorText_SetsLinkUrlOnRuns()
    {
        var root = await BuildAsync(
            "<html><body><p style='margin:0'><a href='page.html'>Link</a></p></body></html>");

        var p = root.Children[0].Children[0];
        var run = p.Lines[0].Runs[0];

        Assert.IsNotNull(run.LinkUrl, "앵커 텍스트 런에 LinkUrl 이 설정되지 않았습니다.");
        StringAssert.Contains(run.LinkUrl!, "page.html");
    }

    [TestMethod]
    public async Task Build_NonAnchor_HasNoLinkUrl()
    {
        var root = await BuildAsync("<html><body><p style='margin:0'>plain</p></body></html>");

        var p = root.Children[0].Children[0];
        Assert.IsNull(p.Lines[0].Runs[0].LinkUrl);
        Assert.IsNull(p.LinkUrl);
    }

    [TestMethod]
    public async Task Build_AnchorWrappingImage_SetsLinkUrlOnImageBox()
    {
        var root = await BuildAsync(
            "<html><body style='margin:0'><a href='target.html'><img src='missing.png'></a></body></html>");

        var image = FindFirstImage(root);
        Assert.IsNotNull(image, "이미지 박스를 찾지 못했습니다.");
        Assert.IsNotNull(image!.LinkUrl, "앵커로 감싼 이미지에 LinkUrl 이 없습니다.");
        StringAssert.Contains(image.LinkUrl!, "target.html");
    }

    private static LayoutBox? FindFirstImage(LayoutBox box)
    {
        if (!string.IsNullOrEmpty(box.ImageSource))
        {
            return box;
        }

        foreach (var child in box.Children)
        {
            var found = FindFirstImage(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
