// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.UWP
{
    using FoxIPTV.Library;
    using LibVLCSharp.Shared;
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Windows.ApplicationModel.Core;
    using Windows.Foundation;
    using Windows.Media;
    using Windows.Storage.Streams;
    using Windows.UI.Core;
    using Windows.UI.ViewManagement;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    /// <summary>An empty page that can be used on its own or navigated to within a Frame.</summary>
    public sealed partial class MainPage : Page
    {
        private UiDataModel _currentUiDataModel = new UiDataModel();

        private LibVLC _libVLC;

        private Media _currentMedia;

        private MediaPlayer _mediaPlayer;

        private LoginContentDialog _loginDialog;

        private SystemMediaTransportControls _systemControls;

        public MainPage()
        {
            InitializeComponent();

            var volumeControl = CoreAudio.GetVolumeObject();

            // Hook up app to system transport controls.
            _systemControls = SystemMediaTransportControls.GetForCurrentView();

            _systemControls.ButtonPressed += SystemControls_ButtonPressed;
            _systemControls.PropertyChanged += (s, e) => Log.Debug($"SMTC: PropertyChanged {e.Property}");

            _systemControls.IsEnabled = false;

            Loaded += (s, e) =>
            {
                InitVideo();

                InitDialogs();

                _mediaPlayer.EndReached += async (sender, evt) =>
                {
                    await CallOnMainViewUiThreadAsync(() =>
                    {
                        if (_currentMedia != null)
                        {
                            _mediaPlayer.Play(_currentMedia);
                        }
                    });
                };

                Library.Core.StateChange += Core_StateChange;

                Core_StateChange(CoreState.Initializing);
            };

            DataContext = _currentUiDataModel;
        }

        private void SystemControls_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            throw new NotImplementedException();
        }

        private void InitVideo()
        {
            _libVLC = new LibVLC(VideoView.SwapChainOptions);

            _mediaPlayer = new MediaPlayer(_libVLC);

            VideoView.MediaPlayer = _mediaPlayer;
        }

        private void InitDialogs()
        {
            _loginDialog = new LoginContentDialog();
        }

        private async void Core_StateChange(CoreState newState)
        {
            switch (newState)
            {
                case CoreState.Initializing:
                {
                    HookControls();

                    if (Library.Core.State == CoreState.Initializing)
                    {
                        Library.Core.ControlReady();
                    }
                }
                break;

                case CoreState.Login:
                {
                    _systemControls.PlaybackStatus = MediaPlaybackStatus.Closed;

                    var accounts = Library.Core.GetAccounts();

                    if (accounts.Count == 0)
                    {
                        ShowAnimatedBackground();

                        await _loginDialog.ShowAsync();
                        // Show Login ContentDialog
                    }
                    else
                    {
                        await Library.Core.AccountsLogin();

                        // Do something fancy, show accounts
                        // or auto-login
                    }
                }
                break;

                case CoreState.Load:
                {
                    _systemControls.PlaybackStatus = MediaPlaybackStatus.Changing;

                    HideAnimatedBackground();
                }
                break;

                case CoreState.Playback:
                {
                    var _currentChannel = Library.Core.ControlGetChannel();

                    _currentUiDataModel.SetCurrentChannel(_currentChannel);

                    _systemControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    _systemControls.IsEnabled = true;

                    // Get the updater.
                    var updater = _systemControls.DisplayUpdater;

                    // Music metadata.
                    updater.Type = MediaPlaybackType.Video;

                    updater.VideoProperties.Title = _currentChannel.Index.ToString();
                    updater.VideoProperties.Subtitle = _currentChannel.Name;

                    if (_currentChannel.Icon != null)
                    {
                        // Set the album art thumbnail.
                        // RandomAccessStreamReference is defined in Windows.Storage.Streams
                        updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(_currentChannel.Icon);
                    }
                    else
                    {
                        updater.Thumbnail = null;
                    }

                    // Update the system media transport controls.
                    updater.Update();

                    ApplicationView.GetForCurrentView().Title = $"CH:{Library.Core.CurrentChannel} - {_currentChannel.Name}";

                    await CallOnMainViewUiThreadAsync(() =>
                    {
                        _mediaPlayer.Stop();

                        _currentMedia = new Media(_libVLC, _currentChannel.Stream.ToString(), FromType.FromLocation);

                        _mediaPlayer.Play(_currentMedia);
                    });
                }
                break;
            }
        }

        private void HookControls()
        {
            Window.Current.CoreWindow.KeyUp += CoreWindow_KeyUp;
        }

        private void CoreWindow_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            if (Library.Core.State != CoreState.Playback)
            {
                return;
            }

            switch (args.VirtualKey)
            {
                case Windows.System.VirtualKey.PageUp:
                {
                    Library.Core.ControlChannelUp();
                }
                break;

                case Windows.System.VirtualKey.PageDown:
                {
                    Library.Core.ControlChannelDown();
                }
                break;

                case Windows.System.VirtualKey.Space:
                {
                    
                }
                break;

                default:
                {
                    Log.Message(args.VirtualKey.ToString());
                }
                break;
            }
        }

        private void ShowAnimatedBackground()
        {
            ApplicationView.GetForCurrentView().TryResizeView(new Size { Width = 1920, Height = 1080 });

            _currentMedia = new Media(_libVLC, $"{Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "bgloop.mp4")}");

            _mediaPlayer.Play(_currentMedia);
        }

        private void HideAnimatedBackground()
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Media = null;

            _currentMedia = null;
        }

        public static async Task CallOnUiThreadAsync(CoreDispatcher dispatcher, DispatchedHandler handler) => await dispatcher.RunAsync(CoreDispatcherPriority.Normal, handler);

        public static async Task CallOnMainViewUiThreadAsync(DispatchedHandler handler) => await CallOnUiThreadAsync(CoreApplication.MainView.CoreWindow.Dispatcher, handler);
    }
}
