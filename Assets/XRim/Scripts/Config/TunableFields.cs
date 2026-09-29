using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using XRim.Core.Gdd;

namespace XRim.Config
{
    /// <summary>Lists the serialized fields of a type with their [Placeholder] and [GddTbd] markers.</summary>
    public static class TunableFields
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static IReadOnlyList<TunableField> Describe(Type type)
        {
            var result = new List<TunableField>();
            foreach (FieldInfo field in type.GetFields(InstanceFields))
            {
                bool serialized = (field.IsPublic && !field.IsNotSerialized) || field.IsDefined(typeof(SerializeField), false);
                if (!serialized || field.IsInitOnly) continue;

                var placeholder = (PlaceholderAttribute)Attribute.GetCustomAttribute(field, typeof(PlaceholderAttribute));
                var tbd = (GddTbdAttribute[])Attribute.GetCustomAttributes(field, typeof(GddTbdAttribute));
                result.Add(new TunableField(field, placeholder, tbd));
            }

            return result;
        }
    }
}
