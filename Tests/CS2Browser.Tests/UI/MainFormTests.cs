using CS2Browser.UI;

namespace CS2Browser.Tests;

[TestClass]
public sealed class MainFormTests
{
    [TestMethod]
    public void MainForm_CreatesHandleAndHostsBrowserView()
    {
        StaRunner.Run(() =>
        {
            using var form = new MainForm();
            _ = form.Handle;

            Assert.IsTrue(form.IsHandleCreated);
            Assert.IsTrue(form.Controls.Count > 0);

            var found = form.Controls.Find("browserView", searchAllChildren: true);
            Assert.AreEqual(1, found.Length, "browserView 컨트롤을 찾을 수 없습니다.");

            var browserView = (BrowserViewControl)found[0];
            _ = browserView.Handle;
            Assert.IsTrue(browserView.IsHandleCreated);
        });
    }

    [TestMethod]
    public void MainForm_HasHelpMenuWithInfoItem()
    {
        StaRunner.Run(() =>
        {
            using var form = new MainForm();
            _ = form.Handle;

            Assert.IsNotNull(form.MainMenuStrip, "메뉴스트립이 없습니다.");

            var help = form.MainMenuStrip.Items.Find("helpMenu", searchAllChildren: false);
            Assert.AreEqual(1, help.Length, "도움말 메뉴를 찾을 수 없습니다.");

            var info = ((ToolStripMenuItem)help[0]).DropDownItems.Find("infoMenuItem", searchAllChildren: false);
            Assert.AreEqual(1, info.Length, "정보 메뉴를 찾을 수 없습니다.");
        });
    }

    [TestMethod]
    public void MainForm_HasStatusBarAndScrollBar()
    {
        StaRunner.Run(() =>
        {
            using var form = new MainForm();
            _ = form.Handle;

            var strips = form.Controls.Find("statusStrip", searchAllChildren: true);
            Assert.AreEqual(1, strips.Length, "상태바를 찾을 수 없습니다.");
            var statusStrip = (StatusStrip)strips[0];
            Assert.IsNotNull(statusStrip.Items.Find("statusLabel", false).FirstOrDefault(), "상태 레이블이 없습니다.");

            var bars = form.Controls.Find("vScrollBar", searchAllChildren: true);
            Assert.AreEqual(1, bars.Length, "세로 스크롤바를 찾을 수 없습니다.");
            Assert.AreEqual(DockStyle.Right, ((VScrollBar)bars[0]).Dock);
        });
    }

    [TestMethod]
    public void InfoDialog_ShowsAndCloses()
    {
        StaRunner.Run(() =>
        {
            using var dialog = new InfoDialog();
            _ = dialog.Handle;
            Assert.IsTrue(dialog.IsHandleCreated);
            Assert.AreEqual("정보", dialog.Text);
        });
    }

    [TestMethod]
    public void MainForm_DockedLayout_StacksMenuNavViewStatus()
    {
        StaRunner.Run(() =>
        {
            using var form = new MainForm();
            form.ClientSize = new System.Drawing.Size(1024, 720);
            _ = form.Handle;
            form.PerformLayout();

            var menu = (MenuStrip)form.Controls.Find("menuStrip", searchAllChildren: true)[0];
            var nav = (Panel)form.Controls.Find("navigationPanel", searchAllChildren: true)[0];
            var status = (StatusStrip)form.Controls.Find("statusStrip", searchAllChildren: true)[0];
            var view = (BrowserViewControl)form.Controls.Find("browserView", searchAllChildren: true)[0];
            var bar = (VScrollBar)form.Controls.Find("vScrollBar", searchAllChildren: true)[0];

            Assert.AreEqual(0, menu.Top, "메뉴가 최상단에 있어야 합니다.");
            Assert.AreEqual(menu.Bottom, nav.Top, "네비게이션 바는 메뉴 바로 아래여야 합니다.");
            Assert.AreEqual(form.ClientSize.Height - status.Height, status.Top, "상태바는 최하단이어야 합니다.");
            Assert.IsTrue(view.Top >= nav.Bottom, "뷰는 네비게이션 아래여야 합니다.");
            Assert.IsTrue(view.Bottom <= status.Top, "뷰는 상태바 위여야 합니다.");
            Assert.AreEqual(status.Top, bar.Bottom, "스크롤바는 상태바 위까지여야 합니다.");
            Assert.AreEqual(nav.Bottom, bar.Top, "스크롤바는 네비게이션 아래부터여야 합니다.");
            Assert.AreEqual(view.Right, bar.Left, "스크롤바는 뷰 오른쪽에 붙어야 합니다.");
        });
    }

    [TestMethod]
    public void MainForm_WheelScrollView_MovesScrollBar()
    {
        StaRunner.Run(() =>
        {
            using var form = new MainForm();
            form.ClientSize = new System.Drawing.Size(1024, 500);
            _ = form.Handle;
            form.PerformLayout();

            var view = (BrowserViewControl)form.Controls.Find("browserView", searchAllChildren: true)[0];
            var bar = (VScrollBar)form.Controls.Find("vScrollBar", searchAllChildren: true)[0];
            _ = view.Handle; // 실제 앱처럼 핸들이 생성된 상태를 만든다.

            var body = string.Concat(Enumerable.Repeat("<div>line</div>", 200));
            view.LoadHtmlAsync($"<html><body style='margin:0'>{body}</body></html>")
                .GetAwaiter().GetResult();

            Assert.IsTrue(view.MaxScroll > 0f, "스크롤이 필요해야 합니다.");
            Assert.IsTrue(bar.Enabled, "스크롤바가 활성화되어야 합니다.");
            Assert.AreEqual(0, bar.Value, "초기 스크롤바 값은 0이어야 합니다.");

            // 휠 스크롤 시뮬레이션: 뷰 오프셋을 바꾸면 스크롤바가 따라와야 한다.
            view.SetScrollOffset(view.MaxScroll);
            Assert.IsTrue(bar.Value > 0, $"휠 스크롤 후 스크롤바 값: {bar.Value}");
            Assert.AreEqual((int)Math.Round(view.MaxScroll), bar.Value, 1);
        });
    }
}
