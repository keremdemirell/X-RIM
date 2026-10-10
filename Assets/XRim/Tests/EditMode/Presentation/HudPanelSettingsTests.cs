using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using XRim.Presentation.UI;
using Object = UnityEngine.Object;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The HUDs copy their panel settings from an Editor-made PanelSettings asset: only an asset carries the ICU data UI Toolkit's text
    /// needs in a player build (the device test of Session 10 drew no HUD text without it).
    /// </summary>
    public sealed class HudPanelSettingsTests
    {
        private static readonly Vector2Int Reference = new Vector2Int(1920, 1080);
        private const int SortingOrder = 7;
        private const int TemplateTargetDisplay = 3; // a value the HUD never sets: only a copy of the template carries it

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void DestroyCreated()
        {
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private PanelSettings NewTemplate()
        {
            var template = ScriptableObject.CreateInstance<PanelSettings>();
            template.targetDisplay = TemplateTargetDisplay;
            _created.Add(template);
            return template;
        }

        [Test]
        public void WithATemplate_TheHudGetsItsOwnCopyOfIt()
        {
            PanelSettings template = NewTemplate();

            PanelSettings settings = HudPanelSettings.Create(Reference, SortingOrder, template);
            _created.Add(settings);

            Assert.That(settings, Is.Not.SameAs(template), "a copy, so destroying it never destroys the asset");
            Assert.That(settings.targetDisplay, Is.EqualTo(TemplateTargetDisplay), "what the template carries (ICU data in a build) is kept");
        }

        [Test]
        public void WithoutATemplate_TheSettingsAreCreatedInCode()
        {
            PanelSettings settings = HudPanelSettings.Create(Reference, SortingOrder, null);
            _created.Add(settings);

            Assert.That(settings, Is.Not.Null);
        }

        [Test]
        public void EitherWay_TheyScaleWithTheScreenHeightFromTheReferenceResolution()
        {
            foreach (PanelSettings template in new[] { NewTemplate(), null })
            {
                PanelSettings settings = HudPanelSettings.Create(Reference, SortingOrder, template);
                _created.Add(settings);

                Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
                Assert.That(settings.referenceResolution, Is.EqualTo(Reference));
                Assert.That(settings.screenMatchMode, Is.EqualTo(PanelScreenMatchMode.MatchWidthOrHeight));
                Assert.That(settings.match, Is.EqualTo(1f), "matched by height: a wider screen shows more, nothing grows");
                Assert.That(settings.sortingOrder, Is.EqualTo(SortingOrder));
            }
        }
    }
}
