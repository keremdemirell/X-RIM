using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRim.Core.Gdd;

namespace XRim.Config
{
    /// <summary>One editable field of a settings class or config asset, with its GDD status markers.</summary>
    public sealed class TunableField
    {
        public FieldInfo Field { get; }
        public PlaceholderAttribute Placeholder { get; }
        public IReadOnlyList<GddTbdAttribute> Tbd { get; }
        public string Label { get; }

        public bool IsPlaceholder => Placeholder != null;
        public bool IsTbd => Tbd.Count > 0;

        public TunableField(FieldInfo field, PlaceholderAttribute placeholder, IReadOnlyList<GddTbdAttribute> tbd)
        {
            Field = field;
            Placeholder = placeholder;
            Tbd = tbd;
            Label = Nicify(field.Name);
        }

        /// <summary>Why the value is open or undecided, for tooltips.</summary>
        public string Describe()
        {
            var text = new StringBuilder();
            if (Placeholder != null) text.Append("Placeholder: ").Append(Placeholder.Reason);
            foreach (GddTbdAttribute tbd in Tbd)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append("TBD ").Append(tbd.Section).Append(": ").Append(tbd.Question);
                if (!string.IsNullOrEmpty(tbd.Proposal)) text.Append(" (proposal: ").Append(tbd.Proposal).Append(')');
            }

            return text.ToString();
        }

        private static string Nicify(string fieldName)
        {
            string name = fieldName.TrimStart('_');
            var text = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i == 0) text.Append(char.ToUpperInvariant(c));
                else
                {
                    if (char.IsUpper(c) && !char.IsUpper(name[i - 1])) text.Append(' ');
                    text.Append(c);
                }
            }

            return text.ToString();
        }
    }
}
