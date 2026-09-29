using System.Collections.Generic;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;

namespace XRim.Bots
{
    /// <summary>
    /// Debug bot: keeps its current weapon, picks a random allowed body move, draws no path and presses Ready.
    /// Enough to exercise the turn loop offline; not a design for a real AI opponent.
    /// </summary>
    public sealed class RandomBotBrain : IBotBrain
    {
        private static readonly BodyMove[] Candidates =
        {
            BodyMove.None, BodyMove.Crouch, BodyMove.Lunge, BodyMove.StepBack, BodyMove.Jump,
        };

        public TurnPlan PlanTurn(BotContext context)
        {
            PlanningConstraints constraints = context.Window.Constraints[context.Side];
            var allowed = new List<BodyMove>();
            foreach (BodyMove move in Candidates)
            {
                if (constraints.IsBodyMoveAllowed(move)) allowed.Add(move);
            }

            BodyMove chosen = allowed[context.Random.NextInt(0, allowed.Count)];
            WeaponId weapon = context.Window.Board.State.Fighters[context.Side].CurrentWeapon;
            return new TurnPlan(weapon, chosen, WeaponPath.Empty, default, true);
        }
    }
}
