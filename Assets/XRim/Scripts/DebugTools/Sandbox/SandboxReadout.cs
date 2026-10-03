using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using XRim.App;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation.Recording;

namespace XRim.DebugTools.Sandbox
{
    /// <summary>
    /// The sandbox's placeholder fight readout (Session 06), two boxes at the bottom of the screen: per dummy an HP bar, each
    /// limb's damage against its durability (* marks the weapon arm, which D15 lets interrupt), and what shapes its turn (a
    /// stun) or a KO. During playback the numbers follow the turn: each hit counts once playback has passed it.
    /// </summary>
    internal sealed class SandboxReadout
    {
        private const float BoxWidth = 330f;
        private const float BoxHeight = 112f;
        private const float Margin = 8f;
        private const float BarHeight = 16f;

        private static readonly BodyPart[] Limbs = { BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };
        private static readonly Color FullHpColor = new Color(0.25f, 0.8f, 0.3f);
        private static readonly Color NoHpColor = new Color(0.85f, 0.2f, 0.15f);
        private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.5f);

        /// <summary>The screen area both boxes cover (GUI coordinates, y down), so mouse drawing can avoid it.</summary>
        public Rect Area => new Rect((Screen.width - BoxWidth * 2f - Margin) * 0.5f, Screen.height - BoxHeight - Margin,
            BoxWidth * 2f + Margin, BoxHeight);

