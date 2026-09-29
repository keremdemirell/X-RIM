using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Hit Zones", fileName = "HitZones")]
    public sealed class HitZoneConfig : SettingsConfig<HitZoneSettings>
    {
    }
}
