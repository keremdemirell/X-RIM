using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Damage", fileName = "Damage")]
    public sealed class DamageConfig : SettingsConfig<DamageSettings>
    {
    }
}
