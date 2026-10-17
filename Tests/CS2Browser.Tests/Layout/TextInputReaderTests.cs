using CS2Browser.Engine;
using CS2Browser.Layout;
using CS2Browser.Network;

namespace CS2Browser.Tests.Layout;

[TestClass]
public sealed class TextInputReaderTests
{
    private static async Task<LayoutBox> BuildAsync(string html, float width = 800f)
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        var document = await dom.OpenHtmlAsync(html);
        return new LayoutTreeBuilder().Build(document, width);
    }

    private static LayoutBox FindInput(LayoutBox box)
    {
        if (box.IsTextInput)
        {
            return box;
        }

        foreach (var child in box.Children)
        {
            var found = FindInput(child);
            if (found is not null)
            {
                return found!;
            }
        }

        return null!;
    }

    [TestMethod]
    public async Task TryHitTest_InsideInput_ReturnsInput()
    {
        var root = await BuildAsync("<html><body style='margin:0'><input type='text' value='hello'></body></html>");
        var input = FindInput(root);
        Assert.IsNotNull(input);

        var insideX = input!.BorderBoxLeft + 2f;
        var insideY = input.BorderBoxTop + input.BorderBoxHeight / 2f;
        Assert.IsTrue(TextInputReader.TryHitTest(root, insideX, insideY, out var hit));
        Assert.AreSame(input, hit);
    }

    [TestMethod]
    public async Task TryHitTest_OutsideInput_ReturnsFalse()
    {
        var root = await BuildAsync("<html><body style='margin:0'><input type='text' value='hello'></body></html>");
        var input = FindInput(root);

        // 입력 상자 왼쪽 바깥.
        Assert.IsFalse(TextInputReader.TryHitTest(root, input!.BorderBoxLeft - 50f, input.BorderBoxTop + 2f, out _));
    }

    [TestMethod]
    public async Task CaretIndexFromX_ClampsToBounds()
    {
        var root = await BuildAsync("<html><body style='margin:0'><input type='text' value='hello'></body></html>");
        var input = FindInput(root);
        var text = input!.InputText ?? string.Empty;

        // 왼쪽 끝 → 0, 오른쪽 멀리 → 텍스트 끝.
        Assert.AreEqual(0, TextInputReader.CaretIndexFromX(input, input.X - 100f, null));
        Assert.AreEqual(text.Length, TextInputReader.CaretIndexFromX(input, input.X + 10_000f, null));
    }

    [TestMethod]
    public async Task CaretX_AtZero_EqualsContentLeft()
    {
        var root = await BuildAsync("<html><body style='margin:0'><input type='text' value='hello'></body></html>");
        var input = FindInput(root);

        input!.CaretIndex = 0;
        Assert.AreEqual(input.X, TextInputReader.CaretX(input, null), 0.01f);
    }

    [TestMethod]
    public async Task DisplayText_InsertsCompositionAtCaret()
    {
        var root = await BuildAsync("<html><body style='margin:0'><input type='text' value='hello'></body></html>");
        var input = FindInput(root);

        input!.CaretIndex = 2;
        input.CompositionText = "X";
        Assert.AreEqual("heXllo", TextInputReader.DisplayText(input));
    }
}
