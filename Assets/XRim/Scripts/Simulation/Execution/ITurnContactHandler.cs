using System.Collections.Generic;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// What happens when two bodies touch during a turn: the seam between physics and the rules (ARCHITECTURE §2,
    /// "Simulation detects, Rules decide, Simulation applies"). Session 06 puts the hit, priority and interrupt rules here
    /// (<see cref="HitContactHandler"/>); Session 07 adds clashes and shield blocks.
    /// </summary>
    public interface ITurnContactHandler
    {
        /// <summary>Called once per turn before its first step, with that turn's context. Per-turn state starts here.</summary>
        void BeginTurn(TurnContactContext context);

        /// <summary>
        /// Contacts in true time order (<see cref="TurnContactOrder"/>), handed over once no earlier contact can still be
        /// reported. Contacts at exactly the same time always arrive together.
        /// </summary>
        void Handle(IReadOnlyList<TurnContact> contacts, TurnContactContext context);
    }
}
