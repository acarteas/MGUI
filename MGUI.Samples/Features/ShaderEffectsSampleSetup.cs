#nullable enable

using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace MGUI.Samples.Features
{
    internal sealed class ShaderEffectsSampleSetup
    {
        internal const string EffectName = "SampleUiEffect";
        internal const string UnavailableEffectName = "SampleUiEffectUnavailable";

        internal bool HasEffect { get; }
        internal string StatusText { get; }
        internal string FailureStatusText { get; }

        internal ShaderEffectsSampleSetup(MGResources Resources, Func<Effect> LoadEffect)
        {
            string? LoadError = TryRegisterEffect(Resources, EffectName, LoadEffect);
            HasEffect = LoadError == null;
            StatusText = HasEffect
                ? "Effect loaded successfully. Compare the live shader buttons with the load-failure preview below."
                : $"Effect load failed: {LoadError} All examples remain available using non-shader rendering.";

            string? SimulatedError = TryRegisterEffect(Resources, UnavailableEffectName,
                () => throw new ContentLoadException("Intentional load failure for this preview."));
            FailureStatusText = $"{SimulatedError} These buttons use their declared fallback fills; no shader is active.";
        }

        private static string? TryRegisterEffect(MGResources Resources, string Name, Func<Effect> LoadEffect)
        {
            Resources.RemoveEffect(Name);
            Effect Effect;
            try
            {
                Effect = LoadEffect() ?? throw new ContentLoadException("The content loader returned no effect.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Shader sample effect '{Name}' could not be loaded: {ex}");
                return ex.Message;
            }

            Resources.AddOrReplaceEffect(Name, Effect);
            return null;
        }

        internal string PrepareXaml(string Source)
        {
            if (HasEffect)
            {
                return Source;
            }

            XDocument Document = XDocument.Parse(Source, LoadOptions.PreserveWhitespace);
            foreach (XElement Element in Document.Descendants().Where(Element =>
                (Element.Name.LocalName == "Image" || Element.Name.LocalName == "NineSliceFillBrush")
                && (string?)Element.Attribute("EffectName") == EffectName))
            {

                // Images and nine-slices have no missing-effect fallback; retain their ordinary texture rendering.
                Element.Attribute("EffectName")?.Remove();
                Element.Attribute("UseStandardParameters")?.Remove();
                Element.Elements().Where(Child => Child.Name.LocalName == "EffectParameter").Remove();
            }

            return Document.ToString(SaveOptions.DisableFormatting);
        }
    }
}
