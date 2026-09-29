using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XRim.Config;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Config
{
    public sealed class ConfigSanityTests
    {
        [Test]
        public void SettingsConfig_SnapshotIsAnIndependentCopy()
        {
            var clash = ScriptableObject.CreateInstance<ClashConfig>();
            try
            {
                ClashSettings snapshot = clash.CreateSnapshot();
                snapshot.CrushRatio = 99f;
                Assert.That(clash.Settings.CrushRatio, Is.EqualTo(new ClashSettings().CrushRatio));
            }
            finally
            {
                Object.DestroyImmediate(clash);
            }
        }

        [Test]
        public void TuningProfile_WithEmptySlots_UsesCodeDefaultsAndReportsThem()
        {
            var profile = ScriptableObject.CreateInstance<TuningProfile>();
            try
            {
                var issues = new List<string>();
                RulesSettings rules = profile.BuildRulesSettings(issues);
                Assert.That(rules.Match.TurnCap, Is.EqualTo(new MatchSettings().TurnCap));
                Assert.That(issues, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SettingsFieldName_MatchesTheSerializedField()
        {
            var damage = ScriptableObject.CreateInstance<DamageConfig>();
            try
            {
                using (var serialized = new SerializedObject(damage))
                {
                    Assert.That(serialized.FindProperty(SettingsConfigBase.SettingsFieldName), Is.Not.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(damage);
            }
        }

        [Test]
        public void TunableFields_SeparatePlaceholdersFromGddValues()
        {
            IReadOnlyList<TunableField> fields = TunableFields.Describe(typeof(DamageSettings));
            Assert.That(fields.Single(f => f.Field.Name == nameof(DamageSettings.MaxHp)).IsPlaceholder, Is.True);
            Assert.That(fields.Single(f => f.Field.Name == nameof(DamageSettings.PerHitLimbCapFraction)).IsPlaceholder, Is.False);
        }
    }
}
