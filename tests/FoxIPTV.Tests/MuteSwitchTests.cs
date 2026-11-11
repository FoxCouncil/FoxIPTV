// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Tests
{
    using FoxIPTV.Playback;

    public sealed class MuteSwitchTests
    {
        [Fact]
        public void ANewSwitchIsNotMuted()
        {
            var mute = new MuteSwitch();

            Assert.False(mute.AppMuted);
            Assert.False(mute.SystemMuted);
            Assert.False(mute.Muted);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public void EitherMuteMutes(bool app, bool system, bool muted)
        {
            var mute = new MuteSwitch { SystemMuted = system };

            if (app)
            {
                mute.Toggle();
            }

            Assert.Equal(muted, mute.Muted);
        }

        [Fact]
        public void ASystemMuteMutesAnUnmutedApp()
        {
            var mute = new MuteSwitch { SystemMuted = true };

            Assert.True(mute.Muted);
            Assert.False(mute.AppMuted);
        }

        [Fact]
        public void ASystemUnmuteReturnsToAnUnmutedApp()
        {
            var mute = new MuteSwitch { SystemMuted = true };

            mute.SystemMuted = false;

            Assert.False(mute.Muted);
        }

        [Fact]
        public void ASystemUnmuteReturnsToAMutedApp()
        {
            var mute = new MuteSwitch();

            mute.Toggle();
            mute.SystemMuted = true;
            mute.SystemMuted = false;

            Assert.True(mute.AppMuted);
            Assert.True(mute.Muted);
        }

        [Fact]
        public void AnAppMuteNeverChangesTheSystemMute()
        {
            var mute = new MuteSwitch();

            mute.Toggle();

            Assert.False(mute.SystemMuted);

            mute.SystemMuted = true;
            mute.Toggle();

            Assert.True(mute.SystemMuted);
        }

        [Fact]
        public void AnAppMuteSetDuringASystemMuteShowsAfterTheSystemUnmutes()
        {
            var mute = new MuteSwitch { SystemMuted = true };

            mute.Toggle();

            Assert.True(mute.Muted);

            mute.SystemMuted = false;

            Assert.True(mute.Muted);
        }

        [Fact]
        public void AnAppUnmuteDuringASystemMuteStaysMutedUntilTheSystemUnmutes()
        {
            var mute = new MuteSwitch();

            mute.Toggle();
            mute.SystemMuted = true;
            mute.Toggle();

            Assert.False(mute.AppMuted);
            Assert.True(mute.Muted);

            mute.SystemMuted = false;

            Assert.False(mute.Muted);
        }
    }
}
