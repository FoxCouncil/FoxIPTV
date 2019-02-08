// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Controls
{
    using System;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Classes;
    using Properties;
    using Timer = System.Windows.Forms.Timer;

    public sealed class GuideLayoutPanel : TableLayoutPanel
    {
        private const int defaultStylePercentage = 42;

        private const double defaultRowHeight = 60d;
        private const double defaultColWidth = 80d;

        private readonly Timer timer = new Timer();

        private DateTime lastHeaderTime;

        private bool _timeCursorOn;
        private DateTime _timeCursor;

        private bool _dataLoaded;

        private readonly ToolTip _toolTip = new ToolTip { IsBalloon = true, ToolTipIcon = ToolTipIcon.Info, ToolTipTitle = "Guide Info" };

        public GuideLayoutPanel()
        {
            BackColor = Color.Black;

            BorderStyle = BorderStyle.Fixed3D;

            Dock = DockStyle.Fill;

            Margin = Padding.Empty;

            // Padding = new Padding(0, 0, 0, 4);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            timer.Interval = 250;
            timer.Tick += timer_Tick;
            timer.Enabled = true;

            TvCore.StateChanged += state => _dataLoaded = state == TvCoreState.Running;
            TvCore.ChannelChanged += async channel => await Draw(true);
        }

        public async Task Draw(bool drawAnyway = false)
        {
            await Task.Run(() =>
            {
                if (!_dataLoaded)
                {
                    return;
                }

                var rowCount = (int)Math.Floor(Size.Height / defaultRowHeight);

                var columnCount = (int)Math.Floor(Size.Width / defaultColWidth);

                if (!drawAnyway && RowCount == rowCount && ColumnCount == columnCount)
                {
                    return;
                }

                this.InvokeIfRequired(() =>
                {
                    SuspendLayout();
                    Controls.Clear();
                });

                RowCount = rowCount;

                for (var i = 1; i <= RowCount; i++)
                {
                    RowStyles.Add(new RowStyle(SizeType.Percent, defaultStylePercentage));
                }

                ColumnCount = columnCount;

                for (var i = 1; i <= ColumnCount; i++)
                {
                    ColumnStyles.Add(new ColumnStyle(SizeType.Percent, defaultStylePercentage));
                }

                // Headers
                var todayLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Black,
                    ForeColor = Color.White,
                    Margin = Padding.Empty,
                    UseMnemonic = false,
                    Font = new Font(Font.Name, 12F),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "Today"
                };

                this.InvokeIfRequired(() =>
                {
                    Controls.Add(todayLabel, 0, 0);
                    SetColumnSpan(todayLabel, 4);
                });

                // Time Columns
                lastHeaderTime = DateTime.Now;
                var now = _timeCursorOn ? _timeCursor : lastHeaderTime;
                var minutes = now.Minute;
                var initialSize = 3 - (minutes >= 30 ? minutes - 30 : minutes) / 10;

                var firstTimeHeaderLabel = new BorderedLabel()
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Black,
                    ForeColor = Color.White,
                    Margin = Padding.Empty,
                    Padding = new Padding(6, 0, 0, 0),
                    Font = new Font(Font.Name, 12F),
                    TextAlign = ContentAlignment.BottomLeft,
                    LeftBorder = true,
                    LeftBorderWidth = 4,
                    LeftBorderColor = Color.White,
                    LeftBorderStyle = ButtonBorderStyle.Solid,
                    Text = $"{now:hh:mm tt}"
                };

                this.InvokeIfRequired(() =>
                {
                    Controls.Add(firstTimeHeaderLabel, 4, 0);
                    SetColumnSpan(firstTimeHeaderLabel, initialSize);
                });

                var delta = minutes >= 30 ? 60 - minutes : 30 - minutes;

                now = now.AddMinutes(delta);

                var idx = 4 + initialSize;
                var headerSpaceLeft = ColumnCount - idx;

                while (headerSpaceLeft > 0)
                {
                    var column = idx;

                    var control = new BorderedLabel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Black,
                        ForeColor = Color.White,
                        Margin = Padding.Empty,
                        Padding = new Padding(6, 0, 0, 0),
                        Font = new Font(Font.Name, 12F),
                        TextAlign = ContentAlignment.BottomLeft,
                        LeftBorder = true,
                        LeftBorderWidth = 4,
                        LeftBorderColor = Color.White,
                        LeftBorderStyle = ButtonBorderStyle.Solid,
                        HoverEffect = true,
                        Tag = now,
                        Text = $"{now:hh:mm tt}"
                    };

                    control.Click += async (s, a) =>
                    {
                        _timeCursorOn = true;
                        _timeCursor = (DateTime)control.Tag;

                        await Draw(true);
                    };

                    var size = headerSpaceLeft >= 3 ? 3 : headerSpaceLeft;

                    this.InvokeIfRequired(() =>
                    {
                        Controls.Add(control, column, 0);
                        SetColumnSpan(control, size);
                    });

                    headerSpaceLeft -= size;
                    idx += size;

                    now = now.AddMinutes(30);
                }

                // Rows
                for (var rowIdx = 1; rowIdx < RowCount; rowIdx++)
                {
                    var channelIndex = TvCore.CurrentChannelIndex;

                    channelIndex += (uint)rowIdx - 1;

                    var columnTime = _timeCursorOn ? _timeCursor.ToUniversalTime() : DateTime.UtcNow;

                    var channelNumber = TvCore.ChannelIndexList[(int)channelIndex].ToString();
                    var channel = TvCore.Channels[(int)channelIndex];
                    var channelName = channel.Name.Contains(':') ? channel.Name.Split(new[] { ':' }, 2).Skip(1).FirstOrDefault() : channel.Name;
                    var channelImg = channel.LogoImage ?? Resources.iptv.ToBitmap();

                    var controlRow = rowIdx;

                    void ChangeChanel(object sender, EventArgs args)
                    {
                        var control = sender as Control;

                        if (!(control?.Tag is Channel clickedChannel))
                        {
                            return;
                        }

                        TvCore.SetChannel((uint)TvCore.ChannelIndexList.IndexOf(clickedChannel.Index));
                    }

                    var backColor = rowIdx == 1 ? Color.FromArgb(0, 20, 0) : Color.Black;

                    var channelNumberLabel = new BorderedLabel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = backColor,
                        ForeColor = Color.White,
                        Margin = Padding.Empty,
                        Font = new Font(Font.Name, 12F),
                        TextAlign = ContentAlignment.MiddleCenter,
                        RightBorder = true,
                        RightBorderColor = Color.White,
                        RightBorderWidth = 4,
                        RightBorderStyle = ButtonBorderStyle.Dotted,
                        TopBorder = true,
                        TopBorderColor = Color.White,
                        Tag = channel,
                        HoverEffect = true,
                        Text = channelNumber
                    };

                    channelNumberLabel.Click += ChangeChanel;

                    this.InvokeIfRequired(() => Controls.Add(channelNumberLabel, 0, controlRow));

                    var channelNameControl = new BorderedLabel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = backColor,
                        ForeColor = Color.White,
                        Margin = Padding.Empty,
                        TextAlign = ContentAlignment.MiddleLeft,
                        TopBorder = true,
                        TopBorderColor = Color.White,
                        Tag = channel,
                        HoverEffect = true,
                        Text = channelName
                    };

                    channelNameControl.Controls.Add(new PictureBox
                    {
                        AutoSize = false,
                        BackColor = Color.White,
                        Dock = DockStyle.Right,
                        Image = channelImg,
                        MaximumSize = new Size(60, 0),
                        Margin = Padding.Empty,
                        MinimumSize = new Size(60, 0),
                        Padding = Padding.Empty,
                        SizeMode = PictureBoxSizeMode.Zoom
                    });

                    channelNameControl.Click += ChangeChanel;

                    this.InvokeIfRequired(() =>
                    {
                        Controls.Add(channelNameControl, 1, controlRow);
                        SetColumnSpan(channelNameControl, 3);
                    });

                    var channelProgrammes = TvCore.Guide.FindAll(x => x.Channel == channel.Id);

                    var dergValue = 20;

                    if (channelProgrammes.Count == 0)
                    {
                        var borderedLabel = new BorderedLabel
                        {
                            Dock = DockStyle.Fill,
                            BackColor = Color.FromArgb(dergValue, dergValue, dergValue),
                            ForeColor = Color.LightGray,
                            Margin = Padding.Empty,
                            Padding = new Padding(6, 0, 0, 0),
                            Font = new Font(Font.Name, 10F),
                            TextAlign = ContentAlignment.MiddleCenter,
                            TopBorder = true,
                            TopBorderColor = Color.White,
                            TopBorderStyle = ButtonBorderStyle.Solid,
                            LeftBorder = true,
                            LeftBorderWidth = 4,
                            LeftBorderColor = Color.White,
                            LeftBorderStyle = ButtonBorderStyle.Solid,
                            Text = "Not Available"
                        };

                        this.InvokeIfRequired(() => 
                        {
                            Controls.Add(borderedLabel, 4, controlRow);
                            SetColumnSpan(borderedLabel, ColumnCount - 4);
                        });

                        continue;
                    }

                    var columnsLeft = ColumnCount - 4;
                    var colIdx = 4;

                    while (columnsLeft > 0)
                    {
                        var channelCurrentProgramme = channelProgrammes.Find(x => x.Start <= columnTime && x.Stop >= columnTime);

                        if (channelCurrentProgramme == null)
                        {
                            var borderedLabel = new BorderedLabel
                            {
                                Dock = DockStyle.Fill,
                                BackColor = Color.FromArgb(dergValue, dergValue, dergValue),
                                ForeColor = Color.LightGray,
                                Margin = Padding.Empty,
                                Padding = new Padding(6, 0, 0, 0),
                                Font = new Font(Font.Name, 10F),
                                TextAlign = ContentAlignment.MiddleCenter,
                                TopBorder = true,
                                TopBorderColor = Color.White,
                                TopBorderStyle = ButtonBorderStyle.Solid,
                                LeftBorder = true,
                                LeftBorderWidth = 4,
                                LeftBorderColor = Color.White,
                                LeftBorderStyle = ButtonBorderStyle.Solid,
                                Text = "Not Available"
                            };

                            var left = columnsLeft;
                            this.InvokeIfRequired(() =>
                            {
                                Controls.Add(borderedLabel, colIdx, controlRow);
                                SetColumnSpan(borderedLabel, left);
                            });

                            columnsLeft = 0;
                            continue;
                        }

                        var timeLeft = (int)Math.Ceiling((channelCurrentProgramme.Stop - columnTime).TotalMinutes / 10d);
                        var columnsUsed = timeLeft > columnsLeft ? columnsLeft : timeLeft;

                        var isCurrentProgramme = channelCurrentProgramme.Start <= DateTime.UtcNow && channelCurrentProgramme.Stop >= DateTime.UtcNow;

                        this.InvokeIfRequired(() =>
                        {
                            var borderedLabel = new BorderedLabel
                            {
                                Dock = DockStyle.Fill,
                                BackColor = isCurrentProgramme ? Color.DarkSlateGray : Color.DimGray,
                                ForeColor = Color.White,
                                Margin = Padding.Empty,
                                Padding = new Padding(6, 0, 0, 0),
                                Font = new Font(Font.Name, 10F),
                                TextAlign = ContentAlignment.MiddleLeft,
                                TopBorder = true,
                                TopBorderColor = Color.White,
                                TopBorderStyle = ButtonBorderStyle.Solid,
                                LeftBorder = true,
                                LeftBorderWidth = 4,
                                LeftBorderColor = Color.White,
                                LeftBorderStyle = ButtonBorderStyle.Solid,
                                HoverEffect = true,
                                Tag = channelCurrentProgramme,
                                Text = channelCurrentProgramme.Title
                            };

                            borderedLabel.MouseEnter += (s, a) =>
                            {
                                var window = s as IWin32Window;

                                _toolTip.Show(string.Empty, window, 0);
                            };

                            borderedLabel.MouseMove += (s, a) =>
                            {
                                var window = s as IWin32Window;
                                const int offset = 5;
                                var newPoint = new Point(a.Location.X + offset, a.Location.Y + offset);

                                _toolTip.ToolTipTitle = $"{channelCurrentProgramme.Start.ToLocalTime():h:mm tt} to {channelCurrentProgramme.Stop.ToLocalTime():h:mm tt} - {channelCurrentProgramme.BlockLength * 10} minutes";
                                _toolTip.Show($"{channelCurrentProgramme.Title}\n{channelCurrentProgramme.Description}", window, newPoint);
                            };

                            borderedLabel.MouseLeave += (s, a) =>
                            {
                                _toolTip.Hide(this);
                            };

                            this.InvokeIfRequired(() =>
                            {
                                Controls.Add(borderedLabel, colIdx, controlRow);
                                SetColumnSpan(borderedLabel, columnsUsed);
                            });
                        });

                        columnsLeft -= columnsUsed;
                        colIdx += columnsUsed;
                        columnTime = channelCurrentProgramme.Stop.AddMinutes(1);
                    }
                }

                this.InvokeIfRequired(() => ResumeLayout(true));
            });
        }

        public async Task ResetView()
        {
            _timeCursorOn = false;

            await Draw(true);
        }

        private async void timer_Tick(object sender, EventArgs e)
        {
            if (lastHeaderTime.Minute != DateTime.Now.Minute)
            {
                await Draw(true);
            }
        }
    }
}
