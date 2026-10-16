namespace CS2Browser.UI;

/// <summary>
/// 메인 브라우저 창. 주소창, 네비게이션 버튼, 렌더링 뷰를 배치한다.
/// </summary>
public partial class MainForm : Form
{
    private const string LocalAddress = "about:local";

    // 시작 시 네트워크 없이 바로 파싱/렌더링할 내장 문서.
    private const string DefaultDocument = """
        <html>
        <head>
        <style>
        body {
            margin: 50px;
            padding: 0;
        }
        </style>
        </head>
        <body>

        <div id="container"
             style="display:flex; flex-direction:row;">
            <div id="_one" style="font-size:20px; margin:0px; color:red; background-color:#777799">
                One
            </div>
            <div id="_two" style="font-size:20px; margin:0px; color:green;background-color:#eee">
                Two
            </div>
            <div onmousemove="getPos(event)" id="_three" style="font-size:20px; margin:0px; color:blue; background-color:#777799">
                Three
            </div>
        </div>
        <div>
                  <form name="myform">
          <input type="submit" value="Done"/>
        </form>
        <a>This is a link</a>
        <p>정부가 국민들 신용하락들로 몰아부쳤다 누가 이시장 승리인가 증권사 주식전혀 안하사람 숏치는 사람들 8월부터 절대 좋아지지 않음 9~10월되어야 된다 </p>

        </div>



        <script>
            var el = document.getElementById('_one'); 
            el.style.color = 'yellow';
            var topic = "mouse position";
            console.log(`Fetched data from ${topic}`);
            var el2 = document.querySelectorAll('div');
            function getPos(e){
        	    x=e.clientX;
        	    y=e.clientY;
        	    cursor="Your Mouse Position Is : " + x + " and " + y ;
        	    console.log(cursor);
            }
        </script>

        </body>
        </html>
        """;

    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _syncingScrollBar;

    public MainForm()
    {
        InitializeComponent();

        backButton.Click += async (_, _) => await GoBackAsync();
        forwardButton.Click += async (_, _) => await GoForwardAsync();
        goButton.Click += async (_, _) => await NavigateFromAddressBarAsync();
        addressBar.KeyDown += OnAddressBarKeyDown;
        browserView.TitleChanged += OnTitleChanged;
        browserView.ScrollMetricsChanged += (_, _) => UpdateScrollBar();
        browserView.ScrollOffsetChanged += (_, _) => SyncScrollBarValue();
        browserView.LinkClicked += OnLinkClicked;
        browserView.LinkHovered += OnLinkHovered;
        vScrollBar.Scroll += (_, _) => OnScrollBarScroll();
        infoMenuItem.Click += (_, _) => ShowInfoDialog();
        Load += OnFormLoad;

        UpdateNavigationButtons();
    }

    private async void OnFormLoad(object? sender, EventArgs e)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            var startPagePath = Path.Combine(AppContext.BaseDirectory, "Res", "index.html");
            if (File.Exists(startPagePath))
            {
                await NavigateAsync(new Uri(startPagePath).AbsoluteUri, recordHistory: false);
            }
            else
            {
                // 출력 폴더에 Res 가 없을 때를 대비한 내장 문서 폴백.
                addressBar.Text = LocalAddress;
                statusLabel.Text = "완료: " + LocalAddress;
                await browserView.LoadHtmlAsync(DefaultDocument);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "로드 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private async void OnAddressBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            await NavigateFromAddressBarAsync();
        }
    }

    private async Task NavigateFromAddressBarAsync()
    {
        var text = addressBar.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (!text.Contains("://", StringComparison.Ordinal) && !text.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            text = "https://" + text;
        }

        await NavigateAsync(text);
    }

    /// <summary>주소를 로드한다. recordHistory 가 true 이면 히스토리에 추가한다.</summary>
    public async Task NavigateAsync(string address, bool recordHistory = true)
    {
        addressBar.Text = address;
        if (recordHistory)
        {
            AddHistory(address);
        }

        Cursor = Cursors.WaitCursor;
        try
        {
            await browserView.NavigateAsync(address);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "탐색 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
            statusLabel.Text = "완료: " + address;
            UpdateNavigationButtons();
            UpdateScrollBar();
        }
    }

    private async Task GoBackAsync()
    {
        if (_historyIndex <= 0)
        {
            return;
        }

        _historyIndex--;
        await NavigateAsync(_history[_historyIndex], recordHistory: false);
    }

    private async Task GoForwardAsync()
    {
        if (_historyIndex >= _history.Count - 1)
        {
            return;
        }

        _historyIndex++;
        await NavigateAsync(_history[_historyIndex], recordHistory: false);
    }

    private void AddHistory(string address)
    {
        if (_historyIndex >= 0 && _historyIndex < _history.Count && _history[_historyIndex] == address)
        {
            return;
        }

        if (_historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }

        _history.Add(address);
        _historyIndex = _history.Count - 1;
    }

    private void UpdateNavigationButtons()
    {
        backButton.Enabled = _historyIndex > 0;
        forwardButton.Enabled = _historyIndex >= 0 && _historyIndex < _history.Count - 1;
    }

    private void ShowInfoDialog()
    {
        using var dialog = new InfoDialog();
        dialog.ShowDialog(this);
    }

    /// <summary>문서/뷰포트 크기에 맞춰 세로 스크롤바를 갱신한다.</summary>
    private void UpdateScrollBar()
    {
        var viewport = Math.Max(1, browserView.ClientSize.Height);
        var documentHeight = (int)Math.Ceiling(browserView.DocumentHeight);
        var maxScroll = Math.Max(0, documentHeight - viewport);

        _syncingScrollBar = true;
        try
        {
            vScrollBar.LargeChange = viewport;
            vScrollBar.SmallChange = 40;
            // VScrollBar 는 Maximum - LargeChange + 1 이 실제 최대값이므로 보정한다.
            vScrollBar.Maximum = maxScroll + vScrollBar.LargeChange - 1;
            vScrollBar.Value = (int)Math.Clamp(browserView.ScrollOffsetY, 0, maxScroll);
            vScrollBar.Enabled = maxScroll > 0;
        }
        finally
        {
            _syncingScrollBar = false;
        }
    }

    /// <summary>스크롤바 조작(휠/드래그)이 아닌, 뷰의 오프셋 변경을 스크롤바에 반영한다.</summary>
    private void SyncScrollBarValue()
    {
        var maxScroll = Math.Max(0, vScrollBar.Maximum - vScrollBar.LargeChange + 1);
        var value = (int)Math.Clamp(browserView.ScrollOffsetY, 0, maxScroll);

        _syncingScrollBar = true;
        try
        {
            vScrollBar.Value = value;
        }
        finally
        {
            _syncingScrollBar = false;
        }
    }

    private void OnScrollBarScroll()
    {
        if (_syncingScrollBar)
        {
            return;
        }

        browserView.SetScrollOffset(vScrollBar.Value);
    }

    private void OnTitleChanged(object? sender, string title)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnTitleChanged(sender, title)));
            return;
        }

        Text = string.IsNullOrWhiteSpace(title) ? "CS2Browser" : title + " - CS2Browser";
    }

    private async void OnLinkClicked(object? sender, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        await NavigateAsync(url);
    }

    private void OnLinkHovered(object? sender, string url)
        => statusLabel.Text = string.IsNullOrEmpty(url) ? "준비됨" : url;
}
