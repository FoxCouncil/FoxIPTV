// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.UWP
{
    using FoxIPTV.Library;
    using System.ComponentModel;

    public class UiDataModel : INotifyPropertyChanged
    {
        private Channel _currentChannel;

        public event PropertyChangedEventHandler PropertyChanged;

        public string CurrentChannelNumberString => _currentChannel?.Index.ToString() ?? string.Empty;

        public string CurrentChannelNameString => _currentChannel?.Name ?? string.Empty;

        public void SetCurrentChannel(Channel currentChannel)
        {
            _currentChannel = currentChannel;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentChannelNumberString"));

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentChannelNameString"));
        }
    }
}
