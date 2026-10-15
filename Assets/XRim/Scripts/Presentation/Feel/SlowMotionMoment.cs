namespace XRim.Presentation.Feel
{
    /// <summary>How one kind of big moment is slowed down: how slow, how long on screen, and how much of it comes before the moment.</summary>
    public readonly struct SlowMotionMoment
    {
        /// <summary>Playback speed through the slowed stretch (1 = real time, 0.25 = a quarter speed).</summary>
        public float Speed { get; }

        /// <summary>How long the slowed stretch lasts on screen, in real seconds (0 = no slow motion for this kind).</summary>
        public float RealSeconds { get; }

        /// <summary>The share of the slowed stretch before the moment: the anticipation (0 = it starts at the moment).</summary>
        public float LeadFraction { get; }

        public SlowMotionMoment(float speed, float realSeconds, float leadFraction)
        {
            Speed = speed;
            RealSeconds = realSeconds;
            LeadFraction = leadFraction;
        }

        /// <summary>Seconds of the turn the slowed stretch covers: its real seconds times its speed.</summary>
        public float TurnSeconds => RealSeconds * Speed;

        public bool IsActive => RealSeconds > 0f && Speed > 0f && Speed < 1f;
    }
}
