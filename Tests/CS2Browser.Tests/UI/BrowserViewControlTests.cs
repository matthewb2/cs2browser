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
}
