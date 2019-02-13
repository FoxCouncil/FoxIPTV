// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FoxIPTV.Classes;
using Vlc.DotNet.Core;
using Vlc.DotNet.Forms;

namespace FoxIPTV.Forms
{
    using Properties;
    using Settings = Classes.Settings;

    public partial class TvForm : Form
    {
        private const string TvIconErrorCaption = "Missing Icon Key";

        private const int DefaultVolume = 100;

        private int _ccIdx;
        private int _isErrorRetryTimeout = 100;

        private bool _ccDetected;
        private bool _isInitialized;
        private bool _isErrorState;
        private bool _isClosing;

        private VlcMedia _currentMedia;

        private static readonly Dictionary<string, Image> _tvIcon = new Dictionary<string, Image>();

        private TvIconData _currentTvIconData;

        private int _uiFadeoutTime;

        private bool _numberEntryMode;
        private int _numberEntryModeTimeout;

        private readonly object _isClosingLock = new object();

        private readonly List<int> _numberEntryDigits = new List<int>();

        private readonly AboutForm _aboutForm = new AboutForm();
        private readonly GuideForm _guideForm = new GuideForm();
        private readonly ChannelsForm _channelsForm = new ChannelsForm();

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

                Text = $"CH: {TvCore.CurrentChannel.Index} [ {TvCore.CurrentChannel.Name} ] Fox IPTV";

                ThreadPool.QueueUserWorkItem(state => vlcControl.Stop());

                _currentMedia = null;
                _currentTvIconData = null;

