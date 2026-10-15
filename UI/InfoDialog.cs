using System.Reflection;

namespace CS2Browser.UI;

/// <summary>
/// 도움말 &gt; 정보 메뉴에서 여는 정보 다이얼로그.
/// 프로그램 이름, 버전, 설명을 보여준다.
/// </summary>
public sealed class InfoDialog : Form
{
    public InfoDialog()
    {
        Text = "정보";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 232);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0";

        var titleLabel = new Label
        {
            Text = "CS2Browser",
            Font = new Font("Malgun Gothic", 16f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 24)
        };

        var versionLabel = new Label
        {
            Text = "버전 " + version,
            AutoSize = true,
            Location = new Point(24, 62)
        };

        var descriptionLabel = new Label
        {
            Text = "AngleSharp 로 HTML/CSS 를 파싱하고 Direct2D/DirectWrite 로 렌더링하는\n" +
                   "경량 웹 브라우저입니다. 이미지 로드, 줄바꿈, 스크롤을 지원합니다.",
            AutoSize = true,
            Location = new Point(24, 92)
        };

        var copyrightLabel = new Label
        {
            Text = "© 2026 CS2Browser Project",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Location = new Point(24, 142)
        };

        var okButton = new Button
        {
            Text = "확인",
            DialogResult = DialogResult.OK,
            Size = new Size(88, 30),
            Location = new Point(ClientSize.Width - 112, ClientSize.Height - 50),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };

        Controls.Add(titleLabel);
        Controls.Add(versionLabel);
        Controls.Add(descriptionLabel);
        Controls.Add(copyrightLabel);
        Controls.Add(okButton);

        AcceptButton = okButton;
        CancelButton = okButton;
    }
}
