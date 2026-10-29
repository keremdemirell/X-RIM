namespace XRim.Networking.Protocol
{
    /// <summary>Why a received message was refused (<see cref="ProtocolException"/>).</summary>
    public enum ProtocolError
    {
        /// <summary>Written by a different version of the protocol (older or newer code).</summary>
        VersionMismatch = 0,

        /// <summary>It ends before its contents do.</summary>
        Truncated = 1,

        /// <summary>Larger than the protocol allows, as sent or once unpacked, or a list longer than its limit.</summary>
        TooLarge = 2,

        /// <summary>A message kind this version does not know.</summary>
        UnknownKind = 3,

        /// <summary>Contents that cannot be right: an unknown value, an impossible combination, bytes left over.</summary>
        Malformed = 4,
    }
}
