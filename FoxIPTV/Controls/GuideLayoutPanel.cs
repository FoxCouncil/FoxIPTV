// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Controls
{
    using System;
    using System.Drawing;
    using System.Linq;
    using System.Threading;
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

        private readonly object _guideModelLock = new object();
        private GuideStateModel _guideModel;

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
            TvCore.ChannelChanged += channel => DrawGuide(true);
        }

        public void DrawGuide(bool drawAnyway = false)
        {
            ThreadPool.QueueUserWorkItem(state => { GenerateGuide(drawAnyway); });
        }

        private void GenerateGuide(bool drawAnyway = false)
        {
            if (!_dataLoaded)
            {
                return;
            }

            var newGuideModel = new GuideStateModel
            {
                TotalRows = (int) Math.Floor(Size.Height / defaultRowHeight),
                TotalColumns = (int) Math.Floor(Size.Width / defaultColWidth),
                HeaderTitle = _timeCursorOn ? (DateTime.Now.RoundUp(TimeSpan.FromMinutes(10)) - _timeCursor).ToString("g") : "Today"
            };

            // Time Columns
            lastHeaderTime = DateTime.Now;
            var now = _timeCursorOn ? _timeCursor : lastHeaderTime;
            var minutes = now.Minute;
            var initialSize = 3 - (minutes >= 30 ? minutes - 30 : minutes) / 10;

            newGuideModel.Headers.Add(new GuideTextModel { Text = $"{now:hh:mm tt}", Column = 4, ColSpan = initialSize });

            var delta = minutes >= 30 ? 60 - minutes : 30 - minutes;

            now = now.AddMinutes(delta);

            var idx = 4 + initialSize;
            var headerSpaceLeft = newGuideModel.TotalColumns - idx;

            while (headerSpaceLeft > 0)
            {
                var column = idx;

                var size = headerSpaceLeft >= 3 ? 3 : headerSpaceLeft;

                newGuideModel.Headers.Add(new GuideTextModel { Text = $"{now:hh:mm tt}", Tag = now, Column = column, ColSpan = size });

                headerSpaceLeft -= size;
                idx += size;

                now = now.AddMinutes(30);
            }

            // Rows
            for (var rowIdx = 1; rowIdx < newGuideModel.TotalRows; rowIdx++)
            {
                var channelIndex = TvCore.CurrentChannelIndex;

                channelIndex += (uint)rowIdx - 1;

                if (channelIndex >= TvCore.Channels.Count)
                {
                    channelIndex -= (uint)TvCore.Channels.Count;
                }

                var columnTime = _timeCursorOn ? _timeCursor.ToUniversalTime() : DateTime.UtcNow;

                var channel = TvCore.Channels[(int)channelIndex];

                var newRow = new GuideRowModel
                {
                    Number = TvCore.ChannelIndexList[(int)channelIndex].ToString(),
                    Text = channel.Name.Contains(':') ? channel.Name.Split(new[] { ':' }, 2).Skip(1).FirstOrDefault() : channel.Name,
                    Tag = channel,
                    Image = channel.LogoImage ?? Resources.iptv.ToBitmap(),
                    BackgroundColor = rowIdx == 1 ? Color.FromArgb(0, 20, 0) : Color.Black
                };

                var channelProgrammes = TvCore.Guide.FindAll(x => x.Channel == channel.Id);

                const int dergValue = 20;

                if (channelProgrammes.Count == 0)
                {
                    newRow.Programmes.Add(new GuideTextModel
                    {
                        Text = "Not Available",
                        BackgroundColor = Color.FromArgb(dergValue, dergValue, dergValue),
                        Column = 4,
                        ColSpan = newGuideModel.TotalColumns - 4
                    });

                    newGuideModel.Rows.Add(newRow);

                    continue;
                }

                var columnsLeft = newGuideModel.TotalColumns - 4;
                var colIdx = 4;

                while (columnsLeft > 0)
                {
                    var channelCurrentProgramme = channelProgrammes.Find(x => x.Start <= columnTime && x.Stop >= columnTime);

                    if (channelCurrentProgramme == null)
                    {
                        var left = columnsLeft;

                        newRow.Programmes.Add(new GuideTextModel
                        {
                            Text = "Not Available",
                            BackgroundColor = Color.FromArgb(dergValue, dergValue, dergValue),
                            Column = colIdx,
                            ColSpan = left
                        });

                        columnsLeft = 0;

                        newGuideModel.Rows.Add(newRow);

                        continue;
                    }

                    var timeLeft = (int)Math.Ceiling((channelCurrentProgramme.Stop - columnTime).TotalMinutes / 10d);
                    var columnsUsed = timeLeft > columnsLeft ? columnsLeft : timeLeft;

                    var isCurrentProgramme = channelCurrentProgramme.Start <= DateTime.UtcNow && channelCurrentProgramme.Stop >= DateTime.UtcNow;

                    newRow.Programmes.Add(new GuideTextModel
                    {
                        Text = channelCurrentProgramme.Title,
                        Tag = channelCurrentProgramme,
                        BackgroundColor = isCurrentProgramme ? Color.DarkSlateGray : Color.DimGray,
                        Column = colIdx,
                        ColSpan = columnsUsed
                    });
                    
                    columnsLeft -= columnsUsed;
                    colIdx += columnsUsed;
                    columnTime = channelCurrentProgramme.Stop.AddMinutes(1);
                }

                newGuideModel.Rows.Add(newRow);
            }

            lock (_guideModelLock)
            {
                _guideModel = newGuideModel;
            }

            this.InvokeIfRequired(() => Draw(drawAnyway));
        }

        private void Draw(bool drawAnyway = false)
        {
            lock (_guideModelLock)
            {
                if (!drawAnyway && RowCount == _guideModel.TotalRows && ColumnCount == _guideModel.TotalColumns)
                {
                    return;
                }

                SuspendLayout();

                Controls.Clear();

                RowCount = _guideModel.TotalRows;

                for (var i = 1; i <= RowCount; i++)
                {
                    RowStyles.Add(new RowStyle(SizeType.Percent, defaultStylePercentage));
                }

                ColumnCount = _guideModel.TotalColumns;

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
                    Text = _guideModel.HeaderTitle
                };

                Controls.Add(todayLabel, 0, 0);
                SetColumnSpan(todayLabel, 4);

                // Headers
                foreach (var header in _guideModel.Headers)
                {
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
                        Tag = header.Tag,
                        Text = header.Text
                    };

                    control.Click += (s, a) =>
                    {
                        if (control.Tag == null)
                        {
                            return;
                        }

                        _timeCursorOn = true;
                        _timeCursor = (DateTime) control.Tag;

                        DrawGuide(true);
                    };

                    Controls.Add(control, header.Column, 0);
                    SetColumnSpan(control, header.ColSpan);
                }

                // Rows
                var rowIdx = 1;

                foreach (var row in _guideModel.Rows)
                {
                    var channelNumberLabel = new BorderedLabel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = row.BackgroundColor,
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
                        Tag = row.Tag,
                        HoverEffect = true,
                        Text = row.Number
                    };

                    channelNumberLabel.Click += ChangeChanel;

                    Controls.Add(channelNumberLabel, 0, rowIdx);

                    var channelNameControl = new BorderedLabel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = row.BackgroundColor,
                        ForeColor = Color.White,
                        Margin = Padding.Empty,
                        TextAlign = ContentAlignment.MiddleLeft,
                        TopBorder = true,
                        TopBorderColor = Color.White,
                        Tag = row.Tag,
                        HoverEffect = true,
                        Text = row.Text
                    };

                    channelNameControl.Controls.Add(new PictureBox
                    {
                        AutoSize = false,
                        BackColor = Color.White,
                        Dock = DockStyle.Right,
                        Image = row.Image,
                        MaximumSize = new Size(60, 0),
                        Margin = Padding.Empty,
                        MinimumSize = new Size(60, 0),
                        Padding = Padding.Empty,
                        SizeMode = PictureBoxSizeMode.Zoom
                    });

                    channelNameControl.Click += ChangeChanel;

                    Controls.Add(channelNameControl, 1, rowIdx);
                    SetColumnSpan(channelNameControl, 3);

                    foreach (var programme in row.Programmes)
                    {
                        var borderedLabel = new BorderedLabel
                        {
                            Dock = DockStyle.Fill,
                            BackColor = programme.BackgroundColor,
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
                            HoverEffect = true,
                            Tag = programme.Tag,
                            Text = programme.Text
                        };

                        if (programme.Tag is Programme channelCurrentProgramme)
                        {
                            borderedLabel.MouseEnter += (s, a) =>
                            {
                                if (s is IWin32Window window)
                                {
                                    _toolTip.Show(string.Empty, window, 0);
                                }
                            };

                            borderedLabel.MouseMove += (s, a) =>
                            {
                                const int offset = 5;
                                var newPoint = new Point(a.Location.X + offset, a.Location.Y + offset);

                                _toolTip.ToolTipTitle = $"{channelCurrentProgramme.Start.ToLocalTime():h:mm tt} to {channelCurrentProgramme.Stop.ToLocalTime():h:mm tt} - {channelCurrentProgramme.BlockLength * 10} minutes";

                                if (s is IWin32Window window)
                                {
                                    _toolTip.Show($"{channelCurrentProgramme.Title}\n{channelCurrentProgramme.Description}", window, newPoint);
                                }
                            };

                            borderedLabel.MouseLeave += (s, a) => { _toolTip.Hide(this); };
                        }

                        Controls.Add(borderedLabel, programme.Column, rowIdx);
                        SetColumnSpan(borderedLabel, programme.ColSpan);
                    }

                    rowIdx++;
                }
            }

            ResumeLayout(true);
        }

        private void ChangeChanel(object sender, EventArgs args)
        {
            var control = sender as Control;

            if (!(control?.Tag is Channel clickedChannel))
            {
                return;
            }

            TvCore.SetChannel((uint) TvCore.ChannelIndexList.IndexOf(clickedChannel.Index));
        }

        public void ResetView()
        {
            _timeCursorOn = false;

            DrawGuide(true);
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (lastHeaderTime.Minute != DateTime.Now.Minute)
            {
                DrawGuide(true);
            }
        }
    }
}
