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
}
