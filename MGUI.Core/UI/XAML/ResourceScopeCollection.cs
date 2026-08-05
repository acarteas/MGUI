using MGUI.Core.UI;
using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.XAML
{
    internal sealed class ResourceScopeCollection
    {
        private readonly MGResources _GlobalResources;
        private readonly Dictionary<string, List<XAMLColor>> _ColorsByKey = new(StringComparer.Ordinal);

        public ResourceScopeCollection(MGResources globalResources)
        {
            _GlobalResources = globalResources ?? throw new ArgumentNullException(nameof(globalResources));
        }

        public void PushScope(IEnumerable<ColorResource> colors, string scopeName)
        {
            if (colors == null)
            {
                return;
            }

            HashSet<string> keysInScope = new(StringComparer.Ordinal);
            foreach (ColorResource color in colors)
            {
                if (color == null || string.IsNullOrWhiteSpace(color.Key))
                {
                    throw new InvalidOperationException($"{scopeName} colors must define a non-empty {nameof(ColorResource.Key)}.");
                }

                if (!keysInScope.Add(color.Key))
                {
                    throw new InvalidOperationException($"The color key '{color.Key}' appears more than once in '{scopeName}'.");
                }

                if (!_ColorsByKey.TryGetValue(color.Key, out List<XAMLColor> values))
                {
                    values = new();
                    _ColorsByKey.Add(color.Key, values);
                }

                values.Add(color.Value);
            }
        }

        public void PopScope(IEnumerable<ColorResource> colors)
        {
            if (colors == null)
            {
                return;
            }

            foreach (ColorResource color in colors)
            {
                if (color == null || string.IsNullOrWhiteSpace(color.Key))
                {
                    continue;
                }

                if (_ColorsByKey.TryGetValue(color.Key, out List<XAMLColor> values))
                {
                    values.RemoveAt(values.Count - 1);
                    if (values.Count == 0)
                    {
                        _ColorsByKey.Remove(color.Key);
                    }
                }
            }
        }

        public bool TryGetColor(string key, out XAMLColor color)
        {
            if (_ColorsByKey.TryGetValue(key, out List<XAMLColor> values) && values.Count > 0)
            {
                color = values[^1];
                return true;
            }

            return _GlobalResources.TryGetColor(key, out color);
        }
    }
}
