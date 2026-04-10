using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI.XAML
{
    internal readonly record struct ResolvedStyle(string Name, Style Style, IReadOnlyList<Setter> Setters);
    internal interface INamedStyleResolver
    {
        public bool TryGetStyle(string name, out Style style);
    }

    internal sealed class NamedStyleScopeCollection : INamedStyleResolver
    {
        private readonly Dictionary<string, List<Style>> _StylesByName;

        public NamedStyleScopeCollection(IReadOnlyDictionary<string, Style> GlobalStyles)
        {
            _StylesByName = new(StringComparer.Ordinal);

            if (GlobalStyles != null)
            {
                foreach (KeyValuePair<string, Style> kvp in GlobalStyles)
                {
                    _StylesByName.Add(kvp.Key, new() { kvp.Value });
                }
            }
        }

        public bool TryGetStyle(string name, out Style style)
        {
            if (!string.IsNullOrWhiteSpace(name)
                && _StylesByName.TryGetValue(name, out List<Style> styles)
                && styles.Count > 0)
            {
                style = styles[^1];
                return true;
            }

            style = null;
            return false;
        }

        public void PushScope(IEnumerable<Style> Styles, string ScopeName)
        {
            if (Styles == null)
            {
                return;
            }

            HashSet<string> NamesInScope = new(StringComparer.Ordinal);
            foreach (Style Style in Styles)
            {
                if (Style == null || string.IsNullOrWhiteSpace(Style.Name))
                {
                    continue;
                }

                if (!NamesInScope.Add(Style.Name))
                {
                    throw new InvalidOperationException($"The style name '{Style.Name}' appears more than once in '{ScopeName}'.");
                }

                if (!_StylesByName.TryGetValue(Style.Name, out List<Style> matchingStyles))
                {
                    matchingStyles = new();
                    _StylesByName.Add(Style.Name, matchingStyles);
                }

                matchingStyles.Add(Style);
            }
        }

        public void PopScope(IEnumerable<Style> Styles)
        {
            if (Styles == null)
            {
                return;
            }

            foreach (Style Style in Styles.Reverse())
            {
                if (Style == null || string.IsNullOrWhiteSpace(Style.Name))
                {
                    continue;
                }

                if (_StylesByName.TryGetValue(Style.Name, out List<Style> matchingStyles) && matchingStyles.Count > 0)
                {
                    matchingStyles.RemoveAt(matchingStyles.Count - 1);
                    if (matchingStyles.Count == 0)
                    {
                        _StylesByName.Remove(Style.Name);
                    }
                }
            }
        }
    }

    internal static class StyleResolver
    {
        public static void ValidateNamedStyles(IReadOnlyDictionary<string, Style> existingStyles, IReadOnlyDictionary<string, Style> incomingStyles)
        {
            if (incomingStyles == null)
            {
                throw new ArgumentNullException(nameof(incomingStyles));
            }

            Dictionary<string, Style> allStyles = existingStyles?.ToDictionary(x => x.Key, x => x.Value) ?? new();
            foreach (KeyValuePair<string, Style> kvp in incomingStyles)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    throw new InvalidOperationException("Styles in a ResourceDictionary must define a non-empty Name.");
                }

                if (allStyles.ContainsKey(kvp.Key))
                {
                    throw new InvalidOperationException($"A style named '{kvp.Key}' is already registered.");
                }

                allStyles.Add(kvp.Key, kvp.Value ?? throw new InvalidOperationException($"Style '{kvp.Key}' cannot be null."));
            }

            foreach (KeyValuePair<string, Style> kvp in incomingStyles)
            {
                ResolveSetters(kvp.Key, kvp.Value, new NamedStyleScopeCollection(allStyles));
            }
        }

        public static IReadOnlyList<Setter> ResolveSetters(Style style, INamedStyleResolver stylesByName)
            => ResolveSetters(null, style, stylesByName);

        public static IReadOnlyList<Setter> ResolveSetters(string styleName, Style style, INamedStyleResolver stylesByName)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            List<Setter> resolvedSetters = new();
            List<string> activePath = new();
            ResolveSetters(styleName ?? style.Name ?? GetUnnamedStyleDisplayName(style), style, stylesByName, resolvedSetters, activePath);
            return resolvedSetters;
        }

        public static IReadOnlyList<ResolvedStyle> ResolveExplicitStyles(string styleNames, MGElementType targetType, INamedStyleResolver stylesByName)
        {
            List<ResolvedStyle> resolvedStyles = new();
            foreach (string styleName in styleNames.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!stylesByName.TryGetStyle(styleName, out Style style))
                {
                    throw new InvalidOperationException($"No style named '{styleName}' was found in the active style scopes.");
                }

                if (style.TargetType == targetType)
                {
                    resolvedStyles.Add(new(styleName, style, ResolveSetters(styleName, style, stylesByName)));
                }
            }

            return resolvedStyles;
        }

        private static void ResolveSetters(string currentStyleName, Style currentStyle, INamedStyleResolver stylesByName, List<Setter> resolvedSetters, List<string> activePath)
        {
            int existingIndex = activePath.IndexOf(currentStyleName);
            if (existingIndex >= 0)
            {
                string cycle = string.Join(" -> ", activePath.Skip(existingIndex).Append(currentStyleName));
                throw new InvalidOperationException($"Detected a cycle while resolving style inheritance: {cycle}");
            }

            activePath.Add(currentStyleName);
            try
            {
                if (!string.IsNullOrWhiteSpace(currentStyle.BasedOn))
                {
                    if (stylesByName == null || !stylesByName.TryGetStyle(currentStyle.BasedOn, out Style baseStyle))
                    {
                        throw new InvalidOperationException($"Style '{currentStyleName}' references missing base style '{currentStyle.BasedOn}'.");
                    }

                    if (baseStyle.TargetType != currentStyle.TargetType)
                    {
                        string baseStyleName = baseStyle.Name ?? currentStyle.BasedOn;
                        throw new InvalidOperationException($"Style '{currentStyleName}' targets '{currentStyle.TargetType}' but its base style '{baseStyleName}' targets '{baseStyle.TargetType}'.");
                    }

                    ResolveSetters(currentStyle.BasedOn, baseStyle, stylesByName, resolvedSetters, activePath);
                }

                foreach (Setter setter in currentStyle.Setters)
                {
                    resolvedSetters.Add(setter);
                }
            }
            finally
            {
                _ = activePath.Remove(currentStyleName);
            }
        }

        private static string GetUnnamedStyleDisplayName(Style style) => $"<unnamed {style.TargetType} style>";
    }
}
