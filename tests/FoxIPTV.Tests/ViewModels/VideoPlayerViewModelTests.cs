namespace FoxIPTV.Tests.ViewModels;

using FoxIPTV.ViewModels;

public class VideoPlayerViewModelTests
{
    private readonly VideoPlayerViewModel _vm = new();

    private static ChannelItemViewModel MakeChannel(string id = "ch1", string name = "Test Channel", string url = "http://test/stream.m3u8") =>
        new() { Id = id, Name = name, Country = "US", StreamUrl = url, Categories = ["News"] };

    [Fact]
    public void InitialState_IsNotPlaying()
    {
        Assert.False(_vm.IsPlaying);
        Assert.False(_vm.IsBuffering);
        Assert.False(_vm.IsPaused);
        Assert.False(_vm.IsMuted);
        Assert.Equal(80, _vm.Volume);
        Assert.Null(_vm.CurrentStreamUrl);
        Assert.Equal(string.Empty, _vm.CurrentChannelName);
    }

    [Fact]
    public void PlayChannel_SetsPlayingState()
    {
        var channel = MakeChannel();

        _vm.PlayChannel(channel);

        Assert.True(_vm.IsPlaying);
        Assert.True(_vm.IsBuffering);
        Assert.False(_vm.IsPaused);
        Assert.Equal("http://test/stream.m3u8", _vm.CurrentStreamUrl);
        Assert.Equal("Test Channel", _vm.CurrentChannelName);
        Assert.Equal(0, _vm.BufferProgress);
    }

    [Fact]
    public void PlayChannel_RaisesPlayRequestedEvent()
    {
        var channel = MakeChannel(url: "http://example.com/live.m3u8");
        channel = new ChannelItemViewModel
        {
            Id = "ch1", Name = "Test", Country = "US", StreamUrl = "http://example.com/live.m3u8",
            Categories = ["News"], UserAgent = "MyAgent", Referrer = "http://ref.com"
        };

        string? receivedUrl = null;
        string? receivedUa = null;
        string? receivedRef = null;
        _vm.PlayRequested += (url, ua, referrer) => { receivedUrl = url; receivedUa = ua; receivedRef = referrer; };

        _vm.PlayChannel(channel);

        Assert.Equal("http://example.com/live.m3u8", receivedUrl);
        Assert.Equal("MyAgent", receivedUa);
        Assert.Equal("http://ref.com", receivedRef);
    }

    [Fact]
    public void OnBuffering_UpdatesProgress()
    {
        _vm.PlayChannel(MakeChannel());

        _vm.OnBuffering(50f);

        Assert.Equal(50, _vm.BufferProgress);
        Assert.True(_vm.IsBuffering);
    }

    [Fact]
    public void OnBuffering_At100_ClearsBuffering()
    {
        _vm.PlayChannel(MakeChannel());

        _vm.OnBuffering(100f);

        Assert.False(_vm.IsBuffering);
        Assert.Equal(100, _vm.BufferProgress);
    }

    [Fact]
    public void OnPlaying_ClearsBufferingAndPaused()
    {
        _vm.PlayChannel(MakeChannel());
        _vm.OnBuffering(50f);

        _vm.OnPlaying();

        Assert.False(_vm.IsBuffering);
        Assert.False(_vm.IsPaused);
        Assert.Equal(100, _vm.BufferProgress);
    }

    [Fact]
    public void OnPaused_SetsPausedTrue()
    {
        _vm.PlayChannel(MakeChannel());
        _vm.OnPlaying();

        _vm.OnPaused();

        Assert.True(_vm.IsPaused);
    }

    [Fact]
    public void TogglePlayPause_WhenPlaying_Pauses()
    {
        _vm.PlayChannel(MakeChannel());
        _vm.OnPlaying();

        bool pauseRequested = false;
        _vm.PauseRequested += () => pauseRequested = true;

        _vm.TogglePlayPauseCommand.Execute(null);

        Assert.True(_vm.IsPaused);
        Assert.True(pauseRequested);
    }

    [Fact]
    public void TogglePlayPause_WhenPaused_Resumes()
    {
        _vm.PlayChannel(MakeChannel());
        _vm.OnPlaying();
        _vm.OnPaused();

        bool resumeRequested = false;
        _vm.ResumeRequested += () => resumeRequested = true;

        _vm.TogglePlayPauseCommand.Execute(null);

        Assert.False(_vm.IsPaused);
        Assert.True(resumeRequested);
    }

    [Fact]
    public void ToggleMute_FlipsMutedState()
    {
        Assert.False(_vm.IsMuted);

        _vm.ToggleMuteCommand.Execute(null);
        Assert.True(_vm.IsMuted);

        _vm.ToggleMuteCommand.Execute(null);
        Assert.False(_vm.IsMuted);
    }

    [Fact]
    public void VolumeChange_RaisesVolumeChangeRequested()
    {
        int? receivedVolume = null;
        _vm.VolumeChangeRequested += vol => receivedVolume = vol;

        _vm.Volume = 42;

        Assert.Equal(42, receivedVolume);
    }

