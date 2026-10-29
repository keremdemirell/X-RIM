using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// This player's own plan for the turn as the server holds it, sent to that player only: after the server refused a command the phone
    /// had accepted (a weapon switch that reached it inside the lock-out), and when a player rejoins mid-planning. The phone rebuilds its own
    /// copy of the plan from it. Never sent to the opponent.
    /// </summary>
    public sealed class PlanStateMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.PlanState;

        public int TurnIndex { get; }

        public WeaponId Weapon { get; }

        /// <summary>The body move the player swiped (a signature move with its own body move sets that one instead, §8).</summary>
        public BodyMove BodyMove { get; }

        /// <summary>The raw stroke the server built the path from; empty when none is drawn or a signature move is set.</summary>
        public IReadOnlyList<Vec2> Stroke { get; }

        /// <summary>The signature move set; empty when none is.</summary>
        public SignatureMoveId Signature { get; }

        public bool IsReady { get; }

        public PlanStateMessage(int turnIndex, WeaponId weapon, BodyMove bodyMove, IReadOnlyList<Vec2> stroke, SignatureMoveId signature,
            bool isReady)
        {
            TurnIndex = turnIndex;
            Weapon = weapon;
            BodyMove = bodyMove;
            Stroke = Guard.NotNull(stroke, nameof(stroke));
            Signature = signature;
            IsReady = isReady;
        }
    }
}
