using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    public sealed class RenderCachingSamples : SampleBase
    {
        private readonly MGRenderCache StaticPanel;
        private readonly MGRenderCache DirtyPanel;
        private readonly MGCachedFillBrush Backdrop;
        private readonly MGCachedFillBrush CustomBrush;
        private readonly LocalSwatch Source = new();
        private readonly MGTextBlock Score;
        private TimeSpan Clock;
        private TimeSpan? PulseStarted;
        private int ScoreValue;
        private int Frame;
        private bool Dirty;
        private bool Wide = true;
        private bool LargeScale;
        private bool AlternateTitle;
        public string Mode { get; private set; } = "Normal";
        public bool IsPulsing => PulseStarted.HasValue && Clock - PulseStarted.Value < TimeSpan.FromMilliseconds(400);

        public RenderCachingSamples(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, nameof(Features), "RenderCaching.xaml")
        {
            StaticPanel = Named<MGRenderCache>("StaticPanel");
            DirtyPanel = Named<MGRenderCache>("DirtyPanel");
            Score = Named<MGTextBlock>("Score");
            Backdrop = (MGCachedFillBrush)Named<MGBorder>("Backdrop").BackgroundBrush.NormalValue;
            CustomBrush = (MGCachedFillBrush)Named<MGBorder>("CustomSwatch").BackgroundBrush.NormalValue;
            CustomBrush.Brush = Source;
            Command("CacheNormal", () => SetMode("Normal"));
            Command("CacheBackground", () => SetMode("Background"));
            Command("CachePanel", () => SetMode("Panel"));
            Command("CacheScore", () => { ScoreValue++; Score.SetText($"SCORE {ScoreValue:000000}"); PulseStarted = Clock; });
            Command("CacheVolatile", () => StaticPanel.IsRenderVolatile = !StaticPanel.IsRenderVolatile);
            Command("CacheInvalidate", () => { Source.Alternate = !Source.Alternate; CustomBrush.InvalidateVisual(); });
            Command("CacheTitle", () => { AlternateTitle = !AlternateTitle; Named<MGTextBlock>("StaticTitle").SetText(AlternateTitle ? "STATIC LIBRARY / ART" : "STATIC NAVIGATION / ART"); });
            Command("CacheResize", () => { Wide = !Wide; Named<MGOverlayPanel>("Preview").PreferredHeight = Wide ? 220 : 250; });
            Command("CacheScale", () => { LargeScale = !LargeScale; Window.UIScaleOverride = new MGScaleSettings(); Window.UIScaleOverride.SetUniformScale(LargeScale ? 1.25f : 1f); });
            Command("CacheDirty", () => { Dirty = !Dirty; Named<MGTextBlock>("DirtyText").SetText("Bad workload OFF: this panel is stable."); });
            Window.OnEndUpdate += (_, e) => Tick(e.UA.BA.TotalElapsed);
        }

        private T Named<T>(string name) where T : MGElement => Window.GetElementByName<T>(name);
        private void Command(string name, Action action) => Window.GetResources().AddCommand(name, _ => action());

        private void SetMode(string mode)
        {
            Mode = mode;
            Backdrop.IsCacheEnabled = mode != "Normal";
            CustomBrush.IsCacheEnabled = mode != "Normal";
            StaticPanel.IsCacheEnabled = mode == "Panel";
            DirtyPanel.IsCacheEnabled = mode == "Panel";
            Named<MGTextBlock>("ModeStatus").SetText("Mode: " + mode);
        }

        private void Tick(TimeSpan clock)
        {
            Clock = clock;
            float progress = PulseStarted.HasValue ? (float)((Clock - PulseStarted.Value).TotalMilliseconds / 400) : 1;
            float pulse = progress < 1 ? MathF.Sin(MathF.PI * Math.Clamp(progress, 0, 1)) : 0;
            Score.Foreground = new(Color.Lerp(Color.Gold, Color.White, pulse));
            Score.FontSize = 20 + (int)MathF.Round(4 * pulse);
            if (Dirty)
            {
                Named<MGTextBlock>("DirtyText").SetText($"Bad workload ON: changes every update / {++Frame:000000}");
            }

            var stats = Desktop.RenderCaches.Statistics;
            Named<MGTextBlock>("Diagnostics").SetText($"Desktop totals: hits {stats.Hits} / builds {stats.Builds} / misses {stats.Misses}\nLive bytes {Desktop.RenderCaches.RetainedBytes} / entries {Desktop.RenderCaches.EntryCount}; static: {StaticPanel.LastBypassReason}; volatile: {StaticPanel.IsRenderVolatile}");
        }

        // Bounded, premultiplied, local-coordinate paint with no clock or interaction dependency.
        // The caller owns opaque changes and calls CustomBrush.InvalidateVisual before the next draw.
        private sealed class LocalSwatch : IFillBrush
        {
            public bool Alternate;
            public void Draw(ElementDrawArgs args, MGElement element, Rectangle bounds)
            {
                new MGSolidFillBrush(Alternate ? Color.DarkSlateBlue : Color.Teal).Draw(args, element, bounds);
                Rectangle inset = bounds;
                inset.Inflate(-6, -6);
                if (inset.Width > 0 && inset.Height > 0)
                {
                    new MGSolidFillBrush(Alternate ? Color.MediumPurple : Color.Turquoise).Draw(args, element, inset);
                }
            }
            public IFillBrush Copy() => new LocalSwatch { Alternate = Alternate };
        }
    }
}
