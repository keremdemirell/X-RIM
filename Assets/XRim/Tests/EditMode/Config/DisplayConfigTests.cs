using NUnit.Framework;
using UnityEngine;
using XRim.Config;

namespace XRim.Tests.EditMode.Config
{
    /// <summary>The frame-rate target is a client setting: it lives in an asset, never in code, and never changes an outcome (GDD §18).</summary>
    public sealed class DisplayConfigTests
    {
        [Test]
        public void TheTargetFrameRate_StartsAt60()
        {
            var display = ScriptableObject.CreateInstance<DisplayConfig>();
            try
            {
                Assert.That(display.TargetFrameRate, Is.EqualTo(60));
            }
            finally
            {
                Object.DestroyImmediate(display);
            }
        }

        [Test]
        public void ADisplayConfigIsPartOfTheClientConfigs_SoTheTuningPanelListsIt()
        {
            var profile = ScriptableObject.CreateInstance<TuningProfile>();
            var display = ScriptableObject.CreateInstance<DisplayConfig>();
            try
            {
                var serialized = new UnityEditor.SerializedObject(profile);
                serialized.FindProperty(TuningProfile.FieldNames.Display).objectReferenceValue = display;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(profile.Display, Is.SameAs(display));
                Assert.That(profile.ClientConfigs(), Does.Contain(display));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(display);
            }
        }
    }
}
