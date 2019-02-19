// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Forms
{
    using Classes;
    using Properties;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows.Forms;
    using Vlc.DotNet.Core;
    using Vlc.DotNet.Forms;

    /// <inheritdoc/>
    /// <summary>The main form for displaying IPTV streams</summary>
    public partial class TvForm : Form
    {
        /// <summary>The title for the message box to show during debugging when an icon is missing</summary>
        private const string TvIconErrorCaption = "Missing Icon Key";

        /// <summary>The default volume</summary>
        private const int DefaultVolume = 100;

        /// <summary>Used when the user presses the left mouse button while the cursor is within the non-client area of a window</summary>
        private const int WM_NCLBUTTONDOWN = 0xA1;

        /// <summary>In a title bar</summary>
        private const int HT_CAPTION = 0x2;

        /// <summary>The current Closed Captioning track index</summary>
        private int _ccIdx;

        /// <summary>The time out in 100ms chunks to wait before retrying the media stream</summary>
        private int _isErrorRetryTimeout = 100;

        /// <summary>Used to determine if there is Closed Captioning data available</summary>
        private bool _ccDetected;

        /// <summary>Locks the form to only allow initialization once</summary>
        private bool _isInitialized;

        /// <summary>Used to determine if currently in a retry error state</summary>
        private bool _isErrorState;

        /// <summary>Used to avoid called disposed methods during a multi-threaded shutdown</summary>
        private bool _isClosing;

        /// <summary>The LibVLC data for the current media loaded</summary>
        private VlcMedia _currentMedia;

        /// <summary>The media information icons <see cref="Dictionary{TKey,TValue}"/></summary>
        private static readonly Dictionary<string, Image> _tvIcon = new Dictionary<string, Image>();

        /// <summary>The icons for the current media loaded</summary>
        private TvIconData _currentTvIconData;

        /// <summary>The time value used to wait before hiding the TV UI</summary>
        private int _uiFadeoutTime;

        /// <summary>Is the UI currently in channel entry mode</summary>
        private bool _numberEntryMode;

        /// <summary>The time out before submitting the entry to change the channel</summary>
        private int _numberEntryModeTimeout;
        
        /// <summary>The list of digits entered by the user</summary>
        private readonly List<int> _numberEntryDigits = new List<int>();
        
        /// <summary>A synchronizer to allow cross threaded access to the quit functions, avoiding touching disposed objects</summary>
        private readonly object _isClosingLock = new object();

        /// <summary>The about form, shows information about the application, because</summary>
        private readonly AboutForm _aboutForm = new AboutForm();

        /// <summary>The guide form, used to show current and upcoming programmes for the channels</summary>
        private readonly GuideForm _guideForm = new GuideForm();

        /// <summary>The channels form, used to search for channels and favorite them</summary>
        private readonly ChannelsForm _channelsForm = new ChannelsForm();

        /// <summary>Send a windows message on behalf of this app</summary>
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        /// <summary>Release the mouse capture state so we can drag a borderless window</summary>
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        /// <inheritdoc/>
        public TvForm()
        {
            TvCore.LogDebug("[.NET] TvForm(): Starting");

            InitializeTvIcons();

            InitializeComponent();

            TvCore.ChannelChanged += TvCoreOnChannelChanged;
            TvCore.ProgrammeChanged += programme => this.InvokeIfRequired(GuiShow);

            InitializeStatusStrip();

            InitializeContextMenu();

            InitializeMouseCapturePanel();

            InitializeVlcPlayer();

            InitializeFormDefaults();

            // Do initial resize...
            AspectRatioResize();

            TvCore.LogDebug("[.NET] TvForm(): Finished starting");
        }

        /// <summary>Handler for TVCore's channel changed event</summary>
        /// <param name="channel">The new channel we are changing to</param>
        private void TvCoreOnChannelChanged(uint channel)
        {
            this.InvokeIfRequired(() => 
            {
                TvCore.LogDebug($"[.NET] TvCoreOnChannelChanged({channel})");

                RemoveErrorState();

                TvCore.Settings.Channel = channel;
                TvCore.Settings.Save();

                _ccDetected = false;
                ccOptionsDropDownButton.Visible = false;

                ThreadPool.QueueUserWorkItem(state => vlcControl.Stop());

                _currentMedia = null;
                _currentTvIconData = null;

                GuiShow();
            });
        }

        /// <summary>Update the main form title to contain the channel number and current programme</summary>
        private void UpdateFormTitle()
        {
            var currentProgramme = string.Empty;

            if (TvCore.CurrentProgramme != null)
            {
                currentProgramme = $" - [ {TvCore.CurrentProgramme.Title} ]";
            }

            var channelObj = TvCore.CurrentChannel;

            var chanName = channelObj.Name.Contains(':') ? channelObj.Name.Split(new[] {':'}, 2).Skip(1).FirstOrDefault()?.TrimStart() : channelObj.Name;

            Text = string.Format(Resources.TvForm_TitleInfo, TvCore.CurrentChannel.Index, chanName, currentProgramme);
        }

        /// <summary>Initialize the TV Media status icons dictionary</summary>
        private static void InitializeTvIcons()
        {
            // Video Codecs
            _tvIcon.Add("VC_H264", Resources.__VC_H264);
            _tvIcon.Add("VC_MPGV", Resources.__VC_MPGV);

            // Video Size
            _tvIcon.Add("VS_352P", Resources.__VS_352P);
            _tvIcon.Add("VS_400P", Resources.__VS_400P);
            _tvIcon.Add("VS_404P", Resources.__VS_404P);
            _tvIcon.Add("VS_478P", Resources.__VS_478P);
            _tvIcon.Add("VS_480P", Resources.__VS_480P);
            _tvIcon.Add("VS_540P", Resources.__VS_540P);
            _tvIcon.Add("VS_544P", Resources.__VS_544P);
            _tvIcon.Add("VS_532P", Resources.__VS_532P);
            _tvIcon.Add("VS_576P", Resources.__VS_576P);
            _tvIcon.Add("VS_718P", Resources.__VS_718P);
            _tvIcon.Add("VS_720P", Resources.__VS_720P);
            _tvIcon.Add("VS_1080P", Resources.__VS_1080P);

            // Frame-rates
            _tvIcon.Add("FR_24FPS", Resources.__FR_24FPS);
            _tvIcon.Add("FR_25FPS", Resources.__FR_25FPS);
            _tvIcon.Add("FR_27FPS", Resources.__FR_27FPS);
            _tvIcon.Add("FR_30FPS", Resources.__FR_30FPS);
            _tvIcon.Add("FR_50FPS", Resources.__FR_50FPS);
            _tvIcon.Add("FR_60FPS", Resources.__FR_60FPS);
            _tvIcon.Add("FR_90FPS", Resources.__FR_90FPS);
            _tvIcon.Add("FR_120FPS", Resources.__FR_120FPS);

            // Audio Codecs
            _tvIcon.Add("AC_AC3", Resources.__AC_AC3);
            _tvIcon.Add("AC_A52", Resources.__AC_AC3);
            _tvIcon.Add("AC_MP4A", Resources.__AC_MP4A);
            _tvIcon.Add("AC_MPGA", Resources.__AC_MPGA);

            // Audio Channels
            _tvIcon.Add("CH_STEREO", Resources.__CH_STEREO);
            _tvIcon.Add("CH_SURROUND", Resources.__CH_SURROUND);

            // Audio Rate
            _tvIcon.Add("AR_44KHZ", Resources.__AR_44KHZ);
            _tvIcon.Add("AR_48KHZ", Resources.__AR_48KHZ);
        }

        /// <summary>Initialize LibVLC</summary>
        private void InitializeVlcPlayer()
        {
            vlcControl.VlcMediaPlayer.Manager.SetAppId("FoxIPTV", Application.ProductVersion, "");
            vlcControl.VlcMediaPlayer.Manager.SetUserAgent("Fox IPTV", "");
            vlcControl.VlcMediaPlayer.AudioVolume += (sender, args) => { TvCore.LogInfo($"[Audio] Volume: {vlcControl.Audio.Volume}"); };
            vlcControl.VlcMediaPlayer.Log += (s, a) => { TvCore.LogInfo($"[Media] {a.Message}"); };
        }

        /// <summary>Load the form defaults from the saved user settings</summary>
        private void InitializeFormDefaults()
        {
            if (TvCore.Settings.TvFormSize != Size.Empty)
            {
                ClientSize = TvCore.Settings.TvFormSize;
            }

            if (TvCore.Settings.TvFormLocation != Point.Empty)
            {
                StartPosition = FormStartPosition.Manual;
                Location = TvCore.Settings.TvFormLocation;
            }
            else
            {
                TvCore.Settings.TvFormLocation = Location;
                TvCore.Settings.Save();
            }

            statusStrip.Visible = TvCore.Settings.StatusBar;

            FormBorderStyle = TvCore.Settings.Borders ? FormBorderStyle.Sizable : FormBorderStyle.None;

            if (TvCore.Settings.Fullscreen)
            {
                Size = TvCore.Settings.TvFormOldSize;
                Location = TvCore.Settings.TvFormOldLocation;
                WindowState = (FormWindowState)Enum.Parse(typeof(FormWindowState), TvCore.Settings.TvFormOldState);

                FullscreenSet(true);
            }

            TopMost = TvCore.Settings.AlwaysOnTop;

            Visible = TvCore.Settings.Visibility;
        }

        /// <summary>Initialize the right click menu</summary>
        private void InitializeContextMenu()
        {
            toolStripMenuItemChannelUp.Click += (sender, args) => TvCore.ChangeChannel(true);
            toolStripMenuItemChannelDown.Click += (sender, args) => TvCore.ChangeChannel(false);

            toolStripMenuItemWindowState.Click += (sender, args) => ToggleVisibility();
            toolStripMenuItemMute.Click += (sender, args) => ToggleMute();

            toolStripMenuItemStatusBar.Click += (sender, args) => ToggleStatusStrip();
            toolStripMenuItemClosedCaptioning.Click += (sender, args) => ToggleClosedCaptioning();
            toolStripMenuItemBorders.Click += (sender, args) => ToggleBorders();
            toolStripMenuItemFullscreen.Click += (sender, args) => FullscreenSet(!TvCore.Settings.Fullscreen);
            toolStripMenuItemAlwaysOnTop.Click += (sender, args) => ToggleAlwaysOnTop();

            toolStripMenuItemGuide.Click += (sender, args) => ToggleGuideForm();
            toolStripMenuItemChannelEditor.Click += (sender, args) => ToggleChannelsForm();

            toolStripMenuItemAbout.Click += (sender, args) => _aboutForm.ShowDialog(this);
            toolStripMenuItemQuit.Click += (sender, args) => Quit();
        }

        /// <summary>Initialize the status strip for the main form</summary>
        private void InitializeStatusStrip()
        {
            statusStrip.Items.Insert(2, new ToolStripSeparator());
            statusStrip.Items.Insert(5, new ToolStripSeparator());

            statusStrip.MouseClick += MouseClickHandler;

            ccStatusLabel.ForeColor = TvCore.Settings.CCEnabled ? Color.Black : Color.Gainsboro;
        }

        /// <summary>Initialize the capture panel that sits on top of the drawn LibVLC output</summary>
        private void InitializeMouseCapturePanel()
        {
            var panelMouseCapture = new AlphaGradientPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Red,
                Rounded = false,
                Gradient = false,
                Border = false
            };

            panelMouseCapture.MouseDoubleClick += MouseDoubleClickHandler;
            panelMouseCapture.MouseClick += MouseClickHandler;
            panelMouseCapture.MouseDown += (s, a) =>
            {
                if (IsFullscreen || TvCore.Settings.Borders || a.Button != MouseButtons.Left)
                {
                    return;
                }

                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            };

            Controls.Add(panelMouseCapture);

            panelMouseCapture.BringToFront();
        }

        /// <summary>Gets or sets if the form is full screen or not</summary>
        public bool IsFullscreen { get; set; }

        /// <summary>The Form Load handler, which starts TVCore's functionality</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private async void TvForm_Load(object sender, EventArgs e)
        {
            TvCore.ChannelLoadPercentageChanged += percentage => this.InvokeIfRequired(() =>
            {
                channelStatusProgressBar.Value = percentage;
                labelStatus.Text = string.Format(Resources.TvForm_LoadingBarMessage, channelStatusProgressBar.Value, guideStatusProgressBar.Value);
            });

            TvCore.GuideLoadPercentageChanged += percentage => this.InvokeIfRequired(() =>
            {
                guideStatusProgressBar.Value = percentage;
                labelStatus.Text = string.Format(Resources.TvForm_LoadingBarMessage, channelStatusProgressBar.Value, guideStatusProgressBar.Value);
            });

            await TvCore.Start();

            channelStatusLabel.Text = Resources.TvForm_ChannelsLoaded;

            guideStatusLabel.Text = Resources.TvForm_GuideLoaded;

            guideStatusProgressBar.Visible = channelStatusProgressBar.Visible = false;

            labelStatus.Visible = false;

            _isInitialized = true;

            if (TvCore.Settings.ChannelEditorOpen)
            {
                _channelsForm.Show(this);
            }

            if (TvCore.Settings.GuideOpen)
            {
                _guideForm.Show(this);
            }

            TvCore.SetChannel(TvCore.Settings.Channel);
        }

        /// <summary>The KeyUp handler for hot keys</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void TvForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Z)
            {
                Opacity -= .1;

                if (Opacity < 0)
                {
                    Opacity = 0;
                }

                TvCore.LogDebug($"[.NET] Opacity Set {Opacity}");
            }

            if (e.KeyCode == Keys.X)
            {
                Opacity += .1;

                if (Opacity > 1)
                {
                    Opacity = 1;
                }

                TvCore.LogDebug($"[.NET] Opacity Set {Opacity}");
            }

            if (e.KeyCode == Keys.B)
            {
                ToggleBorders();
            }

            if (e.KeyCode == Keys.A)
            {
                ToggleAlwaysOnTop();
            }

            if (e.KeyCode == Keys.F)
            {
                ToggleFullscreen();
            }

            if (e.KeyCode == Keys.S)
            {
                ToggleStatusStrip();
            }

            if (e.KeyCode == Keys.I)
            {
                GuiShow();
            }

            if (e.KeyCode == Keys.H)
            {
                ToggleVisibility();
            }

            if (e.KeyCode == Keys.G)
            {
                ToggleGuideForm();
            }

            if (e.KeyCode == Keys.T)
            {
                ToggleChannelsForm();
            }

            if (e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown)
            {
                TvCore.ChangeChannel(e.KeyCode == Keys.PageUp);
            }

            if (e.KeyCode == Keys.Space)
            {
                ToggleMute();
            }

            if (e.KeyCode == Keys.C)
            {
                ToggleClosedCaptioning();
            }

            if (e.KeyCode == Keys.Enter && _numberEntryMode)
            {
                _numberEntryModeTimeout = 1;
            }

            if (e.KeyCode == Keys.Back && _numberEntryMode && _numberEntryDigits.Count > 0)
            {
                if (_numberEntryDigits.Count == 1)
                {
                    _numberEntryMode = false;
                    _numberEntryModeTimeout = 0;
                    _numberEntryDigits.Clear();

                    GuiHide();
                }
                else
                {
                    _numberEntryModeTimeout = 20;

                    _numberEntryDigits.RemoveAt(_numberEntryDigits.Count - 1);

                    GuiShow();
                }
            }

            if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 || e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
            {
                var digitPressed = e.KeyCode - (e.KeyCode < Keys.NumPad0 ? Keys.D0 : Keys.NumPad0);

                if (!_numberEntryMode)
                {
                    _numberEntryMode = true;
                }

                _numberEntryModeTimeout = 20;

                _numberEntryDigits.Add(digitPressed);

                GuiShow();
            }
        }

        /// <summary>The Form's resize end event handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void TvForm_ResizeEnd(object sender, EventArgs e)
        {
            AspectRatioResize();
        }

        /// <summary>The Form's handler when shown</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void TvForm_Shown(object sender, EventArgs e)
        {
            AspectRatioResize();
        }

        /// <summary>Handle the Form's Closing event to hide form instead of closing it</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void TvForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing)
            {
                TvCore.LogDebug("[.NET] Closing main TvForm");

                return;
            }

            e.Cancel = true;

            ToggleVisibility();
        }

        /// <summary>Handle the Form's movement events to save the position</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void TvForm_Move(object sender, EventArgs e)
        {
            TvCore.Settings.TvFormLocation = Location;
            TvCore.Settings.Save();
        }

        /// <summary>The right click menu Opacity change handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void ToolStripMenuItemTransparency_Clicked(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item))
            {
                return;
            }

            var opacityVal = double.Parse(item.Tag.ToString());

            TvCore.Settings.Opacity = Opacity = opacityVal;
            TvCore.Settings.Save();
        }

        /// <summary>The right click menu Stereo mode change handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void ToolStripMenuItemStereoMode_Click(object sender, EventArgs e)
        {
            if(!(sender is ToolStripMenuItem item))
            {
                return;
            }

            var stereoMode = ushort.Parse(item.Tag.ToString());

            TvCore.Settings.StereoMode = stereoMode;
            TvCore.Settings.Save();

            ThreadPool.QueueUserWorkItem(state => { vlcControl.Audio.Channel = stereoMode; });
        }

        /// <summary>Toggle this form's fullscreen state</summary>
        private void ToggleFullscreen()
        {
            FullscreenSet(!TvCore.Settings.Fullscreen);
        }

        /// <summary>Toggle the visibility for the channel editor form</summary>
        private void ToggleChannelsForm()
        {
            if (_channelsForm.Visible)
            {
                TvCore.Settings.ChannelEditorOpen = false;
                _channelsForm.Hide();
            }
            else
            {
                TvCore.Settings.ChannelEditorOpen = true;
                _channelsForm.Show(this);
            }

            TvCore.Settings.Save();
        }

        /// <summary>Toggle the visibility for the guide form</summary>
        private void ToggleGuideForm()
        {
            if (_guideForm.Visible)
            {
                TvCore.Settings.GuideOpen = false;
                _guideForm.Hide();
            }
            else
            {
                TvCore.Settings.GuideOpen = true;
                _guideForm.Show(this);
            }

            TvCore.Settings.Save();
        }

        /// <summary>Toggle the visibility of the form's status strip</summary>
        private void ToggleStatusStrip()
        {
            statusStrip.Visible = !statusStrip.Visible;

            TvCore.Settings.StatusBar = statusStrip.Visible;
            TvCore.Settings.Save();

            AspectRatioResize();
        }

        /// <summary>Toggle the form's borders</summary>
        private void ToggleBorders()
        {
            TvCore.Settings.Borders = !TvCore.Settings.Borders;

            FormBorderStyle = TvCore.Settings.Borders ? FormBorderStyle.Sizable : FormBorderStyle.None;

            TvCore.Settings.Save();

            AspectRatioResize();
        }

        /// <summary>Toggle the form's Always on Top state</summary>
        private void ToggleAlwaysOnTop()
        {
            TopMost = !TopMost;

            TvCore.Settings.AlwaysOnTop = TopMost;
            TvCore.Settings.Save();
        }

        /// <summary>Toggle the main form's visibility</summary>
        private void ToggleVisibility()
        {
            if (Visible)
            {
                TvCore.LogDebug("[.NET] Hiding main TvForm");
                Hide();
            }
            else
            {
                TvCore.LogDebug("[.NET] Showing main TvForm");
                Show();
            }

            TvCore.Settings.Visibility = Visible;
            TvCore.Settings.Save();
        }

        /// <summary>Toggle the mute state</summary>
        private void ToggleMute()
        {
            ThreadPool.QueueUserWorkItem(state => vlcControl.Audio.Volume = vlcControl.Audio.Volume == 0 ? DefaultVolume : 0);
        }

        /// <summary>Toggle Closed Captioning on or off</summary>
        private void ToggleClosedCaptioning()
        {
            TvCore.Settings.CCEnabled = !TvCore.Settings.CCEnabled;
            TvCore.Settings.Save();

            _ccDetected = false;

            ccStatusLabel.ForeColor = TvCore.Settings.CCEnabled ? Color.Black : Color.Gainsboro;
        }

        /// <summary>The form's double click handler, used to toggle fullscreen</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void MouseDoubleClickHandler(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                FullscreenSet(!TvCore.Settings.Fullscreen);
            }
        }

        /// <summary>The form's click handler, used to detect right click and display a context menu</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void MouseClickHandler(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            if (!_isInitialized)
            {
                return;
            }

            contextMenuStrip.Show(Cursor.Position);
        }

        /// <summary>The right click menu's opening event, used to set the various current states</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            toolStripMenuItemChannelNumber.Text = string.Format(Resources.TvForm_ChannelShorthand, TvCore.CurrentChannel.Index);
            toolStripMenuItemChannelName.Text = TvCore.CurrentChannel.Name;

            toolStripMenuItemWindowState.Checked = Visible;
            toolStripMenuItemWindowState.Text = Visible ? "Hide Window" : "Show Window";

            toolStripMenuItemMute.Checked = vlcControl.Audio.Volume == 0;

            toolStripMenuItemStatusBar.Checked = statusStrip.Visible;

            toolStripMenuItemClosedCaptioning.Enabled = _ccDetected;
            toolStripMenuItemClosedCaptioning.Checked = TvCore.Settings.CCEnabled;

            toolStripMenuItemBorders.Enabled = !IsFullscreen;
            toolStripMenuItemBorders.Checked = TvCore.Settings.Borders;
            toolStripMenuItemFullscreen.Checked = IsFullscreen;
            toolStripMenuItemAlwaysOnTop.Checked = TopMost;

            toolStripMenuItemTransparencyMenu.Checked = Opacity < 1.0;

            foreach (ToolStripMenuItem submenu in toolStripMenuItemTransparencyMenu.DropDownItems)
            {
                if (submenu.GetType() != typeof(ToolStripMenuItem))
                {
                    return;
                }

                var opacityVal = double.Parse(submenu.Tag.ToString());

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                submenu.Checked = Opacity == opacityVal;
            }

            var stereoMode = vlcControl.Audio.Channel;

            foreach (ToolStripMenuItem submenu in toolStripMenuItemStereoMode.DropDownItems)
            {
                if (submenu.GetType() != typeof(ToolStripMenuItem))
                {
                    return;
                }

                var audioChannelsVal = ushort.Parse(submenu.Tag.ToString());

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                submenu.Checked = stereoMode == audioChannelsVal;

                if (audioChannelsVal == 0 && TvCore.Settings.StereoMode == 0 && stereoMode != TvCore.Settings.StereoMode)
                {
                    submenu.Enabled = false;
                    submenu.Checked = true;
                }
                else
                {
                    submenu.Enabled = true;
                }
            }

            toolStripMenuItemGuide.Checked = _guideForm.Visible;
            toolStripMenuItemChannelEditor.Checked = _channelsForm.Visible;
        }

        /// <summary>The notification icon click event, used to trigger visibility on the main form</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ToggleVisibility();
        }

        /// <summary>Direct LibVLC to the directory containing libvlc.dll</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void VlcControl_VlcLibDirectoryNeeded(object sender, VlcLibDirectoryNeededEventArgs e)
        {
            TvCore.LogDebug("[.NET] VlcControl_VlcLibDirectoryNeeded()");

            e.VlcLibDirectory = new DirectoryInfo(TvCore.LibraryPath);
        }

        /// <summary>The LibVLC TimeChanged event handler handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void VlcControl_TimeChanged(object sender, VlcMediaPlayerTimeChangedEventArgs e)
        {
            lock (_isClosingLock)
            {
                if (_isClosing)
                {
                    return;
                }
            }

            if (!_ccDetected && vlcControl.IsPlaying && vlcControl.SubTitles.Count != 0)
            {
                _ccDetected = true;

                this.InvokeIfRequired(GuiShow);

                ThreadPool.QueueUserWorkItem(state => ProcessClosedCaptioning());
            }

            this.InvokeIfRequired(() =>
            {
                lock (_isClosingLock) // TODO: Is this dangerous?
                {
                    if (_isClosing || IsDisposed)
                    {
                        return;
                    }

                    var time = new TimeSpan(e.NewTime * 10000);
                    timeStatusLabel.Text = string.Format(Resources.TvForm_PlaybackTime, time.Minutes.ToString().PadLeft(2, '0'), time.Seconds.ToString().PadLeft(2, '0'));
                }
            });
        }

        /// <summary>The LibVLC Playing event handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void VlcControl_Playing(object sender, VlcMediaPlayerPlayingEventArgs e)
        {
            TvCore.LogDebug("[.NET] VlcControl_Playing()");

            ThreadPool.QueueUserWorkItem(state =>
            {
                _currentMedia = vlcControl.GetCurrentMedia();
            });

            this.InvokeIfRequired(() =>
            {
                playerStatusLabel.Text = Resources.TvForm_Playing;
            });
        }

        /// <summary>The LibVLC Paused event handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void VlcControl_Paused(object sender, VlcMediaPlayerPausedEventArgs e)
        {
            this.InvokeIfRequired(() => { playerStatusLabel.Text = Resources.TvForm_Paused; });
        }

        /// <summary>The LibVLC Stopped event handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void VlcControl_Stopped(object sender, VlcMediaPlayerStoppedEventArgs e)
        {
            TvCore.LogDebug("[.NET] VlcControl_Stopped()");

            this.InvokeIfRequired(() =>
            {
                lock (_isClosingLock)
                {
                    if (_isClosing)
                    {
                        TvCore.LogDebug("[.NET] Closing form...");

                        Application.Exit();

                        return;
                    }
                }

                playerStatusLabel.Text = Resources.TvForm_Buffering;

                if (!Disposing && !_isErrorState)
                {
                    ThreadPool.QueueUserWorkItem(state => { vlcControl.Play(TvCore.CurrentChannel.Stream); });
                }
            });
        }

        /// <summary>The LibVLC Buffering event handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void VlcControl_Buffering(object sender, VlcMediaPlayerBufferingEventArgs e)
        {
            this.InvokeIfRequired(() => { bufferStatusProgressBar.Value = (int)e.NewCache; });
        }

        /// <summary>The LibVLC EncounteredError event handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void VlcControl_EncounteredError(object sender, VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            TvCore.LogError($"[.NET] VlcControl_EncounteredError({e})");

            this.InvokeIfRequired(() =>
            {
                SetErrorState();
                playerStatusLabel.Text = Resources.TvForm_Error;
            });
        }

        /// <summary>The LibVLC EndReached event handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void VlcControl_EndReached(object sender, VlcMediaPlayerEndReachedEventArgs e)
        {
            TvCore.LogDebug($"[.NET] VlcControl_EndReached({e})");

            this.InvokeIfRequired(() =>
            {
                playerStatusLabel.Text = Resources.TvForm_Buffering;
                ThreadPool.QueueUserWorkItem(state => vlcControl.Play(TvCore.CurrentChannel.Stream));
            });
        }

        /// <summary>Used to actually being the quit proceedure for the application</summary>
        private void Quit()
        {
            lock (_isClosingLock)
            {
                if (_isClosing)
                {
                    return;
                }

                _isClosing = true;
            }

            ThreadPool.QueueUserWorkItem(state => vlcControl.Stop());
        }

        /// <summary>Detect if the currently playing stream contains closed captioning</summary>
        private void ProcessClosedCaptioning()
        {
            var idx = Convert.ToInt32(TvCore.Settings.CCEnabled);

            if (vlcControl.SubTitles.Count == 0)
            {
                return;
            }

            vlcControl.SubTitles.Current = _ccIdx != 0 ? vlcControl.SubTitles.All.First(x => x.ID == _ccIdx) : vlcControl.SubTitles.All.ToArray()[idx];

            if (TvCore.Settings.CCEnabled && vlcControl.SubTitles.Count > 2)
            {
                this.InvokeIfRequired(() =>
                {
                    ccOptionsDropDownButton.Visible = true;
                    ccOptionsDropDownButton.Text = vlcControl.SubTitles.Current.Name;
                    ccOptionsDropDownButton.DropDownItems.Clear();

                    var subIdx = _ccIdx != 0 ? 1 : 2;

                    foreach (var subTitle in vlcControl.SubTitles.All.Skip(subIdx))
                    {
                        if (subTitle.ID == _ccIdx)
                        {
                            continue;
                        }

                        ccOptionsDropDownButton.DropDownItems.Add(new ToolStripLabel
                        {
                            AutoSize = true,
                            Text = subTitle.Name,
                            Tag = subTitle.ID
                        });
                    }
                });
            }
            else
            {
                this.InvokeIfRequired(() => { ccOptionsDropDownButton.Visible = false; });
            }
        }

        /// <summary>The Closed Captioning index change event handler</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void CCOptionsDropDownButton_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            _ccIdx = (int)e.ClickedItem.Tag;
            _ccDetected = false;
        }

        /// <summary>Set fullscreen to a specific state</summary>
        /// <param name="value">True for fullscreen, false of restored size and position</param>
        public void FullscreenSet(bool value)
        {
            TvCore.Settings.Fullscreen = value;
            TvCore.Settings.Save();

            if (value)
            {
                TvCore.Settings.TvFormOldSize = Size;
                TvCore.Settings.TvFormOldLocation = Location;
                TvCore.Settings.TvFormOldState = WindowState.ToString();
                TvCore.Settings.Save();

                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                WindowState = (FormWindowState)Enum.Parse(typeof(FormWindowState), TvCore.Settings.TvFormOldState);
                FormBorderStyle = TvCore.Settings.Borders ? FormBorderStyle.Sizable : FormBorderStyle.None;

                Location = TvCore.Settings.TvFormOldLocation;
                Size = TvCore.Settings.TvFormOldSize;
            }

            IsFullscreen = value;

            AspectRatioResize();
        }

        /// <summary>Show the TV GUI to the user</summary>
        private void GuiShow()
        {
            if (!_isInitialized)
            {
                return;
            }

            if (_numberEntryMode)
            {
                channelLabel.Text = string.Join(string.Empty, _numberEntryDigits.Select(x => x.ToString()).ToArray());
                channelNameLabel.Text = string.Empty;
                _uiFadeoutTime = 0;
            }
            else
            {
                UpdateFormTitle();

                pictureBoxClosedCaptioning.Visible = _currentTvIconData?.ClosedCaptioning ?? false;

                GetTvIcon(_currentTvIconData?.VideoCodec, pictureBoxVideoCodec);
                GetTvIcon(_currentTvIconData?.VideoSize, pictureBoxVideoSize);
                GetTvIcon(_currentTvIconData?.FrameRate, pictureBoxFramerate);
                GetTvIcon(_currentTvIconData?.AudioCodec, pictureBoxAudioCodec);
                GetTvIcon(_currentTvIconData?.AudioChannel, pictureBoxAudioType);
                GetTvIcon(_currentTvIconData?.AudioRate, pictureBoxAudioRate);

                channelLabel.Text = TvCore.CurrentChannel.Index.ToString();
                channelNameLabel.Text = TvCore.CurrentChannel.Name;

                if (!string.IsNullOrEmpty(TvCore.CurrentProgramme?.Title))
                {
                    var localStartTime = TvCore.CurrentProgramme.Start.ToLocalTime();
                    var localEndTime = TvCore.CurrentProgramme.Stop.ToLocalTime();

                    var timeBlockText = $"{localStartTime:h:mm tt}-{localEndTime:h:mm tt}: ";

                    channelNameLabel.Text += string.Format(Resources.TvForm_ProgrammeDetailDisplay, timeBlockText, TvCore.CurrentProgramme.Title);

                    if (!string.IsNullOrWhiteSpace(TvCore.CurrentProgramme.Description))
                    {
                        channelNameLabel.Text += string.Format(Resources.TvForm_ProgrammeDescription, TvCore.CurrentProgramme.Description);
                    }
                }

                AspectRatioResize();

                _uiFadeoutTime = 50;
            }
        }

        /// <summary>Hide the TV GUI from the user</summary>
        private void GuiHide()
        {
            channelLabel.Text = string.Empty;
            channelNameLabel.Text = string.Empty;
            channelNameLabel.MinimumSize = new Size(0, 0);

            pictureBoxClosedCaptioning.Visible = false;
            pictureBoxVideoCodec.Visible = false;
            pictureBoxFramerate.Visible = false;
            pictureBoxVideoSize.Visible = false;
            pictureBoxAudioCodec.Visible = false;
            pictureBoxAudioType.Visible = false;
            pictureBoxAudioRate.Visible = false;
        }

        /// <summary>Use with a <see cref="PictureBox"/> to set it to TV Icon needed</summary>
        /// <param name="iconStringKey">The string key for the needed icon</param>
        /// <param name="pictureBox">The <see cref="PictureBox"/> to modify</param>
        private static void GetTvIcon(string iconStringKey, PictureBox pictureBox)
        {
            pictureBox.Visible = false;

            if (iconStringKey == null)
            {
                return;
            }

            iconStringKey = iconStringKey.Trim();

            if (!_tvIcon.ContainsKey(iconStringKey))
            {
#if DEBUG
                MessageBox.Show(iconStringKey, TvIconErrorCaption);
#endif
            }
            else
            {
                pictureBox.Image = _tvIcon[iconStringKey];
                pictureBox.Visible = true;
            }
        }

        /// <summary>Set the window size to always match the media's aspect ratio</summary>
        private void AspectRatioResize()
        {
            if (WindowState == FormWindowState.Normal)
            {
                var heightAdjust = 0;

                if (statusStrip.Visible)
                {
                    heightAdjust = statusStrip.Height;
                }

                ClientSize = new Size(ClientSize.Width, (int)(0.5625f * ClientSize.Width) + heightAdjust);
            }

            TvCore.Settings.TvFormSize = ClientSize;
            TvCore.Settings.Save();

            if (!string.IsNullOrWhiteSpace(channelNameLabel.Text))
            {
                channelNameLabel.MaximumSize = new Size(0, 0);

                channelNameLabel.Text += @" ";

                channelNameLabel.MinimumSize = channelNameLabel.Width > ClientSize.Width - 18 ? new Size(ClientSize.Width - 18, 0) : new Size(0, 0);

                channelNameLabel.MaximumSize = new Size(ClientSize.Width - 18, 0);

                channelNameLabel.Text = channelNameLabel.Text.TrimEnd();
            }
        }

        /// <summary>Set the media error state</summary>
        private void SetErrorState()
        {
            _isErrorState = true;

            _isErrorRetryTimeout = 100;

            labelStatus.Text = string.Format(Resources.TvForm_ErrorRetry, (double)_isErrorRetryTimeout / 10, (_isErrorRetryTimeout != 10 ? "s" : " "));

            labelStatus.Visible = true;
        }

        /// <summary>Remove the media error state</summary>
        private void RemoveErrorState()
        {
            _isErrorState = false;

            labelStatus.Visible = false;

            labelStatus.Text = string.Empty;

            ThreadPool.QueueUserWorkItem(state => vlcControl.Stop());
        }

        /// <summary>The timer tick handler for the form</summary>
        /// <param name="sender">The object that triggered the event</param>
        /// <param name="e">The event arguments</param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            this.InvokeIfRequired(() =>
            {
                if (!_isInitialized)
                {
                    // Never Run Uninitialized...
                    return;
                }

                if (_isErrorState)
                {
                    _isErrorRetryTimeout--;

                    labelStatus.Text = string.Format(Resources.TvForm_ErrorRetry, (double)_isErrorRetryTimeout/10, (_isErrorRetryTimeout != 10 ? "s" : " "));

                    if (_isErrorRetryTimeout <= 0)
                    {
                        RemoveErrorState();
                    }
                }

                TimerTvIcons();

                TimerUiFade();

                TimerKeyboardEntry();

                muteLabel.Visible = vlcControl.Audio.Volume == 0;

                if (vlcControl.Audio.Channel != TvCore.Settings.StereoMode)
                {
                    vlcControl.Audio.Channel = TvCore.Settings.StereoMode;
                }
            });
        }

        /// <summary>This is used to check for keyboard channel input</summary>
        private void TimerKeyboardEntry()
        {
            if (_numberEntryMode && _numberEntryModeTimeout == 1)
            {
                channelLabel.Text = string.Empty;
                channelNameLabel.Text = string.Empty;

                var newChannelNumber = uint.Parse(string.Join(string.Empty, _numberEntryDigits.Select(x => x.ToString()).ToArray()));

                _numberEntryMode = false;
                _numberEntryModeTimeout = 0;
                _numberEntryDigits.Clear();

                if (!TvCore.ChannelIndexList.Contains(newChannelNumber))
                {
                    return;
                }

                TvCore.SetChannel((uint)TvCore.ChannelIndexList.IndexOf(newChannelNumber));
            }
            else if (_numberEntryMode && _numberEntryModeTimeout > 0)
            {
                _numberEntryModeTimeout--;
            }
        }

        /// <summary>The tick method to eventually hide the GUI after a certain time</summary>
        private void TimerUiFade()
        {
            if (_uiFadeoutTime == 1)
            {
                GuiHide();

                _uiFadeoutTime = 0;
            }
            else if (_uiFadeoutTime > 0 && vlcControl.IsPlaying)
            {
                _uiFadeoutTime--;
            }
        }

        /// <summary>The tick method to find and display the various media information icons</summary>
        private void TimerTvIcons()
        {
            try
            {
                // We need to wait for VLC to build up the media metadata.
                if (_currentMedia?.Tracks == null || _uiFadeoutTime == 0)
                {
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }

            if (_currentTvIconData == null)
            {
                _currentTvIconData = TvIconData.CreateData(_ccDetected, _currentMedia.Tracks);

                GuiShow();
            }
            else
            {
                var tmpTvIcons = TvIconData.CreateData(_ccDetected, _currentMedia.Tracks);

                if (_currentTvIconData == tmpTvIcons)
                {
                    return;
                }
                
                _currentTvIconData = tmpTvIcons;

                GuiShow();
            }
        }
    }
}