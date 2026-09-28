// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Services
{
    using System;
    using System.Threading.Tasks;
    using Classes;

    public interface ILiveTuner
    {
        bool CanTune { get; }

        Task<Uri> Tune(Channel channel);
    }
}
