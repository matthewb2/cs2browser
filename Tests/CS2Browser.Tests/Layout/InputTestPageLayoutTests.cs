using CS2Browser.Engine;
using CS2Browser.Layout;
using CS2Browser.Network;

namespace CS2Browser.Tests;

/// <summary>시작 페이지(Res/input-test.html)의 레이아웃을 검증한다.</summary>
[TestClass]
public sealed class InputTestPageLayoutTests
{
    public TestContext TestContext { get; set; } = null!;

    private static async Task<LayoutBox> BuildAsync(float width = 1024f)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Res", "input-test.html");
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        var document = await dom.OpenAsync(new Uri(path).AbsoluteUri);
        return new LayoutTreeBuilder().Build(document, width);
    }

    private static LayoutBox Body(LayoutBox root) => root.Children.Single(b => b.TagName == "body");

    private static float MarginBoxBottom(LayoutBox box) => box.BorderBoxTop + box.BorderBoxHeight + box.MarginBottom;

    private static float BorderBoxBottom(LayoutBox box) => box.BorderBoxTop + box.BorderBoxHeight;

    [TestMethod]
    public async Task InputTestPage_DumpsLayout()
    {
        var root = await BuildAsync();

        var dump = LayoutDebug.Dump(root);
        TestContext.WriteLine(dump);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "input-test.layout.txt"), dump);

        Assert.IsTrue(root.Children.Count > 0);
    }

    [TestMethod]
    public async Task InputTestPage_FirstHeadingMarginCollapsesWithBody()
    {
        var body = Body(await BuildAsync());
        var h1 = body.Children.First(b => b.TagName == "h1");

        Assert.AreEqual(body.Y, h1.BorderBoxTop, 0.01f);
    }

    [TestMethod]
    public async Task InputTestPage_AdjacentBlockMarginsCollapse()
    {
        var body = Body(await BuildAsync());
        var blocks = body.Children;
        var firstRow = blocks.First(b => b.TagName == "div");
        var index = blocks.IndexOf(firstRow);
        var secondRow = blocks[index + 1];
        var nextHeading = blocks.Skip(index).First(b => b.TagName == "h2");
        var rowBeforeHeading = blocks[blocks.IndexOf(nextHeading) - 1];

        Assert.AreEqual(8f, secondRow.BorderBoxTop - BorderBoxBottom(firstRow), 0.01f);
        Assert.AreEqual(28f, nextHeading.BorderBoxTop - BorderBoxBottom(rowBeforeHeading), 0.01f);
    }

    [TestMethod]
    public async Task InputTestPage_LabelAndInputShareLine()
    {
        var body = Body(await BuildAsync());
        var row = body.Children.First(b => b.TagName == "div");
        var label = row.Lines.Single();
        var input = row.Children.Single(b => b.IsTextInput);

        Assert.IsTrue(input.BorderBoxLeft > label.X + label.Width, "입력 상자가 레이블 오른쪽에 있어야 합니다.");
        Assert.IsTrue(input.BorderBoxTop <= label.Y, "입력 상자와 레이블이 같은 줄에 있어야 합니다.");
        Assert.IsTrue(BorderBoxBottom(input) >= label.Y + label.Height, "입력 상자와 레이블이 같은 줄에 있어야 합니다.");
        Assert.AreEqual(input.BorderBoxHeight, row.Height, 0.01f);
    }

    [TestMethod]
    public async Task InputTestPage_BodyHeightHasNoTrailingGap()
    {
        var root = await BuildAsync();
        var body = Body(root);
        var last = body.Children[^1];

        Assert.AreEqual(BorderBoxBottom(last), body.Y + body.Height, 0.01f);
        Assert.AreEqual(MarginBoxBottom(body), root.Height, 0.01f);
    }
}
