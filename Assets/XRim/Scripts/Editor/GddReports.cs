using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XRim.Core.Gdd;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Reports: lists every GDD TBD wired into code as a seam, and every placeholder value, straight from the
    /// [GddTbd] and [Placeholder] attributes, so the lists can never drift from the code.
    /// </summary>
    public static class GddReports
    {
        private const string AssemblyPrefix = "XRim.";
        private const string TestAssemblyPrefix = "XRim.Tests";
        private const BindingFlags DeclaredMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [MenuItem(EditorPaths.MenuReports + "TBD Seams", priority = 20)]
        public static void ReportTbdSeams()
        {
            var rows = new List<(int Order, string Line)>();
            foreach (Type type in XRimTypes())
            {
                AddTbdRows(rows, type, type.FullName);
                foreach (MemberInfo member in type.GetMembers(DeclaredMembers))
                {
                    AddTbdRows(rows, member, $"{type.FullName}.{member.Name}");
                }
            }

            rows.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Line, b.Line));
            Debug.Log($"[XRim] {rows.Count} TBD seams in code (GDD open questions):\n" + string.Join("\n", rows.Select(r => r.Line)));
        }

        [MenuItem(EditorPaths.MenuReports + "Placeholder Values", priority = 21)]
        public static void ReportPlaceholders()
        {
            var lines = new List<string>();
            foreach (Type type in XRimTypes())
            {
                foreach (FieldInfo field in type.GetFields(DeclaredMembers))
                {
                    var placeholder = (PlaceholderAttribute)Attribute.GetCustomAttribute(field, typeof(PlaceholderAttribute));
                    if (placeholder != null) lines.Add($"{type.Name}.{field.Name.TrimStart('_')}: {placeholder.Reason}");
                }
            }

            lines.Sort(StringComparer.Ordinal);
            Debug.Log($"[XRim] {lines.Count} placeholder values (numbers the GDD has not set; " +
                      "per-weapon placeholders are flagged by WeaponStats.DesignIsTbd):\n" + string.Join("\n", lines));
        }

        private static void AddTbdRows(List<(int Order, string Line)> rows, MemberInfo member, string location)
        {
            foreach (GddTbdAttribute tbd in Attribute.GetCustomAttributes(member, typeof(GddTbdAttribute), false))
            {
                string proposal = string.IsNullOrEmpty(tbd.Proposal) ? string.Empty : $"  (proposal: {tbd.Proposal})";
                rows.Add((SectionOrder(tbd.Section), $"{tbd.Section}  {tbd.Question}{proposal}  →  {location}"));
            }
        }

        private static IEnumerable<Type> XRimTypes()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (!name.StartsWith(AssemblyPrefix, StringComparison.Ordinal) ||
                    name.StartsWith(TestAssemblyPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(t => t != null).ToArray();
                }

                foreach (Type type in types)
                {
                    yield return type;
                }
            }
        }

        /// <summary>Numeric order of a section like "§9" or "§4–5"; unknown sections sort last.</summary>
        private static int SectionOrder(string section)
        {
            int value = 0;
            bool hasDigit = false;
            foreach (char c in section)
            {
                if (char.IsDigit(c))
                {
                    value = value * 10 + (c - '0');
                    hasDigit = true;
                }
                else if (hasDigit)
                {
                    break;
                }
            }

            return hasDigit ? value : int.MaxValue;
        }
    }
}
