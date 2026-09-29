using UnityEngine;
using XRim.Rules.Settings;

namespace XRim.Config
{
    /// <summary>One signature (joker) move (GDD §8). The catalogue is TBD.</summary>
    [CreateAssetMenu(menuName = ConfigMenus.Rules + "Signature Move", fileName = "SignatureMove")]
    public sealed class SignatureMoveDefinition : SettingsConfig<SignatureMoveStats>
    {
    }
}
