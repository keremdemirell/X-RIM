using System;

namespace XRim.Core.Gdd
{
    /// <summary>
    /// Marks a value the GDD has not set yet. It holds a number only so the prototype runs.
    /// The tuning panel highlights these and the Editor report lists them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PlaceholderAttribute : Attribute
    {
        /// <summary>Why the value is open, e.g. "§11 HP scale is TBD".</summary>
        public string Reason { get; }

        public PlaceholderAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
