using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// A player joins the server. A new player sends no token; a player coming back after a dropped connection sends the token
    /// <see cref="MatchFoundMessage"/> gave it, and the server puts it back in its match (GDD §3 disconnects).
    /// </summary>
    public sealed class ClientHelloMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.ClientHello;

        /// <summary>The account's hand (GDD §4): the dummy holds its weapon in it.</summary>
        public Handedness Handedness { get; }

        /// <summary>The signature moves the player brings (GDD §8): those the account owns (Session 18).</summary>
        public IReadOnlyList<SignatureMoveId> Signatures { get; }

        /// <summary>Empty for a new player; the token from <see cref="MatchFoundMessage"/> to rejoin a match.</summary>
        public string ReconnectToken { get; }

        public ClientHelloMessage(Handedness handedness, IReadOnlyList<SignatureMoveId> signatures, string reconnectToken = "")
        {
            Handedness = handedness;
            Signatures = Guard.NotNull(signatures, nameof(signatures));
            ReconnectToken = reconnectToken ?? string.Empty;
        }
    }
}
