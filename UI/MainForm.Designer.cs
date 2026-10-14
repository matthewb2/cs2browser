namespace CS2Browser.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.Panel navigationPanel;
    private System.Windows.Forms.Button backButton;
    private System.Windows.Forms.Button forwardButton;
    private System.Windows.Forms.Button goButton;
    private System.Windows.Forms.TextBox addressBar;
    private BrowserViewControl browserView;

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
        this.navigationPanel = new System.Windows.Forms.Panel();
        this.backButton = new System.Windows.Forms.Button();
        this.forwardButton = new System.Windows.Forms.Button();
        this.goButton = new System.Windows.Forms.Button();
        this.addressBar = new System.Windows.Forms.TextBox();
        this.browserView = new CS2Browser.UI.BrowserViewControl();
        this.navigationPanel.SuspendLayout();
        this.SuspendLayout();
        // 
        // navigationPanel
        // 
        this.navigationPanel.Controls.Add(this.addressBar);
        this.navigationPanel.Controls.Add(this.goButton);
        this.navigationPanel.Controls.Add(this.forwardButton);
        this.navigationPanel.Controls.Add(this.backButton);
        this.navigationPanel.Dock = System.Windows.Forms.DockStyle.Top;
        this.navigationPanel.Location = new System.Drawing.Point(0, 0);
        this.navigationPanel.Name = "navigationPanel";
        this.navigationPanel.Padding = new System.Windows.Forms.Padding(4);
        this.navigationPanel.Size = new System.Drawing.Size(1024, 36);
        this.navigationPanel.TabIndex = 0;
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
        this.browserView.Location = new System.Drawing.Point(0, 36);
        this.browserView.Name = "browserView";
        this.browserView.Size = new System.Drawing.Size(1024, 684);
        this.browserView.TabIndex = 1;
        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1024, 720);
        this.Controls.Add(this.browserView);
        this.Controls.Add(this.navigationPanel);
        this.MinimumSize = new System.Drawing.Size(480, 360);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "CS2Browser";
        this.navigationPanel.ResumeLayout(false);
        this.navigationPanel.PerformLayout();
        this.ResumeLayout(false);
    }

    #endregion
}