        public void Draw(MatchBootstrap bootstrap, PerSide<SandboxPlanSource> sources)
        {
            Rect area = Area;
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float x = side == Side.Left ? area.x : area.x + BoxWidth + Margin;
                GUILayout.BeginArea(new Rect(x, area.y, BoxWidth, BoxHeight), GUI.skin.box);
                DrawFighter(bootstrap, sources[side], side);
                GUILayout.EndArea();
            }
        }

        private static void DrawFighter(MatchBootstrap bootstrap, SandboxPlanSource source, Side side)
        {
            DamageSettings damage = bootstrap.RulesSettings.Damage;
            if (!TryRead(bootstrap, source, side, out Reading reading))
            {
                GUILayout.Label(side + ": waiting for the first turn");
                return;
            }

            FighterState fighter = reading.State.Fighters[side];
            float hp = reading.HpAt(side);
            GUILayout.Label(string.Format(CultureInfo.InvariantCulture, "{0}   {1}   HP {2:0.#} / {3:0}", side.ToString().ToUpperInvariant(),
                fighter.CurrentWeapon.Value, Mathf.Max(hp, 0f), damage.MaxHp));
            DrawBar(Mathf.Clamp01(hp / damage.MaxHp));

            BodyPart weaponArm = fighter.HasDominantArm ? BodyParts.DominantArm(fighter.Handedness) : BodyParts.OffArm(fighter.Handedness);
            var limbs = new List<string>();
            foreach (BodyPart limb in Limbs)
            {
                float durability = limb.IsArm() ? damage.ArmDurability : damage.LegDurability;
                float taken = reading.LimbDamageAt(side, limb);
                string mark = limb == weaponArm ? "*" : string.Empty;
                string sever = taken >= durability ? " SEVER" : string.Empty;
                limbs.Add(string.Format(CultureInfo.InvariantCulture, "{0}{1} {2:0.#}/{3:0}{4}", Short(limb), mark, taken, durability, sever));
            }

            GUILayout.Label(string.Join("   ", limbs));
            GUILayout.Label(reading.Status(side));
        }

        private static void DrawBar(float fraction)
        {
            Rect bar = GUILayoutUtility.GetRect(BoxWidth - Margin * 2f, BarHeight);
            Color previous = GUI.color;
            GUI.color = BarBackColor;
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(NoHpColor, FullHpColor, fraction);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static string Short(BodyPart limb)
        {
            switch (limb)
            {
                case BodyPart.LeftArm: return "L arm";
                case BodyPart.RightArm: return "R arm";
                case BodyPart.LeftLeg: return "L leg";
                default: return "R leg";
            }
        }

        /// <summary>
        /// While planning: the frozen board and this turn's limits. Otherwise: the last turn as resolved, rewound to the playback
        /// time (hits after it are added back).
        /// </summary>
        private static bool TryRead(MatchBootstrap bootstrap, SandboxPlanSource source, Side side, out Reading reading)
        {
            if (source.IsPlanning)
            {
                reading = Reading.Planning(source.Window.Board.State, source.Window.Constraints[side], bootstrap.RulesSettings.Match);
                return true;
            }

            TurnPlayback playback = bootstrap.Playback;
            TurnResult turn = playback != null ? playback.Current : null;
            if (turn == null)
            {
                reading = null;
                return false;
            }

            reading = Reading.Playback(turn, playback.Player.CurrentTime, bootstrap.Flow != null ? bootstrap.Flow.Outcome : null);
            return true;
        }

        /// <summary>What the readout shows for both dummies at one moment.</summary>
        private sealed class Reading
        {
            private readonly IReadOnlyList<MatchEvent> _events;
            private readonly SimTime _now;
            private readonly PlanningConstraints _constraints;
            private readonly MatchSettings _match;
            private readonly MatchOutcome _outcome;

            private Reading(MatchState state, IReadOnlyList<MatchEvent> events, SimTime now, PlanningConstraints constraints,
                MatchSettings match, MatchOutcome outcome)
            {
                State = state;
                _events = events;
                _now = now;
                _constraints = constraints;
                _match = match;
                _outcome = outcome;
            }

            public MatchState State { get; }

            public static Reading Planning(MatchState board, PlanningConstraints constraints, MatchSettings match) =>
                new Reading(board, null, default, constraints, match, null);

            public static Reading Playback(TurnResult turn, SimTime now, MatchOutcome outcome) =>
                new Reading(turn.Report.ResolvedState, turn.Timeline.Events, now, null, null, outcome);

            public float HpAt(Side side)
            {
                float hp = State.Fighters[side].Hp;
                foreach (HitLandedEvent hit in HitsStillAhead(side))
                {
                    hp += hit.Damage.HpDamage;
                }

                return hp;
            }

            public float LimbDamageAt(Side side, BodyPart limb)
            {
                float taken = State.Fighters[side].GetLimbDamage(limb);
                foreach (HitLandedEvent hit in HitsStillAhead(side))
                {
                    if (hit.Hit.Part == limb) taken -= hit.Damage.LimbDamage;
                }

                return Mathf.Max(taken, 0f);
            }

            public string Status(Side side)
            {
                if (_constraints != null) return PlanningStatus(_constraints, _match);

                var notes = new List<string>();
                foreach (MatchEvent matchEvent in _events)
                {
                    if (matchEvent.Time > _now) continue;
                    if (matchEvent is StatusAppliedEvent status && status.Side == side)
                        notes.Add(status.Kind.ToString().ToUpperInvariant() + ": shapes the next turn");
                    if (matchEvent is WeaponClashEvent clash) AddClashNote(clash.Result, side, notes);
                    if (matchEvent is ShieldBlockEvent block) AddBlockNote(block, side, notes);
                    if (matchEvent is FighterDiedEvent died && died.Side == side) notes.Add("KO");
                }

                if (_outcome != null) notes.Add(_outcome.Winner == side ? $"WINS ({_outcome.Reason})" : "LOSES");
                return notes.Count > 0 ? string.Join("   ", notes) : " ";
            }

            private static void AddClashNote(ClashResult result, Side side, List<string> notes)
            {
                if (result.Rebounds) notes.Add("REBOUND");
                else if (result.IsKnockedOff(side)) notes.Add("KNOCKED OFF");
                else if (result.Kind == ClashKind.CrushThrough) notes.Add("CRUSHED THROUGH");
            }

            private static void AddBlockNote(ShieldBlockEvent block, Side side, List<string> notes)
            {
                bool full = block.Result.AttackStopped;
                if (side == block.Blocker) notes.Add(full ? "BLOCK" : "PARTIAL BLOCK");
                else notes.Add(full ? "BLOCKED" : "HALF-BLOCKED");
            }

            private IEnumerable<HitLandedEvent> HitsStillAhead(Side victim)
            {
                if (_events == null) yield break;
                foreach (MatchEvent matchEvent in _events)
                {
                    if (matchEvent.Time > _now && matchEvent is HitLandedEvent hit && hit.Hit.Victim == victim) yield return hit;
                }
            }

            /// <summary>What this turn's statuses do, read back from the limits they set (D17's option, whichever is tuned).</summary>
            private static string PlanningStatus(PlanningConstraints constraints, MatchSettings match)
            {
                if (constraints.Statuses.Count == 0) return " ";

                var effects = new List<string>();
                if (!constraints.IsBodyMoveAllowed(BodyMove.Crouch) && !constraints.IsBodyMoveAllowed(BodyMove.Jump)) effects.Add("no body move");
                if (constraints.PlanningDurationSeconds < match.PlanningDurationSeconds) effects.Add("shorter planning");
                if (constraints.InkLengthMultiplier < 1f) effects.Add(string.Format(CultureInfo.InvariantCulture, "ink ×{0:0.##}", constraints.InkLengthMultiplier));

                var kinds = new List<string>();
                foreach (StatusKind kind in constraints.Statuses)
                {
                    kinds.Add(kind.ToString().ToUpperInvariant());
                }

                return string.Join(" + ", kinds) + " this turn: " + (effects.Count > 0 ? string.Join(", ", effects) : "no effect");
            }
        }
    }
}
