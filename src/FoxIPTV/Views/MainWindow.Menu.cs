// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Globalization;
    using System.Linq;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Classes;
    using Playback;

    public partial class MainWindow
    {
        private TrayIcon _trayIcon;

        private NativeMenuItem _trayWindowState;

        private NativeMenuItem _trayMute;

        private NativeMenuItem _trayGuide;

        private static bool HasGuide => TvCore.Guide != null && TvCore.Guide.Count > 0;

        private void InitializeContextMenu()
        {
            MenuItemChannelUp.Click += (sender, args) => TvCore.ChangeChannel(true);
            MenuItemChannelDown.Click += (sender, args) => TvCore.ChangeChannel(false);

            MenuItemWindowState.Click += (sender, args) => ToggleVisibility();
            MenuItemMute.Click += (sender, args) => ToggleMute();

            MenuItemStatusBar.Click += (sender, args) => ToggleStatusStrip();
            MenuItemClosedCaptioning.Click += (sender, args) => ToggleClosedCaptioning();
            MenuItemBorders.Click += (sender, args) => ToggleBorders();
            MenuItemFullscreen.Click += (sender, args) => FullscreenSet(!TvCore.Settings.Fullscreen);
            MenuItemAlwaysOnTop.Click += (sender, args) => ToggleAlwaysOnTop();

            foreach (var item in MenuItemTransparency.Items.OfType<MenuItem>())
            {
                item.Click += MenuItemTransparency_Clicked;
            }

            foreach (var item in MenuItemAspectRatio.Items.OfType<MenuItem>())
            {
                item.Click += MenuItemAspectRatio_Clicked;
            }

            foreach (var item in MenuItemStereoMode.Items.OfType<MenuItem>())
            {
                item.Click += MenuItemStereoMode_Click;
            }

            MenuItemGuide.Click += (sender, args) => ToggleGuideForm();
            MenuItemChannelEditor.Click += (sender, args) => ToggleChannelsForm();
            MenuItemSwitchProvider.Click += (sender, args) => SwitchProvider();
            MenuItemUpdate.Click += async (sender, args) => await UpdatePrompt.Run(this, Restart);

            MenuItemAbout.Click += (sender, args) => new AboutWindow().ShowDialog(this);
            MenuItemQuit.Click += (sender, args) => Quit();

            MainContextMenu.Opening += ContextMenu_Opening;

            LogMenuClicks(MainContextMenu, string.Empty);
        }

        private void InitializeTrayIcon()
        {
            var menu = new NativeMenu();

            NativeMenuItem Item(string header, Action action)
            {
                var item = new NativeMenuItem(header);

                item.Click += (sender, args) =>
                {
                    TvCore.LogInfo($"[UI] Tray menu: {header}");

                    action();
                };

                menu.Items.Add(item);

                return item;
            }

            Item("Channel Up", () => TvCore.ChangeChannel(true));
            Item("Channel Down", () => TvCore.ChangeChannel(false));
            menu.Items.Add(new NativeMenuItemSeparator());
            _trayWindowState = Item("Hide Window", ToggleVisibility);
            _trayMute = Item("Mute", ToggleMute);
            menu.Items.Add(new NativeMenuItemSeparator());
            _trayGuide = Item("Guide", ToggleGuideForm);
            Item("Channel Editor", ToggleChannelsForm);
            menu.Items.Add(new NativeMenuItemSeparator());
            Item("Switch Provider...", SwitchProvider);
            Item("About", () => new AboutWindow().Show());
            Item("Quit", Quit);

            menu.Opening += (sender, args) =>
            {
                _trayWindowState.Header = IsVisible ? "Hide Window" : "Show Window";
                _trayMute.Header = _player.Muted ? "Unmute" : "Mute";
                _trayGuide.IsEnabled = HasGuide;
            };

            _trayIcon = new TrayIcon
            {
                Icon = TrayIconArt.ForTaskbar(),
                ToolTipText = "Fox IPTV",
                Menu = menu,
                IsVisible = true
            };

            _trayIcon.Clicked += (sender, args) =>
            {
                TvCore.LogInfo("[UI] Tray icon clicked");

                ToggleVisibility();
            };

            if (Avalonia.Application.Current != null)
            {
                Avalonia.Application.Current.ActualThemeVariantChanged += (sender, args) => _trayIcon.Icon = TrayIconArt.ForTaskbar();
            }
        }

        /// <summary>The right click menu's opening event, used to set the various current states</summary>
        private void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isInitialized)
            {
                e.Cancel = true;

                return;
            }

            if (TvCore.CurrentChannel != null)
            {
                MenuItemChannelNumber.Header = $"CH: {TvCore.CurrentChannel.Index}";
                MenuItemChannelName.Header = TvCore.CurrentChannel.Name;
            }
            else
            {
                MenuItemChannelNumber.Header = TvCore.CurrentService?.Title ?? string.Empty;
                MenuItemChannelName.Header = "No channels";
            }

            var hasChannels = TvCore.Channels != null && TvCore.Channels.Count > 0;

            MenuItemChannelUp.IsEnabled = hasChannels;
            MenuItemChannelDown.IsEnabled = hasChannels;
            MenuItemGuide.IsEnabled = hasChannels && HasGuide;
            MenuItemChannelEditor.IsEnabled = hasChannels;

            MenuItemWindowState.IsChecked = IsVisible;
            MenuItemWindowState.Header = IsVisible ? "Hide Window" : "Show Window";

            MenuItemMute.IsChecked = _player.Muted;

            MenuItemStatusBar.IsChecked = StatusBar.IsVisible;

            MenuItemClosedCaptioning.IsEnabled = _ccAvailable;
            MenuItemClosedCaptioning.IsChecked = TvCore.Settings.CCEnabled;

            MenuItemBorders.IsEnabled = !IsFullscreen;
            MenuItemBorders.IsChecked = TvCore.Settings.Borders;
            MenuItemFullscreen.IsChecked = IsFullscreen;
            MenuItemAlwaysOnTop.IsChecked = Topmost;

            MenuItemTransparency.IsEnabled = WindowOpacity.IsSupported;
            MenuItemTransparency.IsChecked = TvCore.Settings.Opacity < 1.0;

            foreach (var submenu in MenuItemTransparency.Items.OfType<MenuItem>())
            {
                var opacityVal = double.Parse(submenu.Tag.ToString(), CultureInfo.InvariantCulture);

                submenu.IsChecked = Math.Abs(TvCore.Settings.Opacity - opacityVal) < 0.001;
            }

            var aspectRatio = _aspectRatio;

            foreach (var submenu in MenuItemAspectRatio.Items.OfType<MenuItem>())
            {
                var aspectRatioVar = submenu.Tag.ToString();

                submenu.IsChecked = (aspectRatio == null && aspectRatioVar == "null") || aspectRatio == aspectRatioVar;
            }

            var stereoMode = _audioChannel;

            foreach (var submenu in MenuItemStereoMode.Items.OfType<MenuItem>())
            {
                submenu.IsChecked = stereoMode == int.Parse(submenu.Tag.ToString(), CultureInfo.InvariantCulture);
            }

            MenuItemUpdate.IsVisible = Updater.IsEnabled;
            MenuItemUpdate.Header = Updater.Available != null ? "Update Available" : "Check For Update";

            MenuItemGuide.IsChecked = _guideWindow?.IsVisible ?? false;
            MenuItemChannelEditor.IsChecked = _channelsWindow?.IsVisible ?? false;
        }

        /// <summary>The right click menu Opacity change handler</summary>
        private void MenuItemTransparency_Clicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!(sender is MenuItem item))
            {
                return;
            }

            var opacityVal = double.Parse(item.Tag.ToString(), CultureInfo.InvariantCulture);

            SetOpacity(opacityVal);
        }

        private void SetOpacity(double opacity)
        {
            opacity = Math.Round(Math.Max(0.1, Math.Min(1, opacity)), 1);

            TvCore.Settings.Opacity = opacity;
            TvCore.Settings.Save();

            WindowOpacity.Apply(this, opacity);

            TvCore.LogDebug($"[.NET] Opacity Set {opacity}");
        }

        private void MenuItemAspectRatio_Clicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!(sender is MenuItem item))
            {
                return;
            }

            var aspectRatioVal = item.Tag.ToString();

            aspectRatioVal = aspectRatioVal == "null" ? string.Empty : aspectRatioVal;

            TvCore.Settings.AspectRatio = aspectRatioVal;
            TvCore.Settings.Save();

            VideoView.AspectRatio = aspectRatioVal;

            _aspectRatio = aspectRatioVal.Length == 0 ? null : aspectRatioVal;

            AspectRatioResize();
        }

        /// <summary>The right click menu Stereo mode change handler</summary>
        private void MenuItemStereoMode_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!(sender is MenuItem item))
            {
                return;
            }

            var stereoMode = int.Parse(item.Tag.ToString(), CultureInfo.InvariantCulture);

            TvCore.Settings.StereoMode = stereoMode;
            TvCore.Settings.Save();

            _player.StereoMode = (StereoMode)stereoMode;
            _audioChannel = stereoMode;
        }

        private void HotKey(KeyEventArgs e)
        {
            if (e.Handled || e.KeyModifiers != KeyModifiers.None && e.KeyModifiers != KeyModifiers.Shift)
            {
                return;
            }

            TvCore.LogInfo($"[UI] Key {e.Key}{(e.KeyModifiers == KeyModifiers.Shift ? " with Shift" : string.Empty)}");

            switch (e.Key)
            {
                case Key.Z:
                {
                    SetOpacity(TvCore.Settings.Opacity - .1);
                }
                break;

                case Key.X:
                {
                    SetOpacity(TvCore.Settings.Opacity + .1);
                }
                break;

                case Key.B:
                {
                    ToggleBorders();
                }
                break;

                case Key.A:
                {
                    ToggleAlwaysOnTop();
                }
                break;

                case Key.F:
                {
                    FullscreenSet(!TvCore.Settings.Fullscreen);
                }
                break;

                case Key.Escape:
                {
                    if (IsFullscreen)
                    {
                        FullscreenSet(false);
                    }
                }
                break;

                case Key.S:
                {
                    ToggleStatusStrip();
                }
                break;

                case Key.I:
                {
                    GuiShow();
                }
                break;

                case Key.H:
                {
                    ToggleVisibility();
                }
                break;

                case Key.G:
                {
                    ToggleGuideForm();
                }
                break;

                case Key.T:
                {
                    ToggleChannelsForm();
                }
                break;

                case Key.PageUp:
                case Key.PageDown:
                {
                    TvCore.ChangeChannel(e.Key == Key.PageUp);
                }
                break;

                case Key.Space:
                {
                    ToggleMute();
                }
                break;

                case Key.C:
                {
                    ToggleClosedCaptioning();
                }
                break;

                case Key.Enter:
                {
                    if (_numberEntryMode)
                    {
                        _numberEntryModeTimeout = 1;
                    }
                }
                break;

                case Key.Back:
                {
                    if (_numberEntryMode && _numberEntryDigits.Count > 0)
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
                }
                break;

                default:
                {
                    int digit;

                    if (e.Key >= Key.D0 && e.Key <= Key.D9)
                    {
                        digit = e.Key - Key.D0;
                    }
                    else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                    {
                        digit = e.Key - Key.NumPad0;
                    }
                    else
                    {
                        return;
                    }

                    _numberEntryMode = true;
                    _numberEntryModeTimeout = 20;
                    _numberEntryDigits.Add(digit);

                    GuiShow();
                }
                break;
            }

            e.Handled = true;
        }

        private void ToggleChannelsForm()
        {
            ToggleToolWindow(() => ChannelsWindowInstance, open => TvCore.Settings.ChannelEditorOpen = open);
        }

        private void ToggleGuideForm()
        {
            if (!HasGuide)
            {
                return;
            }

            ToggleToolWindow(() => GuideWindowInstance, open => TvCore.Settings.GuideOpen = open);
        }

        private void ToggleToolWindow(Func<Window> window, Action<bool> remember)
        {
            if (TvCore.Channels == null || TvCore.Channels.Count == 0)
            {
                return;
            }

            var open = !window().IsVisible;

            remember(open);

            if (open)
            {
                window().Show(this);
            }
            else
            {
                window().Hide();
            }

            TvCore.Settings.Save();
        }

        private void SwitchProvider()
        {
            TvCore.LogInfo("[.NET] Switching provider, restarting");

            Restart();
        }

        private void Restart()
        {
            App.RestartRequested = true;

            TvCore.Settings.Save();

            Quit();
        }

        private void ToggleStatusStrip()
        {
            StatusBar.IsVisible = !StatusBar.IsVisible;

            TvCore.Settings.StatusBar = StatusBar.IsVisible;
            TvCore.Settings.Save();

            AspectRatioResizeLater();
        }

        private void ToggleBorders()
        {
            if (IsFullscreen)
            {
                return;
            }

            TvCore.Settings.Borders = !TvCore.Settings.Borders;

            SystemDecorations = TvCore.Settings.Borders ? SystemDecorations.Full : SystemDecorations.None;

            TvCore.Settings.Save();

            AspectRatioResizeLater();
        }

        private void ToggleAlwaysOnTop()
        {
            Topmost = !Topmost;

            TvCore.Settings.AlwaysOnTop = Topmost;
            TvCore.Settings.Save();
        }

        private void ToggleVisibility()
        {
            if (IsVisible)
            {
                TvCore.LogDebug("[.NET] Hiding main window");
                Hide();
            }
            else
            {
                TvCore.LogDebug("[.NET] Showing main window");
                Show();
                Activate();

                RestoreOwnedWindows();
            }
        }

        private void RestoreOwnedWindows()
        {
            if (_guideWindow != null && TvCore.Settings.GuideOpen && !_guideWindow.IsVisible)
            {
                _guideWindow.Show(this);
            }

            if (_channelsWindow != null && TvCore.Settings.ChannelEditorOpen && !_channelsWindow.IsVisible)
            {
                _channelsWindow.Show(this);
            }
        }

        private void AspectRatioResizeLater()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(AspectRatioResize, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>Toggle the mute state</summary>
        private void ToggleMute()
        {
            _player.Muted = !_player.Muted;

            TvCore.LogInfo($"[Audio] {(_player.Muted ? "Muted" : "Unmuted")}");
        }

        /// <summary>Toggle Closed Captioning on or off</summary>
        private void ToggleClosedCaptioning()
        {
            TvCore.Settings.CCEnabled = !TvCore.Settings.CCEnabled;
            TvCore.Settings.Save();

            TvCore.LogInfo($"[CC] Setting turned {(TvCore.Settings.CCEnabled ? "on" : "off")}");

            CcStatusLabel.Opacity = TvCore.Settings.CCEnabled ? 1 : 0.35;

            ShowCaption();
        }

        /// <summary>Set fullscreen to a specific state</summary>
        public void FullscreenSet(bool value)
        {
            if (value == IsFullscreen)
            {
                return;
            }

            TvCore.Settings.Fullscreen = value;

            if (value)
            {
                TvCore.Settings.WindowOldState = WindowState.ToString();

                WindowState = WindowState.FullScreen;
            }
            else
            {
                WindowState = Enum.TryParse<WindowState>(TvCore.Settings.WindowOldState, out var old) && old != WindowState.FullScreen ? old : WindowState.Normal;
            }

            TvCore.Settings.Save();

            IsFullscreen = value;

            AspectRatioResizeLater();
        }
    }
}
