using System.Collections.Generic;

#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML
{
    [ContentProperty(nameof(Styles))]
    public class ResourceDictionary
    {
        public List<Style> Styles { get; set; } = new();
    }
}
