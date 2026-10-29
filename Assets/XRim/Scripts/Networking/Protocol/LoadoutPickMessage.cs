using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// The player's loadout, picked blind (GDD §6, Session 14): the server holds it until both players have picked, then reveals both in
    /// <see cref="MatchStartedMessage"/>. The first weapon is the one the match starts with.
    /// </summary>
    public sealed class LoadoutPickMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.LoadoutPick;

        public IReadOnlyList<WeaponId> Weapons { get; }

        public LoadoutPickMessage(IReadOnlyList<WeaponId> weapons)
        {
            Weapons = Guard.NotNull(weapons, nameof(weapons));
        }
    }
}
