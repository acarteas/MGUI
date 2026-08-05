using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML
{
    [ContentProperty(nameof(Entries))]
    public class ResourceDictionary : ISupportInitialize
    {
        /// <summary>Retains the top-level XAML entries in their source order.</summary>
        public List<object> Entries { get; set; } = new();

        /// <summary>The styles contained in this dictionary. This remains available for existing style-only callers.</summary>
        public List<Style> Styles { get; set; } = new();

        /// <summary>The keyed color resources contained in this dictionary.</summary>
        public List<ColorResource> ColorResources { get; set; } = new();

        void ISupportInitialize.BeginInit()
        {
        }

        void ISupportInitialize.EndInit()
        {
            foreach (Style style in Entries.OfType<Style>())
            {
                Styles.Add(style);
            }

            foreach (ColorResource color in Entries.OfType<ColorResource>())
            {
                ColorResources.Add(color);
            }
        }
    }

    /// <summary>A keyed <see cref="XAMLColor"/> resource declared in a <see cref="ResourceDictionary"/>.</summary>
    public class ColorResource
    {
        public string Key { get; set; }
        public XAMLColor Value { get; set; }
    }
}
