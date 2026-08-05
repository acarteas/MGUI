using System;

#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML
{
    /// <summary>Represents the v1 static resource syntax until its containing element's resource scope is materialized.</summary>
    internal readonly record struct StaticResourceExpression(string Key)
    {
        private const string Prefix = "{StaticResource ";

        public static bool TryParse(string value, out StaticResourceExpression expression)
        {
            if (value != null
                && value.StartsWith(Prefix, StringComparison.Ordinal)
                && value.EndsWith('}'))
            {
                string key = value[Prefix.Length..^1].Trim();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    expression = new(key);
                    return true;
                }
            }

            expression = default;
            return false;
        }
    }

    /// <summary>Preserves a static color-resource reference while XAML is parsed into strongly typed color properties.</summary>
    public sealed class StaticResource : MarkupExtension
    {
        public string Key { get; set; }

        public StaticResource()
        {
        }

        public StaticResource(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                throw new InvalidOperationException($"{nameof(StaticResource)} requires a non-empty resource key.");
            }

            if (serviceProvider?.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target
                && target.TargetProperty is System.Reflection.PropertyInfo property
                && property.PropertyType == typeof(string))
            {
                return $"{{StaticResource {Key}}}";
            }

            return XAMLColor.FromStaticResource(Key);
        }
    }
}
