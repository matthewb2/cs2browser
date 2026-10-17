using CS2Browser.Engine;
using CS2Browser.Layout;
using CS2Browser.Network;

namespace CS2Browser.Tests.Layout;

[TestClass]
public sealed class InputLayoutHandlerTests
{
    private static async Task<LayoutBox> BuildAsync(string html, float width = 800f)
    {
        using var loader = new ResourceLoader();
        var dom = new DomManager(loader);
        var document = await dom.OpenHtmlAsync(html);
        return new LayoutTreeBuilder().Build(document, width);
    }

    [TestMethod]
    public async Task Build_InputText_CreatesTextInputBox()
    {
        var root = await BuildAsync("<html><body><input type='text' value='hello'></body></html>");

        var input = FindInput(root);
        Assert.IsNotNull(input, "텍스트 입력 박스를 찾지 못했습니다.");
        Assert.IsTrue(input!.IsTextInput);
        Assert.AreEqual("hello", input.InputText);
        Assert.AreEqual(0, input.CaretIndex);
        Assert.IsFalse(input.IsFocused);
    }

    [TestMethod]
    public async Task Build_Textarea_CreatesTextInputBox()
    {
        var root = await BuildAsync("<html><body><textarea>test content</textarea></body></html>");

        var input = FindInput(root);
        Assert.IsNotNull(input, "textarea 박스를 찾지 못했습니다.");
        Assert.IsTrue(input!.IsTextInput);
        Assert.AreEqual("test content", input.InputText);
    }

    private static LayoutBox? FindInput(LayoutBox box)
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
                return found;
            }
        }

        return null;
    }
}
