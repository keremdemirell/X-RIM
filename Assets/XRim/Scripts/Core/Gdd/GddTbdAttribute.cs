using System;

namespace XRim.Core.Gdd
{
    /// <summary>
    /// Marks a seam (interface, strategy, flag or field) that exists because the GDD marks the
    /// decision TBD. Never treat the current implementation or default as the final answer.
    /// Tools list every usage (Editor menu: XRim/Reports/TBD Seams).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct |
                    AttributeTargets.Enum | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = true, Inherited = false)]
    public sealed class GddTbdAttribute : Attribute
    {
        /// <summary>GDD section, e.g. "§9".</summary>
        public string Section { get; }

        /// <summary>The open question, as worded in the GDD's open questions register (§19).</summary>
        public string Question { get; }

        /// <summary>The GDD's current proposal, if any. Empty when the GDD has none.</summary>
        public string Proposal { get; set; } = string.Empty;

        public GddTbdAttribute(string section, string question)
        {
            Section = section;
            Question = question;
        }
    }
}