                GuiShow();
            });
        }

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

        private void InitializeVlcPlayer()
        {
            vlcControl.VlcMediaPlayer.Manager.SetAppId("FoxIPTV", Application.ProductVersion, "");
            vlcControl.VlcMediaPlayer.Manager.SetUserAgent("Fox IPTV", "");
#if DEBUG
            vlcControl.VlcMediaPlayer.AudioVolume += (sender, args) => { Console.WriteLine($"Volume: {vlcControl.Audio.Volume}"); };
#endif
        }

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

        private void InitializeStatusStrip()
        {
            statusStrip.Items.Insert(2, new ToolStripSeparator());
            statusStrip.Items.Insert(5, new ToolStripSeparator());

            statusStrip.MouseClick += MouseClickHandler;

            ccStatusLabel.ForeColor = TvCore.Settings.CCEnabled ? Color.Black : Color.Gainsboro;
        }

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

            Controls.Add(panelMouseCapture);

            panelMouseCapture.BringToFront();
        }

        public bool IsFullscreen { get; set; }

        private async void TvForm_Load(object sender, EventArgs e)
        {
            var channelProgress = 0;
            var guideProgress = 0;

            TvCore.ChannelLoadPercentageChanged += percentage => this.InvokeIfRequired(() =>
            {
                channelStatusProgressBar.Value = channelProgress =  percentage;
                labelStatus.Text = $"Loading | Channels {channelProgress}% | Guide {guideProgress}% | Please Wait...";
            });

            TvCore.GuideLoadPercentageChanged += percentage => this.InvokeIfRequired(() =>
            {
                guideStatusProgressBar.Value = guideProgress = percentage;
                labelStatus.Text = $"Loading | Channels {channelProgress}% | Guide {guideProgress}% | Please Wait...";
            });

            await TvCore.Start();

            channelStatusLabel.Text = "Channels Loaded";

            guideStatusLabel.Text = "Guide Loaded";

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

        private void TvForm_ResizeEnd(object sender, EventArgs e)
        {
            AspectRatioResize();
        }

        private void TvForm_Shown(object sender, EventArgs e)
        {
            AspectRatioResize();
        }

        private void TvForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing)
            {
                TvCore.LogDebug($"[.NET] Closing main TvForm");

                return;
            }

            e.Cancel = true;

            ToggleVisibility();
        }

        private void TvForm_Move(object sender, EventArgs e)
        {
            TvCore.Settings.TvFormLocation = Location;
            TvCore.Settings.Save();
        }

        private void toolStripMenuItemTransparency_Clicked(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item))
            {
                return;
            }

            var opacityVal = double.Parse(item.Tag.ToString());

            TvCore.Settings.Opacity = Opacity = opacityVal;
            TvCore.Settings.Save();
        }

        private void toolStripMenuItemStereoMode_Click(object sender, EventArgs e)
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

        private void ToggleFullscreen()
        {
            FullscreenSet(!TvCore.Settings.Fullscreen);
        }

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

        private void ToggleStatusStrip()
        {
            statusStrip.Visible = !statusStrip.Visible;

            TvCore.Settings.StatusBar = statusStrip.Visible;
            TvCore.Settings.Save();

            AspectRatioResize();
        }

        private void ToggleBorders()
        {
            TvCore.Settings.Borders = !TvCore.Settings.Borders;

            FormBorderStyle = TvCore.Settings.Borders ? FormBorderStyle.Sizable : FormBorderStyle.None;

            TvCore.Settings.Save();
        }

        private void ToggleAlwaysOnTop()
        {
            TopMost = !TopMost;

            TvCore.Settings.AlwaysOnTop = TopMost;
            TvCore.Settings.Save();
        }

        private void ToggleVisibility()
        {
            if (Visible)
            {
                TvCore.LogDebug($"[.NET] Hiding main TvForm");
                Hide();
            }
            else
            {
                TvCore.LogDebug($"[.NET] Showing main TvForm");
                Show();
            }

            TvCore.Settings.Visibility = Visible;
            TvCore.Settings.Save();
        }

        private void ToggleMute()
        {
            ThreadPool.QueueUserWorkItem(state => vlcControl.Audio.Volume = vlcControl.Audio.Volume == 0 ? DefaultVolume : 0);
        }

        private void ToggleClosedCaptioning()
        {
            TvCore.Settings.CCEnabled = !TvCore.Settings.CCEnabled;
            TvCore.Settings.Save();

            _ccDetected = false;

            ccStatusLabel.ForeColor = TvCore.Settings.CCEnabled ? Color.Black : Color.Gainsboro;
        }

        private void MouseDoubleClickHandler(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                FullscreenSet(!TvCore.Settings.Fullscreen);
            }
        }

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

        private void contextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            toolStripMenuItemChannelNumber.Text = $"CH: {TvCore.CurrentChannel.Index}";
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

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ToggleVisibility();
        }

        private void VlcControl_VlcLibDirectoryNeeded(object sender, VlcLibDirectoryNeededEventArgs e)
        {
            TvCore.LogDebug($"[.NET] VlcControl_VlcLibDirectoryNeeded({e})");

            // TODO: Make this from from temp spot...
            e.VlcLibDirectory = new DirectoryInfo("libvlc\\win-x64");
        }

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

            lock (_isClosingLock) // TODO: Is this dangerous?
            {
                this.InvokeIfRequired(() =>
                {
                    if (_isClosing || IsDisposed)
                    {
                        return;
                    }

                    var time = new TimeSpan(e.NewTime * 10000);
                    timeStatusLabel.Text = $"{time.Minutes.ToString().PadLeft(2, '0')}:{time.Seconds.ToString().PadLeft(2, '0')}";
                });
            }
        }

        private void VlcControl_Playing(object sender, VlcMediaPlayerPlayingEventArgs e)
        {
            TvCore.LogDebug("[.NET] VlcControl_Playing()");

            ThreadPool.QueueUserWorkItem(state =>
            {
                _currentMedia = vlcControl.GetCurrentMedia();

                 vlcControl.Audio.Channel = TvCore.Settings.StereoMode;
            });

            this.InvokeIfRequired(() =>
            {
                playerStatusLabel.Text = "Playing";
            });
        }

        private void VlcControl_Paused(object sender, VlcMediaPlayerPausedEventArgs e)
        {
            this.InvokeIfRequired(() => { playerStatusLabel.Text = "Paused"; });
        }

        private void VlcControl_Stopped(object sender, VlcMediaPlayerStoppedEventArgs e)
        {
            TvCore.LogDebug($"[.NET] VlcControl_Stopped()");

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

                playerStatusLabel.Text = "Buffering";

                if (!Disposing && !_isErrorState)
                {
                    ThreadPool.QueueUserWorkItem(state => { vlcControl.Play(TvCore.CurrentChannel.Stream); });
                }
            });
        }

        private void VlcControl_Buffering(object sender, VlcMediaPlayerBufferingEventArgs e)
        {
            this.InvokeIfRequired(() => { bufferStatusProgressBar.Value = (int)e.NewCache; });
        }

        private void VlcControl_EncounteredError(object sender, VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            TvCore.LogError($"[.NET] VlcControl_EncounteredError({e})");

            this.InvokeIfRequired(() =>
            {
                SetErrorState();
                playerStatusLabel.Text = "Error";
            });
        }

        private void VlcControl_EndReached(object sender, VlcMediaPlayerEndReachedEventArgs e)
        {
            TvCore.LogDebug($"[.NET] VlcControl_EndReached({e})");

            this.InvokeIfRequired(() =>
            {
                playerStatusLabel.Text = "Buffering";
                ThreadPool.QueueUserWorkItem(state => vlcControl.Play(TvCore.CurrentChannel.Stream));
            });
        }

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

        private void ccOptionsDropDownButton_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            _ccIdx = (int)e.ClickedItem.Tag;
            _ccDetected = false;
        }

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

                    var timeBlockText = $"{localStartTime.ToString("h:mm tt")}-{localEndTime.ToString("h:mm tt")}: ";

                    channelNameLabel.Text += "\n" + timeBlockText + TvCore.CurrentProgramme.Title;

                    if (!string.IsNullOrWhiteSpace(TvCore.CurrentProgramme.Description)) channelNameLabel.Text += "\nDescription: " + TvCore.CurrentProgramme.Description;
                }

                AspectRatioResize();

                _uiFadeoutTime = 50;
            }
        }

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

            ThreadPool.QueueUserWorkItem(state =>
            {
                Console.WriteLine($"Channels: {vlcControl.Audio.Channel}");
            });
        }

        private void GetTvIcon(string iconStringKey, PictureBox pictureBox)
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

                channelNameLabel.Text += " ";

                if (channelNameLabel.Width > ClientSize.Width - 18)
                {
                    channelNameLabel.MinimumSize = new Size(ClientSize.Width - 18, 0);
                }
                else
                {
                    channelNameLabel.MinimumSize = new Size(0, 0);
                }

                channelNameLabel.MaximumSize = new Size(ClientSize.Width - 18, 0);

                channelNameLabel.Text = channelNameLabel.Text.TrimEnd();
            }
        }

        private void SetErrorState()
        {
            _isErrorState = true;

            _isErrorRetryTimeout = 100;

            labelStatus.Text = "Stream Error: Retrying in 10 seconds";

            labelStatus.Visible = true;
        }

        private void RemoveErrorState()
        {
            _isErrorState = false;

            labelStatus.Visible = false;

            labelStatus.Text = string.Empty;

            ThreadPool.QueueUserWorkItem(state => vlcControl.Stop());
        }

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
                    labelStatus.Text = $"Stream Error: Retrying in {(double)_isErrorRetryTimeout/10:F1} second{(_isErrorRetryTimeout != 10 ? "s" : " ")}";

                    if (_isErrorRetryTimeout <= 0)
                    {
                        RemoveErrorState();
                    }
                }

                TimerTvIcons();

                TimerUiFade();

                TimerKeyboardEntry();

                muteLabel.Visible = vlcControl.Audio.Volume == 0;
            });
        }

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