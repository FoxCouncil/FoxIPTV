// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Playback
{
    using System;

    public sealed class DriftControl
    {
        public const double MaxCorrection = 0.002;

        public const double SettleSeconds = 30;

        public const double AverageSeconds = 60;

        public const double HoldAfterSeconds = SettleSeconds + AverageSeconds * 3;

        public const double CorrectionPerSecondOffTarget = 0.001;

        private double _playing;

        public double Speed { get; private set; } = 1;

        public double Target { get; private set; } = double.NaN;

        public double Average { get; private set; } = double.NaN;

        public bool IsHolding => !double.IsNaN(Target);

        public double Update(double elapsed, double buffered)
        {
            if (elapsed <= 0 || double.IsNaN(buffered))
            {
                return Speed;
            }

            _playing += elapsed;

            if (_playing < SettleSeconds)
            {
                return Speed;
            }

            Average = double.IsNaN(Average) ? buffered : Average + (buffered - Average) * (1 - Math.Exp(-elapsed / AverageSeconds));

            if (!IsHolding)
            {
                if (_playing < HoldAfterSeconds)
                {
                    return Speed;
                }

                Target = Average;
            }

            Speed = 1 + Math.Clamp((Average - Target) * CorrectionPerSecondOffTarget, -MaxCorrection, MaxCorrection);

            return Speed;
        }
    }
}
