using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    /// <summary>One stance swipe (GDD §5).</summary>
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Body Move", fileName = "BodyMove")]
    public sealed class BodyMoveDefinition : SettingsConfig<BodyMoveStats>
    {
    }
}
