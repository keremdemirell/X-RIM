using XRim.Core;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// The server paired this player with an opponent. It says which side the player plays, gives the token to rejoin with after a dropped
    /// connection, and sends the rules and simulation settings the server plays the match with, so the phone previews strokes, ink and the
    /// loadout pick with the same numbers (<see cref="ISettingsTextCodec"/>).
    /// </summary>
    public sealed class MatchFoundMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.MatchFound;

        public Side YourSide { get; }

        public string ReconnectToken { get; }

        /// <summary>The settings snapshot as text, written and read by an <see cref="ISettingsTextCodec"/>.</summary>
        public string SettingsText { get; }

        public MatchFoundMessage(Side yourSide, string reconnectToken, string settingsText)
        {
            YourSide = yourSide;
            ReconnectToken = reconnectToken ?? string.Empty;
            SettingsText = Guard.NotNull(settingsText, nameof(settingsText));
        }
    }
}
