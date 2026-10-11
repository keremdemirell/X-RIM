using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using XRim.Bots;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation.Execution;

namespace XRim.Tests.EditMode.Rules.Limbs
{
    /// <summary>
    /// Throwing anything, limbs included, is Rejected (GDD §12, §20): the severed limb is a melee club only. No engine-free assembly
    /// names a throw, and no planning command can let go of what a dummy holds; the only way a held item leaves the hand is the arm
    /// that holds it being severed.
    /// </summary>
    public sealed class NoThrowingTests
    {
        private const BindingFlags Everything =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static readonly Assembly[] EngineFree =
        {
            typeof(Vec2).Assembly, typeof(RulePolicies).Assembly, typeof(TurnSimulator).Assembly, typeof(LocalTurnAuthority).Assembly,
            typeof(RandomBotBrain).Assembly,
        };

        [Test]
        public void NoTypeOrMember_IsAboutThrowing()
        {
            var found = new List<string>();
            foreach (Type type in EngineFree.SelectMany(assembly => assembly.GetTypes()))
            {
                if (IsCompilerGenerated(type.Name)) continue;
                if (NamesAThrow(type.Name)) found.Add(type.FullName);
                foreach (MemberInfo member in type.GetMembers(Everything))
                {
                    if (!IsCompilerGenerated(member.Name) && NamesAThrow(member.Name)) found.Add($"{type.FullName}.{member.Name}");
                }
            }

            Assert.That(found, Is.Empty, "§20: throwing is Rejected");
        }

        [Test]
        public void NoWeaponKind_IsThrown_AndTheClubIsAMeleeWeapon()
        {
            Assert.That(Enum.GetNames(typeof(WeaponKind)), Is.EquivalentTo(new[] { "Weapon", "Shield", "SeveredLimb", "BodyBlow" }));
            Assert.That(GddStartingValues.SeveredLimb().Kind, Is.EqualTo(WeaponKind.SeveredLimb));
        }

        [Test]
        public void ThePlanningCommands_CannotLetGoOfAHeldItem()
        {
            string[] commands = typeof(PlanningCommand).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(PlanningCommand).IsAssignableFrom(type))
                .Select(type => type.Name).ToArray();

            Assert.That(commands, Is.EquivalentTo(new[]
            {
                nameof(SelectWeaponCommand), nameof(SetBodyMoveCommand), nameof(SetPathCommand), nameof(ClearPathCommand),
                nameof(SetReadyCommand), nameof(UseSignatureCommand),
            }));
        }

        private static bool NamesAThrow(string name) => name.IndexOf("throw", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsCompilerGenerated(string name) => name.IndexOf('<') >= 0;
    }
}
