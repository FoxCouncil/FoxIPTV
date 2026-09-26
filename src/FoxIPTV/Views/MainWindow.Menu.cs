// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Classes;
    using LibVLCSharp.Shared;

    /// <summary>The menus, tray icon and hot keys of the main window</summary>
    public partial class MainWindow
    {
        /// <summary>The notification area icon</summary>
        private TrayIcon _trayIcon;

        /// <summary>The tray menu's items that change with state</summary>
        private NativeMenuItem _trayWindowState;

        private NativeMenuItem _trayMute;

        /// <summary>Wire the right click menu</summary>
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
            MenuItemLibrary.Click += (sender, args) => ToggleLibraryForm();
            MenuItemSwitchProvider.Click += (sender, args) => SwitchProvider();

            MenuItemAbout.Click += (sender, args) => new AboutWindow().ShowDialog(this);
            MenuItemQuit.Click += (sender, args) => Quit();

            MainContextMenu.Opening += ContextMenu_Opening;

            LogMenuClicks(MainContextMenu, string.Empty);
        }

        /// <summary>The notification area icon: a left click shows or hides the window, the menu has the everyday items</summary>
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
            Item("Guide", ToggleGuideForm);
            Item("Channel Editor", ToggleChannelsForm);
            Item("Library", ToggleLibraryForm);
            menu.Items.Add(new NativeMenuItemSeparator());
            Item("Switch Provider...", SwitchProvider);
            Item("About", () => new AboutWindow().Show());
            Item("Quit", Quit);

            menu.Opening += (sender, args) =>
            {
                _trayWindowState.Header = IsVisible ? "Hide Window" : "Show Window";
                _trayMute.Header = _muted ? "Unmute" : "Mute";
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

            // A change of the Windows theme may be a change of the taskbar colour too
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

            if (TvCore.CurrentMedia != null)
            {
                MenuItemChannelNumber.Header = "On Demand";
                MenuItemChannelName.Header = TvCore.CurrentMediaTitle;
            }
            else if (TvCore.CurrentChannel != null)
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
            MenuItemGuide.IsEnabled = hasChannels;
            MenuItemChannelEditor.IsEnabled = hasChannels;

            MenuItemLibrary.IsEnabled = TvCore.CurrentLibrary != null;
            MenuItemLibrary.IsChecked = _libraryWindow?.IsVisible ?? false;

            MenuItemWindowState.IsChecked = IsVisible;
            MenuItemWindowState.Header = IsVisible ? "Hide Window" : "Show Window";

            MenuItemMute.IsChecked = _muted;

            MenuItemStatusBar.IsChecked = StatusBar.IsVisible;

            MenuItemClosedCaptioning.IsEnabled = _ccDetected;
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

        /// <summary>Set the window opacity and remember it</summary>
        private void SetOpacity(double opacity)
        {
            opacity = Math.Round(Math.Max(0.1, Math.Min(1, opacity)), 1);

            TvCore.Settings.Opacity = opacity;
            TvCore.Settings.Save();

            WindowOpacity.Apply(this, opacity);

            TvCore.LogDebug($"[.NET] Opacity Set {opacity}");
        }

        /// <summary>The right click menu aspect ratio change handler</summary>
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

            Vlc(player =>
            {
                player.AspectRatio = aspectRatioVal.Length == 0 ? null : aspectRatioVal;
                _aspectRatio = player.AspectRatio;
            });

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

            Vlc(player =>
            {
                player.SetChannel((AudioOutputChannel)stereoMode);
                _audioChannel = stereoMode;
            });
        }

        /// <summary>The hot keys, the same set FoxIPTV has always had</summary>
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

        /// <summary>Toggle the visibility for the library window</summary>
        private void ToggleLibraryForm()
        {
            if (TvCore.CurrentLibrary == null)
            {
                return;
            }

            if (LibraryWindowInstance.IsVisible)
            {
                TvCore.Settings.LibraryOpen = false;
                LibraryWindowInstance.Hide();
            }
            else
            {
                TvCore.Settings.LibraryOpen = true;
                LibraryWindowInstance.Show(this);
            }

            TvCore.Settings.Save();
        }

        /// <summary>Toggle the visibility for the channel editor</summary>
        private void ToggleChannelsForm()
        {
            if (TvCore.Channels == null || TvCore.Channels.Count == 0)
            {
                return;
            }

            if (ChannelsWindowInstance.IsVisible)
            {
                TvCore.Settings.ChannelEditorOpen = false;
                ChannelsWindowInstance.Hide();
            }
            else
            {
                TvCore.Settings.ChannelEditorOpen = true;
                ChannelsWindowInstance.Show(this);
            }

            TvCore.Settings.Save();
        }

        /// <summary>Toggle the visibility for the guide</summary>
        private void ToggleGuideForm()
        {
            if (TvCore.Channels == null || TvCore.Channels.Count == 0)
            {
                return;
            }

            if (GuideWindowInstance.IsVisible)
            {
                TvCore.Settings.GuideOpen = false;
                GuideWindowInstance.Hide();
            }
            else
            {
                TvCore.Settings.GuideOpen = true;
                GuideWindowInstance.Show(this);
            }

            TvCore.Settings.Save();
        }

        /// <summary>Go back to the provider picker by restarting the application</summary>
        private void SwitchProvider()
        {
            TvCore.LogInfo("[.NET] Switching provider, restarting");

            App.RestartRequested = true;

            TvCore.Settings.Save();

            Quit();
        }

        /// <summary>Toggle the visibility of the status bar</summary>
        private void ToggleStatusStrip()
        {
            StatusBar.IsVisible = !StatusBar.IsVisible;

            TvCore.Settings.StatusBar = StatusBar.IsVisible;
            TvCore.Settings.Save();

            AspectRatioResizeLater();
        }

        /// <summary>Toggle the window's borders</summary>
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

        /// <summary>Toggle the window's Always on Top state</summary>
        private void ToggleAlwaysOnTop()
        {
            Topmost = !Topmost;

            TvCore.Settings.AlwaysOnTop = Topmost;
            TvCore.Settings.Save();
        }

        /// <summary>Toggle the main window's visibility</summary>
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

            TvCore.Settings.Visibility = IsVisible;
            TvCore.Settings.Save();
        }

        /// <summary>Hiding a window hides every window it owns and lets go of them, and showing it again brings none back: bring back the overlay over the video and whichever of the other windows the settings say are open</summary>
        private void RestoreOwnedWindows()
        {
            // LibVLCSharp shows its overlay again when the video view's visibility changes
            VideoView.IsVisible = false;
            VideoView.IsVisible = true;

            if (_guideWindow != null && TvCore.Settings.GuideOpen && !_guideWindow.IsVisible)
            {
                _guideWindow.Show(this);
            }

            if (_channelsWindow != null && TvCore.Settings.ChannelEditorOpen && !_channelsWindow.IsVisible)
            {
                _channelsWindow.Show(this);
            }

            if (_libraryWindow != null && TvCore.Settings.LibraryOpen && !_libraryWindow.IsVisible)
            {
                _libraryWindow.Show(this);
            }
        }

        /// <summary>Resize to the video's shape once layout has caught up, so a status bar just shown has its height</summary>
        private void AspectRatioResizeLater()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(AspectRatioResize, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>Toggle the mute state</summary>
        private void ToggleMute()
        {
            Vlc(player =>
            {
                var unmute = player.Volume == 0;

                player.Volume = unmute ? DefaultVolume : 0;
                _muted = !unmute;
            });
        }

        /// <summary>Toggle Closed Captioning on or off</summary>
        private void ToggleClosedCaptioning()
        {
            TvCore.Settings.CCEnabled = !TvCore.Settings.CCEnabled;
            TvCore.Settings.Save();

            _ccDetected = false;

            CcStatusLabel.Opacity = TvCore.Settings.CCEnabled ? 1 : 0.35;
        }

        /// <summary>Set fullscreen to a specific state</summary>
        /// <param name="value">True for fullscreen, false for the window as it was</param>
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
