using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MGUI.Core.UI
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed class MGNameScope
    {
        private Dictionary<string, MGElement> ElementsByName { get; } = new(StringComparer.Ordinal);

        public MGWindow Window { get; }
        public MGNameScope ParentScope { get; }
        public MGElement Owner { get; }
        public string Label { get; }

        public MGNameScope(MGWindow Window, MGNameScope ParentScope, MGElement Owner, string Label)
        {
            this.Window = Window ?? throw new ArgumentNullException(nameof(Window));
            this.ParentScope = ParentScope;
            this.Owner = Owner;
            this.Label = string.IsNullOrWhiteSpace(Label) ? GetDefaultLabel(Owner) : Label;
        }

        public bool TryGetElement(string Name, out MGElement Element) => ElementsByName.TryGetValue(Name, out Element);

        internal void Add(string Name, MGElement Element)
        {
            ElementsByName.Add(Name, Element);
        }

        internal bool Remove(string Name) => ElementsByName.Remove(Name);

        internal IEnumerable<KeyValuePair<string, MGElement>> GetEntries() => ElementsByName;

        private static string GetDefaultLabel(MGElement Owner)
        {
            if (Owner == null)
            {
                return "WindowRoot";
            }

            return $"{Owner.ElementType}({Owner.Name ?? Owner.UniqueId})";
        }

        private string DebuggerDisplay => $"{Label} [{ElementsByName.Count}]";
    }
}
