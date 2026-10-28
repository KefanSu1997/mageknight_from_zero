using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MK.Logic.Runtime.Battles
{
    /// <summary>Local demo undo. Restores the existing runtime graph, including physical card/token
    /// identities, rather than creating another player or rerunning the beginning of a game.</summary>
    public sealed class DemoStateCheckpoint
    {
        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
        private sealed class SavedObject
        {
            internal object Target;
            internal FieldInfo[] Fields;
            internal object[] Values;
            internal Array ArrayCopy;
        }
        private readonly List<SavedObject> _objects = new();
        private readonly HashSet<object> _visited = new(new IdentityComparer());

        public static DemoStateCheckpoint Capture(params object[] roots)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            var snapshot = new DemoStateCheckpoint();
            foreach (var root in roots) snapshot.Visit(root);
            return snapshot;
        }

        public void Restore()
        {
            // Field values point to the original objects. Resized collection buffers are reattached,
            // then their original contents are restored; repeated undo remains possible.
            foreach (var saved in _objects)
            {
                if (saved.ArrayCopy != null)
                {
                    Array.Copy(saved.ArrayCopy, (Array)saved.Target, saved.ArrayCopy.Length);
                    continue;
                }
                for (int i = 0; i < saved.Fields.Length; i++)
                    if (!saved.Fields[i].IsInitOnly)
                        saved.Fields[i].SetValue(saved.Target, saved.Values[i]);
            }
        }

        private static bool Domain(Type type) => type.Namespace?.StartsWith("MK.Logic", StringComparison.Ordinal) == true;
        private static bool Leaf(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string)
            || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(TimeSpan)
            || type == typeof(Guid) || typeof(Type).IsAssignableFrom(type);

        private void Visit(object value)
        {
            if (value == null) return;
            var type = value.GetType();
            if (Leaf(type)) return;
            if (!type.IsValueType && !_visited.Add(value)) return;
            if (value is Delegate callback)
            {
                // Logic closures are part of the runtime state; controller callbacks stay outside it.
                foreach (var invocation in callback.GetInvocationList())
                    if (invocation.Target != null && Domain(invocation.Target.GetType())) Visit(invocation.Target);
                return;
            }
            if (value is Array array)
            {
                if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                    throw new NotSupportedException("Demo undo cannot capture this array: " + type.FullName);
                _objects.Add(new SavedObject { Target = array, ArrayCopy = (Array)array.Clone() });
                foreach (var item in array) Visit(item);
                return;
            }
            bool collection = type.Namespace == "System.Collections.Generic"
                || type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(ReadOnlyCollection<>)
                    || type.GetGenericTypeDefinition() == typeof(ReadOnlyDictionary<,>))
                || type.DeclaringType?.IsGenericType == true
                    && type.DeclaringType.GetGenericTypeDefinition() == typeof(ReadOnlyDictionary<,>);
            bool randomState = type == typeof(Random) || type.DeclaringType == typeof(Random)
                || type.FullName?.StartsWith("System.Random+", StringComparison.Ordinal) == true;
            // Framework comparers have no mutable game state. Never recurse into runtime metadata.
            if (collection && (type.Name.Contains("Comparer") || type.Name.Contains("EqualityComparer"))) return;
            if (!Domain(type) && !collection && !randomState && !type.IsValueType)
                throw new NotSupportedException("Demo undo cannot capture state type: " + type.FullName);
            var fields = new List<FieldInfo>();
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                fields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            var values = new object[fields.Count];
            for (int i = 0; i < fields.Count; i++) values[i] = fields[i].GetValue(value);
            if (!type.IsValueType) _objects.Add(new SavedObject { Target = value, Fields = fields.ToArray(), Values = values });
            // Boxed structs are copied in their owning field/array; recurse into their embedded refs.
            foreach (var child in values) Visit(child);
        }
    }
}
