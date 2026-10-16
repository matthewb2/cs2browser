namespace CS2Browser.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.MenuStrip menuStrip;
    private System.Windows.Forms.ToolStripMenuItem helpMenu;
    private System.Windows.Forms.ToolStripMenuItem infoMenuItem;
    private System.Windows.Forms.Panel navigationPanel;
    private System.Windows.Forms.Button backButton;
    private System.Windows.Forms.Button forwardButton;
    private System.Windows.Forms.Button goButton;
    private System.Windows.Forms.TextBox addressBar;
    private CS2Browser.UI.BrowserViewControl browserView;
    private System.Windows.Forms.VScrollBar vScrollBar;
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel statusLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.menuStrip = new System.Windows.Forms.MenuStrip();
        this.helpMenu = new System.Windows.Forms.ToolStripMenuItem();
        this.infoMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.navigationPanel = new System.Windows.Forms.Panel();
        this.backButton = new System.Windows.Forms.Button();
        this.forwardButton = new System.Windows.Forms.Button();
        this.goButton = new System.Windows.Forms.Button();
        this.addressBar = new System.Windows.Forms.TextBox();
        this.browserView = new CS2Browser.UI.BrowserViewControl();
        this.vScrollBar = new System.Windows.Forms.VScrollBar();
        this.statusStrip = new System.Windows.Forms.StatusStrip();
        this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
        this.menuStrip.SuspendLayout();
        this.navigationPanel.SuspendLayout();
        this.statusStrip.SuspendLayout();
        this.SuspendLayout();
        // 
        // menuStrip
        // 
        this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.helpMenu});
        this.menuStrip.Location = new System.Drawing.Point(0, 0);
        this.menuStrip.Name = "menuStrip";
        this.menuStrip.Size = new System.Drawing.Size(1024, 24);
        this.menuStrip.TabIndex = 0;
        // 
        // helpMenu
        // 
        this.helpMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.infoMenuItem});
        this.helpMenu.Name = "helpMenu";
        this.helpMenu.Text = "도움말";
        // 
        // infoMenuItem
        // 
        this.infoMenuItem.Name = "infoMenuItem";
        this.infoMenuItem.Text = "정보";
        // 
        // navigationPanel
        // 
        this.navigationPanel.Controls.Add(this.addressBar);
        this.navigationPanel.Controls.Add(this.goButton);
        this.navigationPanel.Controls.Add(this.forwardButton);
        this.navigationPanel.Controls.Add(this.backButton);
        this.navigationPanel.Dock = System.Windows.Forms.DockStyle.Top;
        this.navigationPanel.Location = new System.Drawing.Point(0, 24);
        this.navigationPanel.Name = "navigationPanel";
        this.navigationPanel.Padding = new System.Windows.Forms.Padding(4);
        this.navigationPanel.Size = new System.Drawing.Size(1024, 36);
        this.navigationPanel.TabIndex = 1;
        // 
        // backButton
        // 
        this.backButton.Location = new System.Drawing.Point(6, 6);
        this.backButton.Name = "backButton";
        this.backButton.Size = new System.Drawing.Size(32, 24);
        this.backButton.TabIndex = 0;
        this.backButton.Text = "◀";
        this.backButton.UseVisualStyleBackColor = true;
        // 
        // forwardButton
        // 
        this.forwardButton.Location = new System.Drawing.Point(42, 6);
        this.forwardButton.Name = "forwardButton";
        this.forwardButton.Size = new System.Drawing.Size(32, 24);
        this.forwardButton.TabIndex = 1;
        this.forwardButton.Text = "▶";
        this.forwardButton.UseVisualStyleBackColor = true;
        // 
        // addressBar
        // 
        this.addressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
        this.addressBar.Location = new System.Drawing.Point(80, 7);
        this.addressBar.Name = "addressBar";
        this.addressBar.PlaceholderText = "검색어 또는 주소 입력";
        this.addressBar.Size = new System.Drawing.Size(864, 23);
        this.addressBar.TabIndex = 2;
        // 
        // goButton
        // 
        this.goButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.goButton.Location = new System.Drawing.Point(950, 6);
        this.goButton.Name = "goButton";
        this.goButton.Size = new System.Drawing.Size(68, 24);
        this.goButton.TabIndex = 3;
        this.goButton.Text = "이동";
        this.goButton.UseVisualStyleBackColor = true;
        // 
        // browserView
        // 
        this.browserView.Dock = System.Windows.Forms.DockStyle.Fill;
        this.browserView.Location = new System.Drawing.Point(0, 60);
        this.browserView.Name = "browserView";
        this.browserView.Size = new System.Drawing.Size(1007, 638);
        this.browserView.TabIndex = 3;
        // 
        // vScrollBar
        // 
        this.vScrollBar.Dock = System.Windows.Forms.DockStyle.Right;
        this.vScrollBar.Location = new System.Drawing.Point(1007, 60);
        this.vScrollBar.Name = "vScrollBar";
        this.vScrollBar.Size = new System.Drawing.Size(17, 638);
        this.vScrollBar.TabIndex = 4;
        // 
        // statusStrip
        // 
        this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
        this.statusStrip.Location = new System.Drawing.Point(0, 698);
        this.statusStrip.Name = "statusStrip";
        this.statusStrip.Size = new System.Drawing.Size(1024, 22);
        this.statusStrip.TabIndex = 5;
        // 
        // statusLabel
        // 
        this.statusLabel.Name = "statusLabel";
        this.statusLabel.Text = "준비됨";
        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1024, 720);
        this.Controls.Add(this.browserView);
        this.Controls.Add(this.vScrollBar);
        this.Controls.Add(this.statusStrip);
        this.Controls.Add(this.navigationPanel);
        this.Controls.Add(this.menuStrip);
        this.MainMenuStrip = this.menuStrip;
        this.MinimumSize = new System.Drawing.Size(480, 360);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "CS2Browser";
        this.menuStrip.ResumeLayout(false);
        this.menuStrip.PerformLayout();
        this.navigationPanel.ResumeLayout(false);
        this.navigationPanel.PerformLayout();
        this.statusStrip.ResumeLayout(false);
        this.statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion
}
