using System;
using System.Collections.Generic;
using System.Linq;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Status;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Simulation.Scenarios
{
    /// <summary>
    /// Turns a <see cref="Scenario"/> into its save-file form (<see cref="ScenarioData"/>) and back, so a JSON serializer only ever sees
    /// plain fields. Engine-free: the same code can load scenarios on a server or in a test without Unity. The data shares the settings
    /// objects with the scenario (they are already plain data), so serialize it before changing them.
    /// <para>
    /// A corrupt or foreign file is refused with a <see cref="FormatException"/>, a file from newer code and a status effect without a saved
    /// form with a <see cref="NotSupportedException"/>, each saying what is wrong.
    /// </para>
    /// </summary>
    public static class ScenarioCodec
    {
        /// <param name="savedAt">When it is saved, written for people reading the file (never read back).</param>
        public static ScenarioData ToData(Scenario scenario, string savedAt = null)
        {
            Guard.NotNull(scenario, nameof(scenario));
            TurnInput input = scenario.Input;
            return new ScenarioData
            {
                FormatVersion = ScenarioData.CurrentFormatVersion,
                Name = scenario.Name,
                Note = scenario.Note,
                SavedAt = savedAt ?? string.Empty,
                WorldUnitsPerArenaUnit = scenario.WorldUnitsPerArenaUnit,
                State = StateToData(input.Board.State),
                Pose = input.Board.Pose.Clone(),
                LeftPlan = ToData(input.Plans.Left),
                RightPlan = ToData(input.Plans.Right),
                Rules = input.Rules,
                Simulation = input.Simulation,
                HasExpected = scenario.Expected != null,
                Expected = scenario.Expected ?? new ScenarioOutcome(),
            };
        }

        public static Scenario FromData(ScenarioData data)
        {
            if (data == null) throw new FormatException("The file holds no scenario.");
            if (data.FormatVersion > ScenarioData.CurrentFormatVersion)
                throw new NotSupportedException($"This scenario was saved in format {data.FormatVersion}, by newer code; this build reads up to " +
                                                $"format {ScenarioData.CurrentFormatVersion}.");
            if (data.FormatVersion < 1) throw new FormatException("The file is not a scenario (it has no format version).");
            if (data.State == null || data.Pose == null || data.LeftPlan == null || data.RightPlan == null)
                throw new FormatException("The scenario is missing its board or a plan.");
            if (data.Rules == null || data.Simulation == null) throw new FormatException("The scenario is missing its settings snapshot.");
            if (!(data.WorldUnitsPerArenaUnit > 0f)) throw new FormatException("The scenario is missing its physics scale (world units per arena unit).");

            CheckPose(data.Pose.Left, "left");
            CheckPose(data.Pose.Right, "right");
            var board = new BoardSnapshot(StateFromData(data.State), data.Pose.Clone());
            var plans = new PerSide<TurnPlan>(FromData(data.LeftPlan), FromData(data.RightPlan));
            var input = new TurnInput(board, plans, data.Rules, data.Simulation);
            return new Scenario(data.Name, data.Note, input, data.WorldUnitsPerArenaUnit, data.HasExpected ? data.Expected : null);
        }

        // --- Rules state ---------------------------------------------------------------------

        /// <summary>
        /// The rules state in plain fields. Public so the online protocol (Session 19) carries a board in the same shape a scenario saves,
        /// and a new field is added in one place.
        /// </summary>
        public static MatchStateData StateToData(MatchState state) => new MatchStateData
        {
            TurnIndex = state.TurnIndex,
            IsSuddenDeath = state.IsSuddenDeath,
            SuddenDeathRepeats = state.SuddenDeathRepeats,
            Left = ToData(state.Fighters.Left),
            Right = ToData(state.Fighters.Right),
            LeftWall = ToData(state.Walls.Left),
            RightWall = ToData(state.Walls.Right),
        };

        /// <summary>The rules state back from its plain fields; a missing fighter or a wrong limb count is a <see cref="FormatException"/>.</summary>
        public static MatchState StateFromData(MatchStateData data)
        {
            if (data == null) throw new FormatException("The rules state is missing.");
            var fighters = new PerSide<FighterState>(FromData(data.Left, "left"), FromData(data.Right, "right"));
            var walls = new PerSide<ElectricWallState>(FromData(data.LeftWall), FromData(data.RightWall));
            return new MatchState(fighters, walls)
            {
                TurnIndex = data.TurnIndex,
                IsSuddenDeath = data.IsSuddenDeath,
                SuddenDeathRepeats = data.SuddenDeathRepeats,
            };
        }

        private static FighterStateData ToData(FighterState fighter)
        {
            var data = new FighterStateData
            {
                Handedness = fighter.Handedness,
                Hp = fighter.Hp,
                HpBeforeSuddenDeath = fighter.HpBeforeSuddenDeath,
                CurrentWeapon = fighter.CurrentWeapon.Value ?? string.Empty,
                HpDamageTaken = fighter.HpDamageTaken,
                LifestealHitCount = fighter.LifestealHitCount,
                LifestealDamageDealt = fighter.LifestealDamageDealt,
                HitLandedThisTurn = fighter.HitLandedThisTurn,
                SignatureCharge = fighter.SignatureCharge,
                SignatureUses = fighter.SignatureUses,
                ConsecutiveIdleTurns = fighter.ConsecutiveIdleTurns,
                HasHeldLimb = fighter.HeldLimb.HasValue,
                HeldLimbOwner = fighter.HeldLimb?.Owner ?? Side.Left,
                HeldLimbPart = fighter.HeldLimb?.Part ?? BodyPart.Head,
                LostWeapons = fighter.LostWeapons.Select(id => id.Value ?? string.Empty).OrderBy(id => id, StringComparer.Ordinal).ToList(),
            };
            foreach (BodyPart part in BodyParts.All)
            {
                data.LimbDamage[(int)part] = fighter.GetLimbDamage(part);
                data.Severed[(int)part] = fighter.IsSevered(part);
            }

            foreach (IStatusEffect status in fighter.Statuses)
            {
                data.Statuses.Add(ToData(status));
            }

            return data;
        }

        private static FighterState FromData(FighterStateData data, string which)
        {
            if (data == null) throw new FormatException($"The scenario is missing the {which} fighter.");
            if (data.LimbDamage == null || data.LimbDamage.Length != BodyParts.Count || data.Severed == null || data.Severed.Length != BodyParts.Count)
                throw new FormatException($"The {which} fighter's limbs are not {BodyParts.Count} entries long.");

            var fighter = new FighterState(data.Handedness, data.Hp, new WeaponId(data.CurrentWeapon))
            {
                HpBeforeSuddenDeath = data.HpBeforeSuddenDeath,
                HpDamageTaken = data.HpDamageTaken,
                LifestealHitCount = data.LifestealHitCount,
                LifestealDamageDealt = data.LifestealDamageDealt,
                HitLandedThisTurn = data.HitLandedThisTurn,
                SignatureCharge = data.SignatureCharge,
                SignatureUses = data.SignatureUses,
                ConsecutiveIdleTurns = data.ConsecutiveIdleTurns,
                HeldLimb = data.HasHeldLimb ? new SeveredLimbRef(data.HeldLimbOwner, data.HeldLimbPart) : (SeveredLimbRef?)null,
            };
            foreach (BodyPart part in BodyParts.All)
            {
                fighter.SetLimbDamage(part, data.LimbDamage[(int)part]);
                if (data.Severed[(int)part]) fighter.MarkSevered(part);
            }

            if (data.Statuses != null)
            {
                foreach (StatusEffectData status in data.Statuses)
                {
                    fighter.Statuses.Add(FromData(status));
                }
            }

            if (data.LostWeapons != null)
            {
                foreach (string id in data.LostWeapons)
                {
                    fighter.LostWeapons.Add(new WeaponId(id));
                }
            }

            return fighter;
        }

        private static StatusEffectData ToData(IStatusEffect status)
        {
            var data = new StatusEffectData { Kind = status.Kind, RemainingTurns = status.RemainingTurns };
            switch (status)
            {
                case NoBodyMoveStatus _:
                    data.Effect = StunEffect.NoBodyMove;
                    break;
                case ShorterPlanningStatus shorter:
                    data.Effect = StunEffect.ShorterPlanning;
                    data.Fraction = shorter.DurationFraction;
                    break;
                case LessInkStatus lessInk:
                    data.Effect = StunEffect.LessInk;
                    data.Fraction = lessInk.InkLengthFraction;
                    break;
                default:
                    throw new NotSupportedException($"A scenario cannot save the status effect '{status.GetType().Name}': only the built-in " +
                                                    "ones (no body move, shorter planning, less ink) have a saved form.");
            }

            return data;
        }

        private static IStatusEffect FromData(StatusEffectData data)
        {
            if (data == null || data.RemainingTurns < 1) throw new FormatException("A saved status effect has no turns left.");
            switch (data.Effect)
            {
                case StunEffect.NoBodyMove:
                    return new NoBodyMoveStatus(data.Kind, data.RemainingTurns);
                case StunEffect.ShorterPlanning:
                    return new ShorterPlanningStatus(data.Kind, data.RemainingTurns, Fraction(data));
                case StunEffect.LessInk:
                    return new LessInkStatus(data.Kind, data.RemainingTurns, Fraction(data));
                default:
                    throw new FormatException($"A saved status effect is of an unknown type ({(int)data.Effect}).");
            }
        }

        private static float Fraction(StatusEffectData data) =>
            data.Fraction > 0f ? data.Fraction : throw new FormatException("A saved status effect has no fraction.");

        private static ElectricWallStateData ToData(ElectricWallState wall) => new ElectricWallStateData
        {
            Phase = wall.Phase,
            IsUp = wall.IsUp,
            PositionUnits = wall.PositionUnits,
            ConsecutiveRetreats = wall.ConsecutiveRetreats,
        };

        private static ElectricWallState FromData(ElectricWallStateData data) => data == null
            ? new ElectricWallState()
            : new ElectricWallState
            {
                Phase = data.Phase,
                IsUp = data.IsUp,
                PositionUnits = data.PositionUnits,
                ConsecutiveRetreats = data.ConsecutiveRetreats,
            };

        // --- Plans and pose --------------------------------------------------------------------

        private static PlanData ToData(TurnPlan plan) => new PlanData
        {
            Weapon = plan.Weapon.Value ?? string.Empty,
            BodyMove = plan.BodyMove,
            Path = new List<Vec2>(plan.Path.Points),
            Signature = plan.Signature.Value ?? string.Empty,
            PressedReady = plan.PressedReady,
        };

        private static TurnPlan FromData(PlanData data)
        {
            WeaponPath path = data.Path != null && data.Path.Count > 0 ? new WeaponPath(data.Path) : WeaponPath.Empty;
            return new TurnPlan(new WeaponId(data.Weapon), data.BodyMove, path, new SignatureMoveId(data.Signature), data.PressedReady);
        }

        private static void CheckPose(FighterPose pose, string which)
        {
            if (pose == null || pose.Parts == null || pose.Parts.Length != BodyParts.Count || pose.LowerSegments == null ||
                pose.LowerSegments.Length != BodyParts.Count)
                throw new FormatException($"The {which} dummy's pose is not {BodyParts.Count} body parts long.");
        }
    }
}
