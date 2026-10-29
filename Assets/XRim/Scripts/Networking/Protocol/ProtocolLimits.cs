namespace XRim.Networking.Protocol
{
    /// <summary>
    /// The largest things a message may hold (technical limits, not gameplay values). A message beyond one is refused before anything is
    /// built from it, so a broken or hostile peer cannot make the other side allocate without bound.
    /// </summary>
    public static class ProtocolLimits
    {
        /// <summary>The largest message as it travels (1 MiB). A full-length turn's result is a small fraction of it.</summary>
        public const int MaxMessageBytes = 1 << 20;

        /// <summary>The largest a packed message may grow when unpacked (8 MiB).</summary>
        public const int MaxUnpackedBytes = 8 << 20;

        /// <summary>
        /// The most points a raw stroke may hold. A finger stroke has a point every arena unit (Session 08), and every weapon's ink runs
        /// out long before 4096 units of drawing, so a longer stroke changes nothing that executes.
        /// </summary>
        public const int MaxStrokePoints = 4096;

        /// <summary>The most entries of a short list: loadout weapons, signature moves, limits, statuses, lost weapons.</summary>
        public const int MaxShortListItems = 64;

        /// <summary>The most frames a turn's timeline may hold (a 1.5 s turn at 240 Hz has 361).</summary>
        public const int MaxTimelineFrames = 8192;

        /// <summary>The most events a turn may hold.</summary>
        public const int MaxTurnEvents = 8192;

        /// <summary>The most severed limbs on the floor at once (two dummies have eight severable limbs).</summary>
        public const int MaxSeveredLimbs = 16;

        /// <summary>The longest id, name or short text, in UTF-8 bytes.</summary>
        public const int MaxTextBytes = 1024;

        /// <summary>The longest settings snapshot text, in UTF-8 bytes (the rules and simulation settings as the server plays them).</summary>
        public const int MaxSettingsTextBytes = 4 << 20;
    }
}
