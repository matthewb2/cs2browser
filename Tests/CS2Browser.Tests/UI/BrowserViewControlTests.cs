using CS2Browser.UI;

namespace CS2Browser.Tests;

[TestClass]
public sealed class BrowserViewControlTests
{
    [TestMethod]
    public void BrowserViewControl_ScrollsTallDocument()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            var body = string.Concat(Enumerable.Repeat("<div>line</div>", 100));
            view.LoadHtmlAsync($"<html><body style='margin:0'>{body}</body></html>")
                .GetAwaiter().GetResult();

            Assert.IsTrue(view.DocumentHeight > 200f, $"문서 높이: {view.DocumentHeight}");
            Assert.IsTrue(view.MaxScroll > 0f, "스크롤 가능해야 합니다.");

            // 아래로 스크롤하면 최대치로 클램프된다.
            view.SetScrollOffset(1_000_000f);
            Assert.AreEqual(view.MaxScroll, view.ScrollOffsetY, 0.5f);

            // 음수는 0으로 클램프된다.
            view.SetScrollOffset(-50f);
            Assert.AreEqual(0f, view.ScrollOffsetY, 0.01f);
        });
    }

    [TestMethod]
    public void BrowserViewControl_SetScrollOffset_RaisesScrollOffsetChanged()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            var body = string.Concat(Enumerable.Repeat("<div>line</div>", 100));
            view.LoadHtmlAsync($"<html><body style='margin:0'>{body}</body></html>")
                .GetAwaiter().GetResult();

            var raised = 0;
            view.ScrollOffsetChanged += (_, _) => raised++;

            view.SetScrollOffset(120f);
            Assert.AreEqual(1, raised, "스크롤 오프셋 변경 이벤트가 발생해야 합니다.");

            // 실제로 바뀌지 않는 값은 이벤트를 발생시키지 않는다.
            view.SetScrollOffset(120f);
            Assert.AreEqual(1, raised);
        });
    }

    [TestMethod]
    public void BrowserViewControl_TryGetLinkAt_FindsLinkText()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            view.LoadHtmlAsync("<html><body style='margin:0'><a href='target.html'>Link</a></body></html>")
                .GetAwaiter().GetResult();

            // 링크 텍스트 첫 글자 부근.
            Assert.IsTrue(view.TryGetLinkAt(new Point(2, 5), out var link),
                "링크 위 좌표에서 링크를 찾지 못했습니다.");
            StringAssert.Contains(link!.Url, "target.html");

            // 링크가 없는 빈 영역.
            Assert.IsFalse(view.TryGetLinkAt(new Point(390, 190), out _),
                "링크가 없는 영역에서 링크를 찾았습니다.");
        });
    }

    [TestMethod]
    public void BrowserViewControl_TryGetLinkAt_RespectsScrollOffset()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 120) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            // 링크가 문서 상단에 있고, 스크롤로 아래로 밀어낸다.
            var spacer = string.Concat(Enumerable.Repeat("<div>line</div>", 100));
            view.LoadHtmlAsync(
                $"<html><body style='margin:0'>{spacer}<a href='deep.html'>Deep</a></body></html>")
                .GetAwaiter().GetResult();

            Assert.IsTrue(view.MaxScroll > 0f, "스크롤이 가능해야 합니다.");

            // 스크롤 전: 링크는 화면 밖(아래)에 있다.
            Assert.IsFalse(view.TryGetLinkAt(new Point(2, 5), out _));

            // 위로 스크롤하면 링크가 화면 안으로 들어온다.
            view.SetScrollOffset(view.MaxScroll);
            Assert.IsTrue(view.TryGetLinkAt(new Point(2, 105), out var link));
            StringAssert.Contains(link!.Url, "deep.html");
        });
    }

    [TestMethod]
    public void BrowserViewControl_TryGetTextOffsetAt_ReturnsOffsetOverText()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            view.LoadHtmlAsync("<html><body style='margin:0'><p style='margin:0'>Hello</p></body></html>")
                .GetAwaiter().GetResult();

            Assert.IsTrue(view.TryGetTextOffsetAt(new Point(1, 5), out var offset),
                "텍스트 위 좌표에서 오프셋을 찾지 못했습니다.");
            Assert.AreEqual(0, offset);
        });
    }

    [TestMethod]
    public void BrowserViewControl_SelectionAndCopy_NotAvailableWithoutSelection()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            view.LoadHtmlAsync("<html><body style='margin:0'><p style='margin:0'>Hello</p></body></html>")
                .GetAwaiter().GetResult();

            Assert.IsFalse(view.HasSelection);
            Assert.AreEqual(string.Empty, view.SelectedText);
            Assert.IsFalse(view.CopySelectionToClipboard());
        });
    }

    [TestMethod]
    public void BrowserViewControl_DragSelectsText_AndCopyWorks()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            view.LoadHtmlAsync(
                "<html><body style='margin:0'><p style='margin:0'>Hello World</p></body></html>")
                .GetAwaiter().GetResult();

            // 드래그: 텍스트 왼쪽에서 오른쪽으로.
            RaiseMouseDown(view, new Point(1, 5));
            RaiseMouseMove(view, new Point(60, 5), MouseButtons.Left);
            RaiseMouseUp(view, new Point(60, 5));

            Assert.IsTrue(view.HasSelection, "드래그 후 선택이 있어야 합니다.");
            Assert.IsTrue(view.SelectedText.StartsWith("Hello", StringComparison.Ordinal),
                $"선택 텍스트: '{view.SelectedText}'");

            Assert.IsTrue(view.CopySelectionToClipboard(), "클립보드 복사에 실패했습니다.");
            Assert.AreEqual(view.SelectedText, Clipboard.GetText());

            // ClearSelection 후에는 선택이 비어야 한다.
            view.ClearSelection();
            Assert.IsFalse(view.HasSelection);
        });
    }

    [TestMethod]
    public void BrowserViewControl_DragOverLink_DoesNotNavigate()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            view.LoadHtmlAsync(
                "<html><body style='margin:0'><p style='margin:0'><a href='target.html'>LinkText</a></p></body></html>")
                .GetAwaiter().GetResult();

            var navigated = false;
            view.LinkClicked += (_, _) => navigated = true;

            // 링크 텍스트 위에서 드래그하면 이동이 아니라 선택이 되어야 한다.
            RaiseMouseDown(view, new Point(1, 5));
            RaiseMouseMove(view, new Point(40, 5), MouseButtons.Left);
            RaiseMouseUp(view, new Point(40, 5));

            Assert.IsFalse(navigated, "드래그 중에는 링크로 이동하면 안 됩니다.");
            Assert.IsTrue(view.HasSelection);
        });
    }

    [TestMethod]
    public void BrowserViewControl_ClickOnLink_Navigates()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            view.LoadHtmlAsync(
                "<html><body style='margin:0'><p style='margin:0'><a href='target.html'>LinkText</a></p></body></html>")
                .GetAwaiter().GetResult();

            string? navigated = null;
            view.LinkClicked += (_, url) => navigated = url;

            // 이동 없는 단순 클릭은 링크 이동을 트리거해야 한다.
            RaiseMouseDown(view, new Point(1, 5));
            RaiseMouseUp(view, new Point(1, 5));

            Assert.IsNotNull(navigated);
            StringAssert.Contains(navigated!, "target.html");
        });
    }

    [TestMethod]
    public void BrowserViewControl_ClickInput_FocusesAndTypingInserts()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            view.LoadHtmlAsync("<html><body style='margin:0'><input type='text' value='hi'></body></html>")
                .GetAwaiter().GetResult();

            Assert.IsTrue(view.TryGetInputAt(new Point(10, 10), out var input));
            Assert.IsNotNull(input);

            // 입력 상자 클릭 → 포커스 + 캐럿 표시.
            RaiseMouseDown(view, new Point(10, 10));
            Assert.IsTrue(input!.IsFocused, "클릭한 입력 상자가 포커스되어야 합니다.");
            Assert.IsTrue(input.IsCaretVisible, "포커스 시 캐럿이 보여야 합니다.");

            // 끝으로 이동 후 타이핑하면 값이 갱신된다.
            RaiseKeyDown(view, Keys.End);
            RaiseKeyPress(view, 'X');
            Assert.AreEqual("hiX", input.InputText);
            Assert.AreEqual(3, input.CaretIndex);

            // 백스페이스로 한 글자 삭제.
            RaiseKeyDown(view, Keys.Back);
            Assert.AreEqual("hi", input.InputText);
        });
    }

    [TestMethod]
    public void BrowserViewControl_ClickInput_BlursOnOutsideClick()
    {
        StaRunner.Run(() =>
        {
            using var form = new Form { ClientSize = new System.Drawing.Size(400, 200) };
            var view = new BrowserViewControl { Dock = DockStyle.Fill };
            form.Controls.Add(view);
            _ = form.Handle;
            _ = view.Handle;

            view.LoadHtmlAsync(
                "<html><body style='margin:0'><input type='text' value='hi'><p style='margin:0'>text</p></body></html>")
                .GetAwaiter().GetResult();

            RaiseMouseDown(view, new Point(10, 10));
            Assert.IsTrue(view.TryGetInputAt(new Point(10, 10), out var input));
            Assert.IsTrue(input!.IsFocused);

            // 입력 상자 바깥(아래 문단)을 클릭하면 포커스가 해제된다.
            RaiseMouseDown(view, new Point(5, (int)(input.BorderBoxTop + input.BorderBoxHeight + 20f)));
            Assert.IsFalse(input.IsFocused, "바깥 클릭 시 포커스가 해제되어야 합니다.");
            Assert.IsFalse(input.IsCaretVisible);
        });
    }

    private static void RaiseMouseDown(BrowserViewControl view, Point point)
        => InvokeProtected(view, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));

    private static void RaiseMouseMove(BrowserViewControl view, Point point, MouseButtons buttons)
        => InvokeProtected(view, "OnMouseMove", new MouseEventArgs(buttons, 0, point.X, point.Y, 0));

    private static void RaiseMouseUp(BrowserViewControl view, Point point)
        => InvokeProtected(view, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));

    private static void RaiseKeyDown(BrowserViewControl view, Keys key)
        => InvokeProtected(view, "OnKeyDown", new KeyEventArgs(key));

    private static void RaiseKeyPress(BrowserViewControl view, char ch)
        => InvokeProtected(view, "OnKeyPress", new KeyPressEventArgs(ch));

    private static void InvokeProtected(BrowserViewControl view, string method, object arg)
    {
        var info = typeof(BrowserViewControl).GetMethod(
            method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        info!.Invoke(view, new[] { arg });
    }
}
