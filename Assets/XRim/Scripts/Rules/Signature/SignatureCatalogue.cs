using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// Which signature moves a dummy brings into a match (GDD §8). A dummy's setup lists the ids it brought; with no list it brings the
    /// whole catalogue (dev: every move is available until the shop of Session 18 decides what a player owns, §16).
    /// </summary>
    public static class SignatureCatalogue
    {
        /// <summary>The catalogue's moves for the ids a dummy brought, in its order; unknown and repeated ids are left out. Null ids: every move.</summary>
        public static List<SignatureMoveStats> Brought(RulesSettings settings, IReadOnlyList<SignatureMoveId> ids)
        {
            Guard.NotNull(settings, nameof(settings));
            var moves = new List<SignatureMoveStats>();
            if (ids == null)
            {
                foreach (SignatureMoveStats move in settings.SignatureMoves)
                {
                    if (!moves.Contains(move)) moves.Add(move);
                }

                return moves;
            }

            foreach (SignatureMoveId id in ids)
            {
                SignatureMoveStats move = settings.FindSignatureMove(id);
                if (move != null && !moves.Contains(move)) moves.Add(move);
            }

            return moves;
        }

        /// <summary>The move of this id among <paramref name="moves"/>; null when none has it.</summary>
        public static SignatureMoveStats Find(IReadOnlyList<SignatureMoveStats> moves, SignatureMoveId id)
        {
            if (moves == null || id.IsEmpty) return null;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i].MoveId == id) return moves[i];
            }

            return null;
        }
    }
}