    [Fact]
    public void Mute_SendsZeroVolume()
    {
        _vm.Volume = 75;
        int? receivedVolume = null;
        _vm.VolumeChangeRequested += vol => receivedVolume = vol;

        _vm.IsMuted = true;

        Assert.Equal(0, receivedVolume);
    }

    [Fact]
    public void Unmute_RestoresVolume()
    {
        _vm.Volume = 75;
        _vm.IsMuted = true;
        int? receivedVolume = null;
        _vm.VolumeChangeRequested += vol => receivedVolume = vol;

        _vm.IsMuted = false;

        Assert.Equal(75, receivedVolume);
    }

    [Fact]
    public void Stop_ResetsAllState()
    {
        _vm.PlayChannel(MakeChannel());
        _vm.OnPlaying();

        bool stopRequested = false;
        _vm.StopRequested += () => stopRequested = true;

        _vm.StopCommand.Execute(null);

        Assert.False(_vm.IsPlaying);
        Assert.False(_vm.IsBuffering);
        Assert.False(_vm.IsPaused);
        Assert.Equal(0, _vm.BufferProgress);
        Assert.Null(_vm.CurrentStreamUrl);
        Assert.Equal(string.Empty, _vm.CurrentChannelName);
        Assert.Equal(string.Empty, _vm.StreamInfo);
        Assert.Equal(string.Empty, _vm.VideoResolution);
        Assert.Equal(string.Empty, _vm.VideoCodecName);
        Assert.Equal(string.Empty, _vm.AudioCodecName);
        Assert.Equal(string.Empty, _vm.AudioChannelLayout);
        Assert.Equal(0, _vm.AudioTrackCount);
        Assert.Equal(0, _vm.SubtitleTrackCount);
        Assert.Empty(_vm.AudioTracks);
        Assert.Empty(_vm.SubtitleTracks);
        Assert.True(stopRequested);
    }

    [Fact]
    public void ShouldShowOverlay_WhenOverlayVisible_ReturnsTrue()
    {
        _vm.IsOverlayVisible = true;
        Assert.True(_vm.ShouldShowOverlay);
    }

    [Fact]
    public void ShouldShowOverlay_WhenBuffering_ReturnsTrue()
    {
        _vm.IsOverlayVisible = false;
        _vm.PlayChannel(MakeChannel());
        Assert.True(_vm.IsBuffering);
        Assert.True(_vm.ShouldShowOverlay);
    }

    [Fact]
    public void ShouldShowOverlay_WhenPaused_ReturnsTrue()
    {
        _vm.IsOverlayVisible = false;
        _vm.PlayChannel(MakeChannel());
        _vm.OnPlaying();
        _vm.OnPaused();
        Assert.True(_vm.ShouldShowOverlay);
    }

    [Fact]
    public void ShouldShowOverlay_WhenAllFalse_ReturnsFalse()
    {
        _vm.IsOverlayVisible = false;
        // IsBuffering and IsPaused are false by default
        Assert.False(_vm.ShouldShowOverlay);
    }

    [Fact]
    public void HasMultipleAudioTracks_WhenCountGreaterThan1()
    {
        Assert.False(_vm.HasMultipleAudioTracks);

        _vm.AudioTrackCount = 2;
        Assert.True(_vm.HasMultipleAudioTracks);

        _vm.AudioTrackCount = 1;
        Assert.False(_vm.HasMultipleAudioTracks);
    }

    [Fact]
    public void HasSubtitles_WhenCountGreaterThan0()
    {
        Assert.False(_vm.HasSubtitles);

        _vm.SubtitleTrackCount = 1;
        Assert.True(_vm.HasSubtitles);

        _vm.SubtitleTrackCount = 0;
        Assert.False(_vm.HasSubtitles);
    }

    [Fact]
    public void SelectAudioTrack_RaisesEvent()
    {
        int? receivedId = null;
        _vm.AudioTrackChangeRequested += id => receivedId = id;

        _vm.SelectAudioTrackCommand.Execute(3);

        Assert.Equal(3, receivedId);
    }

    [Fact]
    public void SelectSubtitleTrack_RaisesEvent()
    {
        int? receivedId = null;
        _vm.SubtitleTrackChangeRequested += id => receivedId = id;

        _vm.SelectSubtitleTrackCommand.Execute(2);

        Assert.Equal(2, receivedId);
    }

    [Fact]
    public void PlayChannel_ClearsErrorMessage()
    {
        _vm.ErrorMessage = "Previous error";

        _vm.PlayChannel(MakeChannel());

        Assert.Null(_vm.ErrorMessage);
    }

    [Fact]
    public void PlayChannel_WhileAlreadyPlaying_SwitchesChannel()
    {
        _vm.PlayChannel(MakeChannel(id: "ch1", name: "Channel 1", url: "http://a/1.m3u8"));
        _vm.OnPlaying();

        _vm.PlayChannel(MakeChannel(id: "ch2", name: "Channel 2", url: "http://b/2.m3u8"));

        Assert.Equal("Channel 2", _vm.CurrentChannelName);
        Assert.Equal("http://b/2.m3u8", _vm.CurrentStreamUrl);
        Assert.True(_vm.IsBuffering);
        Assert.False(_vm.IsPaused);
    }
}
