using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Brushes.Border_Brushes;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using MGUI.Shared.Helpers;

#if UseWPF
using System.Xaml;
#else
using Portable.Xaml;
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML
{
    public sealed class XAMLLoadOptions
    {
        public NameScopeMode RootNameScopeMode { get; init; } = NameScopeMode.Inherit;
        public string RootNameScopeLabel { get; init; }
    }

    public class XAMLParser
    {
        private const string XMLNameSpaceBaseUri = @"http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        public const string XMLLocalNameSpacePrefix = "MGUI";
        public static readonly string XMLLocalNameSpaceUri = $"clr-namespace:{nameof(MGUI)}.{nameof(Core)}.{nameof(UI)}.{nameof(XAML)};assembly={nameof(MGUI)}.{nameof(Core)}";

        private static readonly string XMLNameSpaces =
            $"xmlns=\"{XMLNameSpaceBaseUri}\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:{XMLLocalNameSpacePrefix}=\"{XMLLocalNameSpaceUri}\"";
            //$"xmlns=\"{XMLLocalNameSpaceUri}\" xmlns:x=\"{XMLNameSpaceBaseUri}\""; // This URI avoids using a prefix for the MGUI namespace

        private static readonly Dictionary<string, string> ElementNameAliases = new()
        {
            { "ContentPresenter", nameof(ContentPresenter) },
            { "HeaderedContentPresenter", nameof(HeaderedContentPresenter) },

            { "Border", nameof(Border) },
            { "Button", nameof(Button) },
            { "CheckBox", nameof(CheckBox) },
            { "ComboBox", nameof(ComboBox) },

            { "ContextMenu", nameof(ContextMenu) },
            { "ContextMenuButton", nameof(ContextMenuButton) },
            { "ContextMenuToggle", nameof(ContextMenuToggle) },
            { "ContextMenuSeparator", nameof(ContextMenuSeparator) },
            { "ContextMenuRadioButton", nameof(ContextMenuRadioButton) },

            { "MenuBar", nameof(MenuBar) },
            { "MenuBarItem", nameof(MenuBarItem) },

            { "TreeView", nameof(TreeView) },
            { "TreeViewItem", nameof(TreeViewItem) },

            { "Expander", nameof(Expander) },
            { "GroupBox", nameof(GroupBox) },
            { "Image", nameof(Image) },
            { "ListBox", nameof(ListBox) },
            { "ListView", nameof(ListView) },
            { "ListViewColumn", nameof(ListViewColumn) },

            { "OverlayHost", nameof(OverlayHost) },
            { "Overlay", nameof(Overlay) },

            { "PasswordBox", nameof(PasswordBox) },
            { "ProgressBar", nameof(ProgressBar) },
            { "RadioButton", nameof(RadioButton) },
            { "RatingControl", nameof(RatingControl) },
            { "Rectangle", nameof(Rectangle) },
            { "ResizeGrip", nameof(ResizeGrip) },
            { "ScrollViewer", nameof(ScrollViewer) },
            { "Separator", nameof(Separator) },
            { "Slider", nameof(Slider) },
            { "Spacer", nameof(Spacer) },
            { "Spoiler", nameof(Spoiler) },
            { "Stopwatch", nameof(Stopwatch) },
            { "TabControl", nameof(TabControl) },
            { "TabItem", nameof(TabItem) },
            { "TextBlock", nameof(TextBlock) },
            { "TextBox", nameof(TextBox) },
            { "Timer", nameof(Timer) },
            { "ToggleButton", nameof(ToggleButton) },
            { "ToolTip", nameof(ToolTip) },
            { "Window", nameof(Window) },

            { "RowDefinition", nameof(RowDefinition) },
            { "ColumnDefinition", nameof(ColumnDefinition) },
            { "GridSplitter", nameof(GridSplitter) },
            { "Grid", nameof(Grid) },
            { "UniformGrid", nameof(UniformGrid) },
            { "DockPanel", nameof(DockPanel) },
            { "StackPanel", nameof(StackPanel) },
            { "OverlayPanel", nameof(OverlayPanel) },

            { "Style", nameof(Style) },
            { "Setter", nameof(Setter) },
            { "ResourceDictionary", nameof(ResourceDictionary) },

            //  Abbreviated names
            { "CP", nameof(ContentPresenter) },
            { "HCP", nameof(HeaderedContentPresenter) },
            { "CM", nameof(ContextMenu) },
            { "CMB", nameof(ContextMenuButton) },
            { "CMT", nameof(ContextMenuToggle) },
            { "CMS", nameof(ContextMenuSeparator) },
            { "CMR", nameof(ContextMenuRadioButton) },
            { "MB", nameof(MenuBar) },
            { "MBI", nameof(MenuBarItem) },
            { "GB", nameof(GroupBox) },
            { "LB", nameof(ListBox) },
            { "LV", nameof(ListView) },
            { "LVC", nameof(ListViewColumn) },
            { "RB", nameof(RadioButton) },
            { "RC", nameof(RatingControl) },
            { "RG", nameof(ResizeGrip) },
            { "SV", nameof(ScrollViewer) },
            { "TC", nameof(TabControl) },
            { "TI", nameof(TabItem) },
            { "TB", nameof(TextBlock) },
            { "TT", nameof(ToolTip) },
            { "TV", nameof(TreeView) },
            { "TVI", nameof(TreeViewItem) },
            { "RD", nameof(RowDefinition) },
            { "CD", nameof(ColumnDefinition) },
            { "GS", nameof(GridSplitter) },
            { "UG", nameof(UniformGrid) },
            { "DP", nameof(DockPanel) },
            { "SP", nameof(StackPanel) },
            { "OP", nameof(OverlayPanel) }
        };

        private static string ValidateXAMLString(string XAMLString)
        {
            XAMLString = XAMLString.Trim();

            //  TODO: First line might not be an XElement. Could be something like a comment,
            //  so we should use more robust logic to insert the namespaces
            int FirstLineBreakIndex = XAMLString.IndexOfAny(new char[] { '\n', '\r' });
            string FirstLine = FirstLineBreakIndex < 0 ? XAMLString : XAMLString.Substring(0, FirstLineBreakIndex);
            if (!FirstLine.Contains(XMLNameSpaces))
            {
                //  Insert the required xml namespaces into the XAML string
                int SpaceIndex = XAMLString.IndexOf(' ');
                int InsertionIndex = XAMLString.IndexOf('>');
                if (SpaceIndex >= 0 && (InsertionIndex < 0 || SpaceIndex < InsertionIndex))
                {
                    XAMLString = $"{XAMLString.Substring(0, SpaceIndex)} {XMLNameSpaces} {XAMLString.Substring(SpaceIndex + 1)}";
                }
                else
                {
                    XAMLString = $"{XAMLString.Substring(0, InsertionIndex)} {XMLNameSpaces} {XAMLString.Substring(InsertionIndex)}";
                }
            }

            //  Replace all element names with their fully-qualified name, such as:
            //  "<Button/>"                 --> "<MGUI:Button/>"
            //  "<Button Content="Foo" />"  --> "<MGUI:Button Content="Foo" />"
            //  "<Button.Content>"          --> "<MGUI:Button.Content>"
            //  Where the "MGUI" XML namespace prefix refers to the XMLLocalNameSpaceUri static string
            XDocument Document = XDocument.Parse(XAMLString);

            foreach (var Element in Document.Descendants())
            {
                string ElementName = Element.Name.LocalName;
                foreach (KeyValuePair<string, string> KVP in ElementNameAliases)
                {
                    if (ElementName.StartsWith(KVP.Key))
                    {
                        Element.Name = XName.Get(ElementName.ReplaceFirstOccurrence(KVP.Key, KVP.Value), XMLLocalNameSpaceUri);
                        break;
                    }
                }
            }

            using (StringWriter SW = new())
            {
                using (XmlWriter XW = XmlWriter.Create(SW, new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true }))
                {
                    Document.WriteTo(XW);
                }

                string Result = SW.ToString();
                return Result;
            }
        }

        private static string PrepareXAMLString(string XAMLString, bool SanitizeXAMLString, bool ReplaceLinebreakLiterals)
        {
            if (SanitizeXAMLString)
            {
                XAMLString = ValidateXAMLString(XAMLString);
            }

            if (ReplaceLinebreakLiterals)
            {
                XAMLString = XAMLString.Replace(@"\n", "&#x0a;");
            }

            return XAMLString;
        }

        private static string RemoveDefaultPresentationNamespace(string xamlString)
        {
            XDocument document = XDocument.Parse(xamlString);
            XAttribute defaultNamespace = document.Root?.Attributes()
                .FirstOrDefault(x => x.IsNamespaceDeclaration && x.Name.Namespace == XNamespace.None && x.Name.LocalName == "xmlns" && x.Value == XMLNameSpaceBaseUri);
            defaultNamespace?.Remove();

            using StringWriter sw = new();
            using XmlWriter xw = XmlWriter.Create(sw, new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true });
            document.WriteTo(xw);
            xw.Flush();
            return sw.ToString();
        }

        /// <param name="SanitizeXAMLString">If true, the given <paramref name="XAMLString"/> will be pre-processed via the following logic:<para/>
        /// 1. Trim leading and trailing whitespace<br/>
        /// 2. Insert required XML namespaces (such as "xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation")<br/>
        /// 3. Replace type names with their fully-qualified names, such as "Button" -> "MGUI:Button" where the "MGUI" namespace prefix points to the URI defined by <see cref="XMLLocalNameSpaceUri"/><para/>
        /// If your XAML already contains fully-qualified types, you probably should set this to false.</param>
        /// <param name="ReplaceLinebreakLiterals">If true, the literal string @"\n" will be replaced with "&#38;#x0a;", which is the XAML encoding of the linebreak character '\n'.<br/>
        /// If false, setting the text of an <see cref="MGTextBlock"/> requires encoding the '\n' character as "&#38;#x0a;"<para/>
        /// See also: <see href="https://stackoverflow.com/a/183435/11689514"/></param>
        public static T Load<T>(MGWindow Window, string XAMLString, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            where T : MGElement
            => Load<T>(Window, XAMLString, null, SanitizeXAMLString, ReplaceLinebreakLiterals);

        /// <param name="LoadOptions">Optional configuration applied to the loaded subtree root before child content and bindings are processed.</param>
        /// <param name="SanitizeXAMLString">If true, the given <paramref name="XAMLString"/> will be pre-processed via the following logic:<para/>
        /// 1. Trim leading and trailing whitespace<br/>
        /// 2. Insert required XML namespaces (such as "xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation")<br/>
        /// 3. Replace type names with their fully-qualified names, such as "Button" -> "MGUI:Button" where the "MGUI" namespace prefix points to the URI defined by <see cref="XMLLocalNameSpaceUri"/><para/>
        /// If your XAML already contains fully-qualified types, you probably should set this to false.</param>
        /// <param name="ReplaceLinebreakLiterals">If true, the literal string @"\n" will be replaced with "&#38;#x0a;", which is the XAML encoding of the linebreak character '\n'.<br/>
        /// If false, setting the text of an <see cref="MGTextBlock"/> requires encoding the '\n' character as "&#38;#x0a;"<para/>
        /// See also: <see href="https://stackoverflow.com/a/183435/11689514"/></param>
        public static T Load<T>(MGWindow Window, string XAMLString, XAMLLoadOptions LoadOptions, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
            where T : MGElement
        {
            XAMLString = PrepareXAMLString(XAMLString, SanitizeXAMLString, ReplaceLinebreakLiterals);
            Element Parsed = (Element)XamlServices.Parse(XAMLString);
            Parsed.ProcessStyles(Window.GetResources());
            return Parsed.ToElement<T>(Window, null, Element =>
            {
                if (LoadOptions != null)
                {
                    Element.NameScopeMode = LoadOptions.RootNameScopeMode;
                    Element.NameScopeLabel = LoadOptions.RootNameScopeLabel;
                }
            });
        }

        /// <param name="SanitizeXAMLString">If true, the given <paramref name="XAMLString"/> will be pre-processed via the following logic:<para/>
        /// 1. Trim leading and trailing whitespace<br/>
        /// 2. Insert required XML namespaces (such as "xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation")<br/>
        /// 3. Replace type names with their fully-qualified names, such as "Button" -> "MGUI:Button" where the "MGUI" namespace prefix points to the URI defined by <see cref="XMLLocalNameSpaceUri"/><para/>
        /// If your XAML already contains fully-qualified types, you probably should set this to false.</param>
        /// <param name="ReplaceLinebreakLiterals">If true, the literal string @"\n" will be replaced with "&#38;#x0a;", which is the XAML encoding of the linebreak character '\n'.<br/>
        /// If false, setting the text of an <see cref="MGTextBlock"/> requires encoding the '\n' character as "&#38;#x0a;"<para/>
        /// See also: <see href="https://stackoverflow.com/a/183435/11689514"/></param>
        public static MGWindow LoadRootWindow(MGDesktop Desktop, string XAMLString, bool SanitizeXAMLString = false, bool ReplaceLinebreakLiterals = true)
        {
            XAMLString = PrepareXAMLString(XAMLString, SanitizeXAMLString, ReplaceLinebreakLiterals);
            Window Parsed = (Window)XamlServices.Parse(XAMLString);
            Parsed.ProcessStyles(Desktop.Resources);
            return Parsed.ToElement(Desktop);
        }

        /// <summary>Parses a top-level <see cref="ResourceDictionary"/> XAML payload containing shared named styles.</summary>
        public static ResourceDictionary LoadStyleDictionary(string xaml, bool sanitizeXamlString = false, bool replaceLinebreakLiterals = true)
        {
            xaml = PrepareXAMLString(xaml, sanitizeXamlString, replaceLinebreakLiterals);
            if (sanitizeXamlString)
            {
                xaml = RemoveDefaultPresentationNamespace(xaml);
            }

            ResourceDictionary parsed = (ResourceDictionary)XamlServices.Parse(xaml);
            if (parsed.Styles.Any(x => x == null || string.IsNullOrWhiteSpace(x.Name)))
            {
                throw new InvalidOperationException($"{nameof(ResourceDictionary)} only supports named styles in this version.");
            }

            return parsed;
        }
    }
}
