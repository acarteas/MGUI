using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI.Data_Binding
{
    /// <summary>Static class that keeps track of all <see cref="DataBinding"/>s for all objects.</summary>
    public static class DataBindingManager
    {
        private static readonly List<DataBinding> _Bindings = new();
        public static IReadOnlyList<DataBinding> Bindings => _Bindings;

        private static readonly Dictionary<object, List<DataBinding>> _BindingsByTargetObject = new(ReferenceEqualityComparer.Instance);

        public static DataBinding AddBinding(BindingConfig Config, object TargetObject)
        {
            if (TargetObject == null)
            {
                throw new ArgumentNullException(nameof(TargetObject));
            }

            _BindingsByTargetObject.TryGetValue(TargetObject, out List<DataBinding> ObjectBindings);
            if (ObjectBindings != null && ObjectBindings.Any(x => x.Config.TargetPath == Config.TargetPath))
            {
                throw new InvalidOperationException(
                    $"Unable to bind to target property '{Config.TargetPath}' of target object '{TargetObject}' ({TargetObject.GetType().FullName}) " +
                    $"because a binding was already created for this target.");
            }

            DataBinding Binding = new(Config, TargetObject);
            if (ObjectBindings == null)
            {
                ObjectBindings = new();
                _BindingsByTargetObject.Add(TargetObject, ObjectBindings);
            }

            _Bindings.Add(Binding);
            ObjectBindings.Add(Binding);
            return Binding;
        }

        internal static bool UnregisterBinding(DataBinding Binding)
        {
            bool Result = _Bindings.Remove(Binding);
            if (_BindingsByTargetObject.TryGetValue(Binding.RegistrationTarget, out List<DataBinding> ObjectBindings))
            {
                ObjectBindings.Remove(Binding);
                if (ObjectBindings.Count == 0)
                {
                    _BindingsByTargetObject.Remove(Binding.RegistrationTarget);
                }
            }

            return Result;
        }

        public static bool RemoveBinding(DataBinding Binding)
        {
            if (Binding == null)
            {
                throw new ArgumentNullException(nameof(Binding));
            }

            bool Result = UnregisterBinding(Binding);
            if (Result)
            {
                Binding.Dispose();
            }

            return Result;
        }

        /// <summary>Removes all <see cref="DataBinding"/>s registered for <paramref name="TargetObject"/>.</summary>
        /// <returns>The count of the removed bindings</returns>
        public static int RemoveBindings(object TargetObject)
        {
            if (TargetObject == null)
            {
                throw new ArgumentNullException(nameof(TargetObject));
            }

            if (!_BindingsByTargetObject.TryGetValue(TargetObject, out List<DataBinding> ObjectBindings))
            {
                return 0;
            }

            DataBinding[] Removed = ObjectBindings.ToArray();
            foreach (DataBinding Binding in Removed)
            {
                Binding.Dispose();
            }

            return Removed.Length;
        }
    }
}
