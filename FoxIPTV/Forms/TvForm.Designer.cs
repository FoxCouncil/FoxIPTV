namespace FoxIPTV.Forms
{
    partial class TvForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TvForm));
            this.channelStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.guideStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.vlcControl = new Vlc.DotNet.Forms.VlcControl();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.channelStatusProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.guideStatusProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.playerStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.ccStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.ccOptionsDropDownButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.bufferStatusProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.timeStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.channelLabel = new System.Windows.Forms.Label();
            this.channelNameLabel = new System.Windows.Forms.Label();
            this._timer = new System.Windows.Forms.Timer(this.components);
            this.muteLabel = new System.Windows.Forms.Label();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItemChannelNumber = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemChannelName = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemChannelUp = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemChannelDown = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemWindowState = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemMute = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStatusBar = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemClosedCaptioning = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemBorders = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemFullscreen = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemAlwaysOnTop = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparencyMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency0 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency2 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency3 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency4 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency5 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency6 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency7 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency8 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemTransparency9 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStereoMode = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStereoModeSurround = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStereoModeOriginal = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStereoModeHeadphones = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStereoModeStereo = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStereoModeReverse = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStereoModeLeft = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStereoModeRight = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemStereoModeMono = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemGuide = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemChannelEditor = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemQuit = new System.Windows.Forms.ToolStripMenuItem();
            this.pictureBoxAudioRate = new System.Windows.Forms.PictureBox();
            this.pictureBoxAudioType = new System.Windows.Forms.PictureBox();
            this.pictureBoxAudioCodec = new System.Windows.Forms.PictureBox();
            this.pictureBoxFramerate = new System.Windows.Forms.PictureBox();
            this.pictureBoxVideoSize = new System.Windows.Forms.PictureBox();
            this.pictureBoxVideoCodec = new System.Windows.Forms.PictureBox();
            this.pictureBoxClosedCaptioning = new System.Windows.Forms.PictureBox();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.labelStatus = new System.Windows.Forms.Label();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            ((System.ComponentModel.ISupportInitialize)(this.vlcControl)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxAudioRate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxAudioType)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxAudioCodec)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxFramerate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxVideoSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxVideoCodec)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxClosedCaptioning)).BeginInit();
            this.SuspendLayout();
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(175, 6);
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(175, 6);
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new System.Drawing.Size(175, 6);
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new System.Drawing.Size(175, 6);
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new System.Drawing.Size(175, 6);
            // 
            // channelStatusLabel
            // 
            this.channelStatusLabel.Name = "channelStatusLabel";
            this.channelStatusLabel.Size = new System.Drawing.Size(59, 19);
            this.channelStatusLabel.Text = "Channels:";
            // 
            // guideStatusLabel
            // 
            this.guideStatusLabel.Name = "guideStatusLabel";
            this.guideStatusLabel.Size = new System.Drawing.Size(41, 19);
            this.guideStatusLabel.Text = "Guide:";
            // 
            // vlcControl
            // 
            this.vlcControl.BackColor = System.Drawing.Color.Black;
            this.vlcControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.vlcControl.Location = new System.Drawing.Point(0, 0);
            this.vlcControl.Name = "vlcControl";
            this.vlcControl.Size = new System.Drawing.Size(1008, 513);
            this.vlcControl.Spu = -1;
            this.vlcControl.TabIndex = 0;
            this.vlcControl.Text = "videoOutput";
            this.vlcControl.VlcLibDirectory = null;
            this.vlcControl.VlcMediaplayerOptions = new string[] {
        "--gain=1.8",
        "--quiet"};
            this.vlcControl.VlcLibDirectoryNeeded += new System.EventHandler<Vlc.DotNet.Forms.VlcLibDirectoryNeededEventArgs>(this.VlcControl_VlcLibDirectoryNeeded);
            this.vlcControl.Buffering += new System.EventHandler<Vlc.DotNet.Core.VlcMediaPlayerBufferingEventArgs>(this.VlcControl_Buffering);
            this.vlcControl.EncounteredError += new System.EventHandler<Vlc.DotNet.Core.VlcMediaPlayerEncounteredErrorEventArgs>(this.VlcControl_EncounteredError);
            this.vlcControl.EndReached += new System.EventHandler<Vlc.DotNet.Core.VlcMediaPlayerEndReachedEventArgs>(this.VlcControl_EndReached);
            this.vlcControl.Paused += new System.EventHandler<Vlc.DotNet.Core.VlcMediaPlayerPausedEventArgs>(this.VlcControl_Paused);
            this.vlcControl.Playing += new System.EventHandler<Vlc.DotNet.Core.VlcMediaPlayerPlayingEventArgs>(this.VlcControl_Playing);
            this.vlcControl.TimeChanged += new System.EventHandler<Vlc.DotNet.Core.VlcMediaPlayerTimeChangedEventArgs>(this.VlcControl_TimeChanged);
            this.vlcControl.Stopped += new System.EventHandler<Vlc.DotNet.Core.VlcMediaPlayerStoppedEventArgs>(this.VlcControl_Stopped);
            // 
            // statusStrip
            // 
            this.statusStrip.BackColor = System.Drawing.SystemColors.Control;
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.channelStatusLabel,
            this.channelStatusProgressBar,
            this.guideStatusLabel,
            this.guideStatusProgressBar,
            this.playerStatusLabel,
            this.ccStatusLabel,
            this.ccOptionsDropDownButton,
            this.bufferStatusProgressBar,
            this.timeStatusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 513);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1008, 24);
            this.statusStrip.TabIndex = 1;
            this.statusStrip.Text = "statusStrip1";
            // 
            // channelStatusProgressBar
            // 
            this.channelStatusProgressBar.Name = "channelStatusProgressBar";
            this.channelStatusProgressBar.Size = new System.Drawing.Size(100, 18);
            // 
            // guideStatusProgressBar
            // 
            this.guideStatusProgressBar.Name = "guideStatusProgressBar";
            this.guideStatusProgressBar.Size = new System.Drawing.Size(100, 18);
            // 
            // playerStatusLabel
            // 
            this.playerStatusLabel.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.playerStatusLabel.ForeColor = System.Drawing.Color.SeaGreen;
            this.playerStatusLabel.Name = "playerStatusLabel";
            this.playerStatusLabel.Size = new System.Drawing.Size(530, 19);
            this.playerStatusLabel.Spring = true;
            this.playerStatusLabel.Text = "Waiting";
            // 
            // ccStatusLabel
            // 
            this.ccStatusLabel.ForeColor = System.Drawing.Color.Gainsboro;
            this.ccStatusLabel.Name = "ccStatusLabel";
            this.ccStatusLabel.Size = new System.Drawing.Size(23, 19);
            this.ccStatusLabel.Text = "CC";
            // 
            // ccOptionsDropDownButton
            // 
            this.ccOptionsDropDownButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.ccOptionsDropDownButton.Image = ((System.Drawing.Image)(resources.GetObject("ccOptionsDropDownButton.Image")));
            this.ccOptionsDropDownButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ccOptionsDropDownButton.Name = "ccOptionsDropDownButton";
            this.ccOptionsDropDownButton.Size = new System.Drawing.Size(13, 22);
            this.ccOptionsDropDownButton.Visible = false;
            this.ccOptionsDropDownButton.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.ccOptionsDropDownButton_DropDownItemClicked);
            // 
            // bufferStatusProgressBar
            // 
            this.bufferStatusProgressBar.Name = "bufferStatusProgressBar";
            this.bufferStatusProgressBar.Size = new System.Drawing.Size(100, 18);
            this.bufferStatusProgressBar.ToolTipText = "Buffer";
            // 
            // timeStatusLabel
            // 
            this.timeStatusLabel.Name = "timeStatusLabel";
            this.timeStatusLabel.Size = new System.Drawing.Size(34, 19);
            this.timeStatusLabel.Text = "00:00";
            // 
            // channelLabel
            // 
            this.channelLabel.AutoSize = true;
            this.channelLabel.BackColor = System.Drawing.Color.Black;
            this.channelLabel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.channelLabel.Font = new System.Drawing.Font("Arial", 36F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.channelLabel.ForeColor = System.Drawing.Color.Lime;
            this.channelLabel.Location = new System.Drawing.Point(9, 12);
            this.channelLabel.Margin = new System.Windows.Forms.Padding(0);
            this.channelLabel.Name = "channelLabel";
            this.channelLabel.Size = new System.Drawing.Size(0, 56);
            this.channelLabel.TabIndex = 2;
            // 
            // channelNameLabel
            // 
            this.channelNameLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.channelNameLabel.AutoSize = true;
            this.channelNameLabel.BackColor = System.Drawing.Color.Black;
            this.channelNameLabel.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.channelNameLabel.ForeColor = System.Drawing.Color.Lime;
            this.channelNameLabel.Location = new System.Drawing.Point(9, 92);
            this.channelNameLabel.Margin = new System.Windows.Forms.Padding(0);
            this.channelNameLabel.Name = "channelNameLabel";
            this.channelNameLabel.Size = new System.Drawing.Size(0, 19);
            this.channelNameLabel.TabIndex = 3;
            this.channelNameLabel.UseMnemonic = false;
            // 
            // _timer
            // 
            this._timer.Enabled = true;
            this._timer.Tick += new System.EventHandler(this.Timer_Tick);
            // 
            // muteLabel
            // 
            this.muteLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.muteLabel.AutoSize = true;
            this.muteLabel.BackColor = System.Drawing.Color.Transparent;
            this.muteLabel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.muteLabel.Font = new System.Drawing.Font("Arial", 21.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.muteLabel.ForeColor = System.Drawing.Color.Lime;
            this.muteLabel.Location = new System.Drawing.Point(901, 12);
            this.muteLabel.Margin = new System.Windows.Forms.Padding(0);
            this.muteLabel.Name = "muteLabel";
            this.muteLabel.Size = new System.Drawing.Size(98, 34);
            this.muteLabel.TabIndex = 4;
            this.muteLabel.Text = "MUTE";
            this.muteLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.muteLabel.Visible = false;
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemChannelNumber,
            this.toolStripMenuItemChannelName,
            toolStripSeparator1,
            this.toolStripMenuItemChannelUp,
            this.toolStripMenuItemChannelDown,
            toolStripSeparator2,
            this.toolStripMenuItemWindowState,
            this.toolStripMenuItemMute,
            toolStripSeparator4,
            this.toolStripMenuItemStatusBar,
            this.toolStripMenuItemClosedCaptioning,
            this.toolStripMenuItemBorders,
            this.toolStripMenuItemFullscreen,
            this.toolStripMenuItemAlwaysOnTop,
            toolStripSeparator3,
            this.toolStripMenuItemTransparencyMenu,
            this.toolStripMenuItemStereoMode,
            this.toolStripSeparator6,
            this.toolStripMenuItemGuide,
            this.toolStripMenuItemChannelEditor,
            toolStripSeparator5,
            this.toolStripMenuItemAbout,
            this.toolStripMenuItemQuit});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
            this.contextMenuStrip.Size = new System.Drawing.Size(179, 414);
            this.contextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip_Opening);
            // 
            // toolStripMenuItemChannelNumber
            // 
            this.toolStripMenuItemChannelNumber.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripMenuItemChannelNumber.Enabled = false;
            this.toolStripMenuItemChannelNumber.Name = "toolStripMenuItemChannelNumber";
            this.toolStripMenuItemChannelNumber.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemChannelNumber.Text = "<ChannelNumber>";
            // 
            // toolStripMenuItemChannelName
            // 
            this.toolStripMenuItemChannelName.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripMenuItemChannelName.Enabled = false;
            this.toolStripMenuItemChannelName.Name = "toolStripMenuItemChannelName";
            this.toolStripMenuItemChannelName.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemChannelName.Text = "<ChannelName>";
            // 
            // toolStripMenuItemChannelUp
            // 
            this.toolStripMenuItemChannelUp.Image = global::FoxIPTV.Properties.Resources.arrow_090;
            this.toolStripMenuItemChannelUp.Name = "toolStripMenuItemChannelUp";
            this.toolStripMenuItemChannelUp.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemChannelUp.Text = "Channel Up";
            this.toolStripMenuItemChannelUp.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            // 
            // toolStripMenuItemChannelDown
            // 
            this.toolStripMenuItemChannelDown.Image = global::FoxIPTV.Properties.Resources.arrow_270;
            this.toolStripMenuItemChannelDown.Name = "toolStripMenuItemChannelDown";
            this.toolStripMenuItemChannelDown.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemChannelDown.Text = "Channel Down";
            // 
            // toolStripMenuItemWindowState
            // 
            this.toolStripMenuItemWindowState.Name = "toolStripMenuItemWindowState";
            this.toolStripMenuItemWindowState.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemWindowState.Text = "Show Window";
            // 
            // toolStripMenuItemMute
            // 
            this.toolStripMenuItemMute.Name = "toolStripMenuItemMute";
            this.toolStripMenuItemMute.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemMute.Text = "&Mute";
            // 
            // toolStripMenuItemStatusBar
            // 
            this.toolStripMenuItemStatusBar.Name = "toolStripMenuItemStatusBar";
            this.toolStripMenuItemStatusBar.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemStatusBar.Text = "&Status Bar";
            // 
            // toolStripMenuItemClosedCaptioning
            // 
            this.toolStripMenuItemClosedCaptioning.Name = "toolStripMenuItemClosedCaptioning";
            this.toolStripMenuItemClosedCaptioning.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemClosedCaptioning.Text = "&Closed Captioning";
            // 
            // toolStripMenuItemBorders
            // 
            this.toolStripMenuItemBorders.Name = "toolStripMenuItemBorders";
            this.toolStripMenuItemBorders.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemBorders.Text = "Borders";
            // 
            // toolStripMenuItemFullscreen
            // 
            this.toolStripMenuItemFullscreen.Name = "toolStripMenuItemFullscreen";
            this.toolStripMenuItemFullscreen.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemFullscreen.Text = "&Fullscreen";
            // 
            // toolStripMenuItemAlwaysOnTop
            // 
            this.toolStripMenuItemAlwaysOnTop.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.toolStripMenuItemAlwaysOnTop.Name = "toolStripMenuItemAlwaysOnTop";
            this.toolStripMenuItemAlwaysOnTop.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemAlwaysOnTop.Text = "Always On Top";
            // 
            // toolStripMenuItemTransparencyMenu
            // 
            this.toolStripMenuItemTransparencyMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemTransparency0,
            this.toolStripMenuItemTransparency1,
            this.toolStripMenuItemTransparency2,
            this.toolStripMenuItemTransparency3,
            this.toolStripMenuItemTransparency4,
            this.toolStripMenuItemTransparency5,
            this.toolStripMenuItemTransparency6,
            this.toolStripMenuItemTransparency7,
            this.toolStripMenuItemTransparency8,
            this.toolStripMenuItemTransparency9});
            this.toolStripMenuItemTransparencyMenu.Name = "toolStripMenuItemTransparencyMenu";
            this.toolStripMenuItemTransparencyMenu.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemTransparencyMenu.Text = "Transparency";
            // 
            // toolStripMenuItemTransparency0
            // 
            this.toolStripMenuItemTransparency0.Checked = true;
            this.toolStripMenuItemTransparency0.CheckState = System.Windows.Forms.CheckState.Checked;
            this.toolStripMenuItemTransparency0.Name = "toolStripMenuItemTransparency0";
            this.toolStripMenuItemTransparency0.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency0.Tag = "1.0";
            this.toolStripMenuItemTransparency0.Text = "0%";
            this.toolStripMenuItemTransparency0.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemTransparency1
            // 
            this.toolStripMenuItemTransparency1.Name = "toolStripMenuItemTransparency1";
            this.toolStripMenuItemTransparency1.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency1.Tag = ".9";
            this.toolStripMenuItemTransparency1.Text = "10%";
            this.toolStripMenuItemTransparency1.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemTransparency2
            // 
            this.toolStripMenuItemTransparency2.Name = "toolStripMenuItemTransparency2";
            this.toolStripMenuItemTransparency2.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency2.Tag = ".8";
            this.toolStripMenuItemTransparency2.Text = "20%";
            this.toolStripMenuItemTransparency2.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemTransparency3
            // 
            this.toolStripMenuItemTransparency3.Name = "toolStripMenuItemTransparency3";
            this.toolStripMenuItemTransparency3.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency3.Tag = ".7";
            this.toolStripMenuItemTransparency3.Text = "30%";
            this.toolStripMenuItemTransparency3.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemTransparency4
            // 
            this.toolStripMenuItemTransparency4.Name = "toolStripMenuItemTransparency4";
            this.toolStripMenuItemTransparency4.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency4.Tag = ".6";
            this.toolStripMenuItemTransparency4.Text = "40%";
            this.toolStripMenuItemTransparency4.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemTransparency5
            // 
            this.toolStripMenuItemTransparency5.Name = "toolStripMenuItemTransparency5";
            this.toolStripMenuItemTransparency5.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency5.Tag = ".5";
            this.toolStripMenuItemTransparency5.Text = "50%";
            this.toolStripMenuItemTransparency5.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemTransparency6
            // 
            this.toolStripMenuItemTransparency6.Name = "toolStripMenuItemTransparency6";
            this.toolStripMenuItemTransparency6.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency6.Tag = ".4";
            this.toolStripMenuItemTransparency6.Text = "60%";
            this.toolStripMenuItemTransparency6.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemTransparency7
            // 
            this.toolStripMenuItemTransparency7.Name = "toolStripMenuItemTransparency7";
            this.toolStripMenuItemTransparency7.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency7.Tag = ".3";
            this.toolStripMenuItemTransparency7.Text = "70%";
            this.toolStripMenuItemTransparency7.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemTransparency8
            // 
            this.toolStripMenuItemTransparency8.Name = "toolStripMenuItemTransparency8";
            this.toolStripMenuItemTransparency8.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency8.Tag = ".2";
            this.toolStripMenuItemTransparency8.Text = "80%";
            this.toolStripMenuItemTransparency8.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemTransparency9
            // 
            this.toolStripMenuItemTransparency9.Name = "toolStripMenuItemTransparency9";
            this.toolStripMenuItemTransparency9.Size = new System.Drawing.Size(96, 22);
            this.toolStripMenuItemTransparency9.Tag = ".1";
            this.toolStripMenuItemTransparency9.Text = "90%";
            this.toolStripMenuItemTransparency9.Click += new System.EventHandler(this.toolStripMenuItemTransparency_Clicked);
            // 
            // toolStripMenuItemStereoMode
            // 
            this.toolStripMenuItemStereoMode.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemStereoModeSurround,
            this.toolStripMenuItemStereoModeOriginal,
            this.toolStripMenuItemStereoModeHeadphones,
            this.toolStripMenuItemStereoModeStereo,
            this.toolStripMenuItemStereoModeReverse,
            this.toolStripMenuItemStereoModeLeft,
            this.toolStripMenuItemStereoModeRight,
            this.toolStripMenuItemStereoModeMono});
            this.toolStripMenuItemStereoMode.Name = "toolStripMenuItemStereoMode";
            this.toolStripMenuItemStereoMode.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemStereoMode.Text = "Stereo Mode";
            // 
            // toolStripMenuItemStereoModeSurround
            // 
            this.toolStripMenuItemStereoModeSurround.Name = "toolStripMenuItemStereoModeSurround";
            this.toolStripMenuItemStereoModeSurround.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemStereoModeSurround.Tag = "5";
            this.toolStripMenuItemStereoModeSurround.Text = "Surround (5.1+)";
            this.toolStripMenuItemStereoModeSurround.Click += new System.EventHandler(this.toolStripMenuItemStereoMode_Click);
            // 
            // toolStripMenuItemStereoModeOriginal
            // 
            this.toolStripMenuItemStereoModeOriginal.Checked = true;
            this.toolStripMenuItemStereoModeOriginal.CheckState = System.Windows.Forms.CheckState.Checked;
            this.toolStripMenuItemStereoModeOriginal.Name = "toolStripMenuItemStereoModeOriginal";
            this.toolStripMenuItemStereoModeOriginal.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemStereoModeOriginal.Tag = "0";
            this.toolStripMenuItemStereoModeOriginal.Text = "Original";
            this.toolStripMenuItemStereoModeOriginal.Click += new System.EventHandler(this.toolStripMenuItemStereoMode_Click);
            // 
            // toolStripMenuItemStereoModeHeadphones
            // 
            this.toolStripMenuItemStereoModeHeadphones.Name = "toolStripMenuItemStereoModeHeadphones";
            this.toolStripMenuItemStereoModeHeadphones.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemStereoModeHeadphones.Tag = "6";
            this.toolStripMenuItemStereoModeHeadphones.Text = "Headphones";
            this.toolStripMenuItemStereoModeHeadphones.Click += new System.EventHandler(this.toolStripMenuItemStereoMode_Click);
            // 
            // toolStripMenuItemStereoModeStereo
            // 
            this.toolStripMenuItemStereoModeStereo.Name = "toolStripMenuItemStereoModeStereo";
            this.toolStripMenuItemStereoModeStereo.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemStereoModeStereo.Tag = "1";
            this.toolStripMenuItemStereoModeStereo.Text = "Stereo";
            this.toolStripMenuItemStereoModeStereo.Click += new System.EventHandler(this.toolStripMenuItemStereoMode_Click);
            // 
            // toolStripMenuItemStereoModeReverse
            // 
            this.toolStripMenuItemStereoModeReverse.Name = "toolStripMenuItemStereoModeReverse";
            this.toolStripMenuItemStereoModeReverse.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemStereoModeReverse.Tag = "2";
            this.toolStripMenuItemStereoModeReverse.Text = "Reverse Stereo";
            this.toolStripMenuItemStereoModeReverse.Click += new System.EventHandler(this.toolStripMenuItemStereoMode_Click);
            // 
            // toolStripMenuItemStereoModeLeft
            // 
            this.toolStripMenuItemStereoModeLeft.Name = "toolStripMenuItemStereoModeLeft";
            this.toolStripMenuItemStereoModeLeft.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemStereoModeLeft.Tag = "3";
            this.toolStripMenuItemStereoModeLeft.Text = "Left Channel";
            this.toolStripMenuItemStereoModeLeft.Click += new System.EventHandler(this.toolStripMenuItemStereoMode_Click);
            // 
            // toolStripMenuItemStereoModeRight
            // 
            this.toolStripMenuItemStereoModeRight.Name = "toolStripMenuItemStereoModeRight";
            this.toolStripMenuItemStereoModeRight.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemStereoModeRight.Tag = "4";
            this.toolStripMenuItemStereoModeRight.Text = "Right Channel";
            this.toolStripMenuItemStereoModeRight.Click += new System.EventHandler(this.toolStripMenuItemStereoMode_Click);
            // 
            // toolStripMenuItemStereoModeMono
            // 
            this.toolStripMenuItemStereoModeMono.Name = "toolStripMenuItemStereoModeMono";
            this.toolStripMenuItemStereoModeMono.Size = new System.Drawing.Size(157, 22);
            this.toolStripMenuItemStereoModeMono.Tag = "7";
            this.toolStripMenuItemStereoModeMono.Text = "Mono";
            this.toolStripMenuItemStereoModeMono.Click += new System.EventHandler(this.toolStripMenuItemStereoMode_Click);
            // 
            // toolStripSeparator6
            // 
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new System.Drawing.Size(175, 6);
            // 
            // toolStripMenuItemGuide
            // 
            this.toolStripMenuItemGuide.Name = "toolStripMenuItemGuide";
            this.toolStripMenuItemGuide.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemGuide.Text = "Guide";
            // 
            // toolStripMenuItemChannelEditor
            // 
            this.toolStripMenuItemChannelEditor.Name = "toolStripMenuItemChannelEditor";
            this.toolStripMenuItemChannelEditor.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemChannelEditor.Text = "Channel Editor";
            // 
            // toolStripMenuItemAbout
            // 
            this.toolStripMenuItemAbout.Image = global::FoxIPTV.Properties.Resources.question_white;
            this.toolStripMenuItemAbout.Name = "toolStripMenuItemAbout";
            this.toolStripMenuItemAbout.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemAbout.Text = "&About";
            // 
            // toolStripMenuItemQuit
            // 
            this.toolStripMenuItemQuit.Image = global::FoxIPTV.Properties.Resources.door__arrow;
            this.toolStripMenuItemQuit.Name = "toolStripMenuItemQuit";
            this.toolStripMenuItemQuit.Size = new System.Drawing.Size(178, 22);
            this.toolStripMenuItemQuit.Text = "Quit";
            // 
            // pictureBoxAudioRate
            // 
            this.pictureBoxAudioRate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxAudioRate.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxAudioRate.Image = global::FoxIPTV.Properties.Resources.@__AR_48KHZ;
            this.pictureBoxAudioRate.Location = new System.Drawing.Point(951, 57);
            this.pictureBoxAudioRate.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.pictureBoxAudioRate.Name = "pictureBoxAudioRate";
            this.pictureBoxAudioRate.Size = new System.Drawing.Size(48, 24);
            this.pictureBoxAudioRate.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxAudioRate.TabIndex = 6;
            this.pictureBoxAudioRate.TabStop = false;
            this.pictureBoxAudioRate.Visible = false;
            // 
            // pictureBoxAudioType
            // 
            this.pictureBoxAudioType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxAudioType.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxAudioType.Image = global::FoxIPTV.Properties.Resources.@__CH_STEREO;
            this.pictureBoxAudioType.Location = new System.Drawing.Point(893, 57);
            this.pictureBoxAudioType.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.pictureBoxAudioType.Name = "pictureBoxAudioType";
            this.pictureBoxAudioType.Size = new System.Drawing.Size(48, 24);
            this.pictureBoxAudioType.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxAudioType.TabIndex = 7;
            this.pictureBoxAudioType.TabStop = false;
            this.pictureBoxAudioType.Visible = false;
            // 
            // pictureBoxAudioCodec
            // 
            this.pictureBoxAudioCodec.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxAudioCodec.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxAudioCodec.Image = global::FoxIPTV.Properties.Resources.@__AC_MP4A;
            this.pictureBoxAudioCodec.Location = new System.Drawing.Point(835, 57);
            this.pictureBoxAudioCodec.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.pictureBoxAudioCodec.Name = "pictureBoxAudioCodec";
            this.pictureBoxAudioCodec.Size = new System.Drawing.Size(48, 24);
            this.pictureBoxAudioCodec.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxAudioCodec.TabIndex = 8;
            this.pictureBoxAudioCodec.TabStop = false;
            this.pictureBoxAudioCodec.Visible = false;
            // 
            // pictureBoxFramerate
            // 
            this.pictureBoxFramerate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxFramerate.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxFramerate.Image = global::FoxIPTV.Properties.Resources.@__FR_30FPS;
            this.pictureBoxFramerate.Location = new System.Drawing.Point(777, 57);
            this.pictureBoxFramerate.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.pictureBoxFramerate.Name = "pictureBoxFramerate";
            this.pictureBoxFramerate.Size = new System.Drawing.Size(48, 24);
            this.pictureBoxFramerate.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxFramerate.TabIndex = 9;
            this.pictureBoxFramerate.TabStop = false;
            this.pictureBoxFramerate.Visible = false;
            // 
            // pictureBoxVideoSize
            // 
            this.pictureBoxVideoSize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxVideoSize.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxVideoSize.Image = global::FoxIPTV.Properties.Resources.@__VS_720P;
            this.pictureBoxVideoSize.Location = new System.Drawing.Point(719, 57);
            this.pictureBoxVideoSize.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.pictureBoxVideoSize.Name = "pictureBoxVideoSize";
            this.pictureBoxVideoSize.Size = new System.Drawing.Size(48, 24);
            this.pictureBoxVideoSize.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxVideoSize.TabIndex = 10;
            this.pictureBoxVideoSize.TabStop = false;
            this.pictureBoxVideoSize.Visible = false;
            // 
            // pictureBoxVideoCodec
            // 
            this.pictureBoxVideoCodec.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxVideoCodec.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxVideoCodec.Image = global::FoxIPTV.Properties.Resources.@__VC_H264;
            this.pictureBoxVideoCodec.Location = new System.Drawing.Point(661, 57);
            this.pictureBoxVideoCodec.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.pictureBoxVideoCodec.Name = "pictureBoxVideoCodec";
            this.pictureBoxVideoCodec.Size = new System.Drawing.Size(48, 24);
            this.pictureBoxVideoCodec.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxVideoCodec.TabIndex = 11;
            this.pictureBoxVideoCodec.TabStop = false;
            this.pictureBoxVideoCodec.Visible = false;
            // 
            // pictureBoxClosedCaptioning
            // 
            this.pictureBoxClosedCaptioning.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxClosedCaptioning.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxClosedCaptioning.Image = global::FoxIPTV.Properties.Resources.@__ClosedCaptioning;
            this.pictureBoxClosedCaptioning.Location = new System.Drawing.Point(603, 57);
            this.pictureBoxClosedCaptioning.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.pictureBoxClosedCaptioning.Name = "pictureBoxClosedCaptioning";
            this.pictureBoxClosedCaptioning.Size = new System.Drawing.Size(48, 24);
            this.pictureBoxClosedCaptioning.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxClosedCaptioning.TabIndex = 12;
            this.pictureBoxClosedCaptioning.TabStop = false;
            this.pictureBoxClosedCaptioning.Visible = false;
            // 
            // notifyIcon
            // 
            this.notifyIcon.ContextMenuStrip = this.contextMenuStrip;
            this.notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.notifyIcon.Text = "Fox IPTV";
            this.notifyIcon.Visible = true;
            this.notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon_MouseClick);
            // 
            // labelStatus
            // 
            this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelStatus.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.labelStatus.Font = new System.Drawing.Font("Consolas", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelStatus.ForeColor = System.Drawing.Color.White;
            this.labelStatus.Location = new System.Drawing.Point(12, 468);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(984, 45);
            this.labelStatus.TabIndex = 13;
            this.labelStatus.Text = "Loading...";
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // TvForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(1008, 537);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.pictureBoxClosedCaptioning);
            this.Controls.Add(this.pictureBoxVideoCodec);
            this.Controls.Add(this.pictureBoxVideoSize);
            this.Controls.Add(this.pictureBoxFramerate);
            this.Controls.Add(this.pictureBoxAudioCodec);
            this.Controls.Add(this.pictureBoxAudioType);
            this.Controls.Add(this.pictureBoxAudioRate);
            this.Controls.Add(this.muteLabel);
            this.Controls.Add(this.channelNameLabel);
            this.Controls.Add(this.channelLabel);
            this.Controls.Add(this.vlcControl);
            this.Controls.Add(this.statusStrip);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MinimumSize = new System.Drawing.Size(1020, 564);
            this.Name = "TvForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Fox IPTV";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TvForm_FormClosing);
            this.Load += new System.EventHandler(this.TvForm_Load);
            this.Shown += new System.EventHandler(this.TvForm_Shown);
            this.ResizeEnd += new System.EventHandler(this.TvForm_ResizeEnd);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.TvForm_KeyUp);
            this.Move += new System.EventHandler(this.TvForm_Move);
            ((System.ComponentModel.ISupportInitialize)(this.vlcControl)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.contextMenuStrip.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxAudioRate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxAudioType)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxAudioCodec)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxFramerate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxVideoSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxVideoCodec)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxClosedCaptioning)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        

        private Vlc.DotNet.Forms.VlcControl vlcControl;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripProgressBar channelStatusProgressBar;
        private System.Windows.Forms.ToolStripProgressBar guideStatusProgressBar;
        private System.Windows.Forms.ToolStripStatusLabel channelStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel guideStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel playerStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel timeStatusLabel;
        private System.Windows.Forms.ToolStripProgressBar bufferStatusProgressBar;
        private System.Windows.Forms.Label channelLabel;
        private System.Windows.Forms.Label channelNameLabel;
        private System.Windows.Forms.Timer _timer;
        private System.Windows.Forms.Label muteLabel;
        private System.Windows.Forms.ToolStripStatusLabel ccStatusLabel;
        private System.Windows.Forms.ToolStripDropDownButton ccOptionsDropDownButton;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemChannelNumber;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemChannelName;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemChannelUp;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemChannelDown;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStatusBar;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemClosedCaptioning;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemFullscreen;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemAbout;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemQuit;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemMute;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemAlwaysOnTop;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemGuide;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemChannelEditor;
        private System.Windows.Forms.PictureBox pictureBoxAudioRate;
        private System.Windows.Forms.PictureBox pictureBoxAudioType;
        private System.Windows.Forms.PictureBox pictureBoxAudioCodec;
        private System.Windows.Forms.PictureBox pictureBoxFramerate;
        private System.Windows.Forms.PictureBox pictureBoxVideoSize;
        private System.Windows.Forms.PictureBox pictureBoxVideoCodec;
        private System.Windows.Forms.PictureBox pictureBoxClosedCaptioning;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemBorders;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemWindowState;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparencyMenu;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency0;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency3;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency4;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency5;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency6;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency7;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency8;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemTransparency9;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStereoMode;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStereoModeSurround;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStereoModeStereo;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStereoModeOriginal;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStereoModeReverse;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStereoModeLeft;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStereoModeRight;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStereoModeHeadphones;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemStereoModeMono;
        private System.Windows.Forms.Label labelStatus;
    }
}

