using System.Text.Json;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.RenderCacheTestHost;

internal sealed class ContentBoundaryTests : Game
{
    private readonly Dictionary<string, bool> Results = new();
    private bool Completed;
    private int Frame;

    private ContentBoundaryTests()
    {
        _ = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 640,
            PreferredBackBufferHeight = 480,
            GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = false
        };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        IsFixedTimeStep = false;
    }

    internal static int RunAllNative()
    {
        using ContentBoundaryTests game = new();
        game.Run();
        Check(game.Completed, "Content-boundary checks did not complete.");
        Console.WriteLine("RESULT:" + JsonSerializer.Serialize(game.Results));
        Console.WriteLine("EVIDENCE:MonoGame DesktopGL Release; exact full-frame native RGBA; moving world; renderer recorded by test-native.py.");
        return 0;
    }

    protected override void LoadContent()
    {
        using BenchmarkFixture fixture = new(this, new("hud", 640, 480, 1, 1729, 0, 1, "", ""));
        string evidence = Path.Combine(Path.GetTempPath(), "mgui-content-boundary-" + Environment.ProcessId);
        Directory.CreateDirectory(evidence);
        int comparisons = 0;
        MGDesktop desktop = fixture.Desktop;
        desktop.Windows.Clear();
        MGWindow window = new(desktop, 40, 50, 240, 180)
        {
            WindowStyle = WindowStyle.None, Padding = new(0), BackgroundBrush = new((IFillBrush)null!)
        };
        desktop.Windows.Add(window);
        MGRenderCache boundary = new(window) { Padding = new(7), BackgroundBrush = new(new MGSolidFillBrush(Color.DarkSlateGray)) };
        Probe content = new(window);
        boundary.SetContent(content);
        window.SetContent(boundary);
        void Update(bool prepare = true)
        {
            fixture.Update(++Frame);
            if (prepare)
            {
                desktop.PrepareRenderCaches();
            }
        }
        Color[] Paint(int world, MGElement? element = null, float opacity = 1, Rectangle? clip = null)
        {
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.ScissorRectangle = GraphicsDevice.Viewport.Bounds;
            GraphicsDevice.Clear(new Color(13 + world % 23, 27, 41));
            long switches = GraphicsDevice.Metrics.TargetCount;
            using (DrawTransaction transaction = new(desktop.Renderer, DrawSettings.Default, true))
            {
                transaction.FillRectangle(Vector2.Zero, new Rectangle(450 + world % 31, 380, 32, 28), Color.Lime);
                using (clip.HasValue ? transaction.SetClipTargetTemporary(clip.Value, false) : null)
                {
                    MGElement painter = element ?? window;
                    painter.Draw(new(new(TimeSpan.Zero, transaction, opacity), painter.VisualState, Point.Zero));
                }
            }
            Check(GraphicsDevice.Metrics.TargetCount == switches, "Final Draw switched render targets.");
            Color[] pixels = new Color[640 * 480];
            GraphicsDevice.GetBackBufferData(pixels);
            Check(pixels[390 * 640 + 455 + world % 31] == Color.Lime, "World sentinel was destroyed.");
            return pixels;
        }
        void Warm()
        {
            boundary.IsCacheEnabled = true;
            for (int settle = 0; settle < 4; settle++)
            {
                Update();
                Paint(Frame);
            }
            long hits = desktop.RenderCaches.Statistics.Hits;
            Update();
            Paint(Frame);
            Check(desktop.RenderCaches.Statistics.Hits == hits + 1, "Boundary did not warm: " + content.GetRenderEligibilitySnapshot());
        }
        void ExactAgainstDirect(Color[] cached, int world)
        {
            boundary.IsCacheEnabled = false;
            Color[] direct = Paint(world);
            Check(cached.SequenceEqual(direct), "Cached/direct full-frame RGBA mismatch.");
            comparisons++;
            using Texture2D image = new(GraphicsDevice, 640, 480);
            foreach (var item in new[] { ("cached", cached), ("direct", direct) })
            {
                image.SetData(item.Item2);
                using FileStream file = File.Create(Path.Combine(evidence, $"{comparisons}-{item.Item1}.png"));
                image.SaveAsPng(file, 640, 480);
            }
        }

        Check(boundary.IsCacheEnabled && boundary.ElementType == MGElementType.RenderCache, "Runtime defaults differ.");
        MGSpacer empty = new(window, 0, 0)
        {
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        boundary.SetContent(empty);
        for (int settle = 0; settle < 4; settle++)
        {
            Update();
            Paint(0);
        }
        Check(empty.LayoutBounds.Width == 0 && empty.LayoutBounds.Height == 0 && boundary.LayoutBounds.Width > 0,
            "Zero-size content test did not create a full-size boundary with empty child geometry.");
        Check(desktop.RenderCaches.Statistics.Allocations == 0, "Initially empty content allocated a target.");
        boundary.SetContent(content);
        for (int settle = 0; settle < 3; settle++)
        {
            Update();
        }
        Check(desktop.RenderCaches.Statistics.Allocations == 0, "Unobserved content allocated.");
        int paints = content.Paints;
        Paint(1);
        Check(content.Paints == paints + 1, "First miss skipped direct paint.");
        Update();
        paints = content.Paints;
        Color[] firstHit = Paint(2);
        Check(content.Paints == paints && desktop.RenderCaches.Statistics.Hits > 0, "First prepared hit painted descendants.");
        ExactAgainstDirect(firstHit, 2);
        Results["first-observation-defaults-and-exact-native-parity"] = true;

        Warm();
        paints = content.Paints;
        int updates = content.Updates;
        long builds = desktop.RenderCaches.Statistics.Builds;
        long allocations = desktop.RenderCaches.Statistics.Allocations;
        for (int idle = 0; idle < 24; idle++)
        {
            Update();
            content.Discoveries = 0;
            Paint(Frame);
            Check(content.Discoveries == 0, "Warm paint discovered descendants.");
            Check(((IMouseHandlerHost)content).CanReceiveMouseInput(), "Warm hit disabled ordinary mouse input eligibility.");
        }
        Check(content.Paints == paints && content.Updates == updates + 24, "Warm hit skipped updates or repeated content painting.");
        Check(desktop.RenderCaches.Statistics.Builds == builds && desktop.RenderCaches.Statistics.Allocations == allocations,
            "Idle content rebuilt or allocated.");
        Check(content.RenderContentObserverSlots == 1 && boundary.RenderContentBoundary.PublisherCount == 1, "Idle observer churn.");
        Results["warm-paint-skip-live-updates-moving-world-and-no-rebuilds"] = true;

        Update(false);
        paints = content.Paints;
        Color[] omitted = Paint(7);
        Check(content.Paints == paints + 1, "Omitted Prepare reused an old target.");
        ExactAgainstDirect(omitted, 7);
        Warm();
        Update();
        content.Tint = Color.CornflowerBlue;
        content.InvalidateVisual();
        paints = content.Paints;
        Color[] late = Paint(8);
        Check(content.Paints == paints + 1, "Late mutation reused stale pixels.");
        ExactAgainstDirect(late, 8);
        Warm();
        Results["omitted-prepare-and-late-mutation-direct-parity"] = true;

        Rectangle previousBounds = content.LayoutBounds;
        content.UpdateLayout(new Rectangle(previousBounds.Location, Point.Zero));
        Update();
        Paint(8);
        Check(content.LayoutBounds.Width == 0 && content.LayoutBounds.Height == 0, "Zero-size transition did not persist.");
        Check(desktop.RenderCaches.EntryCount == 0 && content.RenderContentObserverSlots == 0,
            "Warm content becoming empty retained its target or publishers.");
        content.UpdateLayout(previousBounds);
        Warm();
        Results["initial-and-transition-empty-content-never-allocate"] = true;

        Update();
        Paint(9, content, 1, new Rectangle(0, 0, 1, 1));
        Check(content.RecentDrawWasClipped, "Alternate child draw did not change clipping bookkeeping.");
        paints = content.Paints;
        Paint(9);
        Check(content.Paints == paints + 1 && !content.RecentDrawWasClipped, "Alternate child observation reused stale bookkeeping.");
        Warm();
        Update();
        Paint(10, boundary, 1, new Rectangle(0, 0, 1, 1));
        paints = content.Paints;
        Paint(10);
        Check(content.Paints == paints + 1, "Skipped boundary content did not retire its observation.");
        Results["alternate-descendant-and-skipped-boundary-draw-retire"] = true;

        Warm();
        int hooks = 0;
        EventHandler<MGElement.MGElementDrawEventArgs> hook = (_, _) => hooks++;
        boundary.OnBeginDraw += hook;
        Warm();
        hooks = 0;
        paints = content.Paints;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            Update();
            Paint(Frame);
        }
        Check(hooks == 3 && content.Paints == paints, "Wrapper hook entered capture or stopped warm content reuse.");
        boundary.OnBeginDraw -= hook;
        content.OnEndDraw += hook;
        Update();
        paints = content.Paints;
        Paint(11);
        Check(content.Paints == paints + 1, "Captured public descendant callback.");
        content.OnEndDraw -= hook;
        Results["live-wrapper-hooks-and-descendant-hook-fallback"] = true;

        Warm();
        Update();
        EventHandler<MGElement.MGElementDrawEventArgs> alternate = (_, args) => content.Draw(args.DA.SetOpacity(0.25f));
        boundary.OnEndingDraw += alternate;
        Paint(12);
        boundary.OnEndingDraw -= alternate;
        paints = content.Paints;
        Paint(12);
        Check(content.Paints == paints + 1, "Wrapper after-content alternate draw was incorrectly suppressed.");
        Results["wrapper-hook-alternate-draw-is-not-own-content-traversal"] = true;

        void ReentrantHookCase(int stage, bool throwAfterInner)
        {
            bool enabled = false;
            bool inside = false;
            EventHandler<MGElement.MGElementDrawEventArgs> handler = (_, args) =>
            {
                if (enabled && !inside)
                {
                    inside = true;
                    try
                    {
                        Rectangle innerClip = stage == 0 || throwAfterInner
                            ? GraphicsDevice.Viewport.Bounds : new Rectangle(0, 0, 1, 1);
                        using (args.DA.DT.SetClipTargetTemporary(innerClip, false))
                        {
                            boundary.Draw(args.DA);
                        }
                    }
                    finally
                    {
                        inside = false;
                    }
                    if (throwAfterInner)
                    {
                        throw new ReentrantProbeException();
                    }
                }
            };
            if (stage == 0)
            {
                boundary.OnBeginDraw += handler;
            }
            else if (stage == 1)
            {
                boundary.OnEndingDraw += handler;
            }
            else
            {
                boundary.OnEndDraw += handler;
            }
            Warm();
            Update();
            enabled = true;
            bool threw = false;
            try
            {
                Paint(12, boundary, 1, stage == 0 ? new Rectangle(0, 0, 1, 1) : null);
            }
            catch (ReentrantProbeException)
            {
                threw = true;
            }
            finally
            {
                enabled = false;
            }
            Check(threw == throwAfterInner, "Reentrant exception scenario did not execute as intended.");
            if (stage == 0 && !throwAfterInner)
            {
                Check(boundary.RecentDrawWasClipped, "Outer reentrant begin draw did not skip its content stage.");
            }
            int beforePaints = content.Paints;
            long beforeHits = desktop.RenderCaches.Statistics.Hits;
            Paint(12);
            Check(content.Paints == beforePaints + 1 && desktop.RenderCaches.Statistics.Hits == beforeHits,
                $"Reentrant stage {stage}, exception {throwAfterInner}: next draw reused an incompatible observation.");
            Warm();
            if (stage == 0)
            {
                boundary.OnBeginDraw -= handler;
            }
            else if (stage == 1)
            {
                boundary.OnEndingDraw -= handler;
            }
            else
            {
                boundary.OnEndDraw -= handler;
            }
        }
        for (int stage = 0; stage < 3; stage++)
        {
            ReentrantHookCase(stage, false);
            ReentrantHookCase(stage, true);
        }
        Results["reentrant-begin-ending-end-and-exception-observations-are-per-invocation"] = true;


        Warm();
        boundary.IsCacheEnabled = false;
        Check(content.RenderContentObserverSlots == 0, "Disable retained observation publishers.");
        Update();
        Check(desktop.RenderCaches.EntryCount == 0, "Disabled entry was not safely released.");
        Warm();
        boundary.SetContent((MGElement)null!);
        Check(content.RenderContentObserverSlots == 0, "Empty boundary retained publishers.");
        Update();
        Paint(13);
        Check(desktop.RenderCaches.EntryCount == 0, "Empty boundary allocated or retained target.");
        boundary.SetContent(content);
        Warm();
        content.Visibility = Visibility.Hidden;
        Update();
        Paint(14);
        Check(desktop.RenderCaches.EntryCount == 0 && content.RenderContentObserverSlots == 0, "Hidden content retained target/observer.");
        content.Visibility = Visibility.Visible;
        Warm();
        Results["disable-empty-hidden-retire-and-recover"] = true;

        desktop.Windows.Remove(window);
        Update();
        Check(desktop.RenderCaches.EntryCount == 0 && content.RenderContentObserverSlots == 0, "Root detach retained target/publishers.");
        desktop.Windows.Add(window);
        Update();
        paints = content.Paints;
        Paint(15);
        Check(content.Paints == paints + 1, "Reinsert bypassed new direct observation.");
        Warm();
        window.SetContent(new MGSpacer(window, 20, 20));
        Update();
        Check(content.RenderContentObserverSlots == 0 && desktop.RenderCaches.EntryCount == 0, "Managed detach retained publishers/target.");
        window.SetContent(boundary);
        Warm();
        Results["raw-root-and-managed-detach-require-new-observation"] = true;

        window.SetContent((MGElement)null!);
        MGContentPresenter branch = new(window);
        branch.SetContent(boundary);
        MGContextualContentPresenter contextual = new(window, true, branch, new MGSpacer(window, 10, 10));
        window.SetContent(contextual);
        Warm();
        MGElement previousParent = branch.Parent;
        contextual.Value = false;
        Check(ReferenceEquals(branch.Parent, previousParent), "Inactive-branch test unexpectedly cleared logical Parent.");
        Check(content.RenderContentObserverSlots == 0 && boundary.RenderContentBoundary.PublisherCount == 0,
            "Inactive nested boundary retained observation publishers.");
        Update();
        Paint(15);
        Check(desktop.RenderCaches.EntryCount == 0, "Inactive branch retained or rebuilt its target.");
        contextual.Value = true;
        Warm();
        window.SetContent((MGElement)null!);
        branch.SetContent((MGElement)null!);
        window.SetContent(boundary);
        Warm();
        Results["inactive-nested-paint-branch-cleans-observers-with-logical-parent-retained"] = true;

        content.IsRenderVolatile = true;
        Update();
        paints = content.Paints;
        Paint(16);
        Check(content.Paints == paints + 1 && desktop.RenderCaches.EntryCount == 0, "Volatile content reused cache.");
        content.IsRenderVolatile = false;
        Warm();
        boundary.IsRenderVolatile = true;
        Update();
        paints = content.Paints;
        Paint(16);
        Check(content.Paints == paints + 1, "Owner volatility was ignored.");
        boundary.IsRenderVolatile = false;
        Warm();
        Update();
        paints = content.Paints;
        Paint(17, opacity: 0.5f);
        Check(content.Paints == paints + 1, "Partial opacity reused flattened group.");
        Results["content-owner-volatility-and-incoming-opacity-fallback"] = true;

        MGRenderCache inner = new(window);
        inner.SetContent(content);
        boundary.SetContent(inner);
        Warm();
        paints = content.Paints;
        long beforeBuilds = desktop.RenderCaches.Statistics.Builds;
        Update();
        Paint(18);
        Check(content.Paints == paints && desktop.RenderCaches.Statistics.Builds == beforeBuilds,
            "Nested capture did not stabilize.");
        Color[] nested = Paint(18);
        inner.IsCacheEnabled = false;
        ExactAgainstDirect(nested, 18);
        boundary.SetContent(content);
        Warm();
        Results["nested-capture-direct-bypass-and-native-parity"] = true;

        for (int cycle = 0; cycle < 20; cycle++)
        {
            Probe replacement = new(window);
            boundary.SetContent(replacement);
            Warm();
            Check(content.RenderContentObserverSlots == 0 && replacement.RenderContentObserverSlots == 1, "Replacement publisher cleanup failed.");
            boundary.SetContent(content);
            Warm();
            Check(replacement.RenderContentObserverSlots == 0 && content.RenderContentObserverSlots == 1, "Observer slots accumulated on removed content.");
        }
        Results["repeated-content-replacement-has-no-observer-slot-growth"] = true;

        MGStackPanel panel = new(window, Orientation.Vertical);
        MGTextBlock label = new(window, "STATIC PANEL", Color.White, 18);
        panel.TryAddChild(label);
        panel.TryAddChild(new MGRectangle(window, 90, 24, Color.Black, 1, Color.Gold));
        boundary.SetContent(panel);
        Warm();
        Color[] panelPixels = Paint(20);
        ExactAgainstDirect(panelPixels, 20);
        Warm();
        Update();
        label.Text = "CHANGED PANEL";
        Color[] changedPanel = Paint(21);
        ExactAgainstDirect(changedPanel, 21);
        Check(!panelPixels.SequenceEqual(changedPanel), "Built-in text mutation did not change the panel.");
        boundary.SetContent(content);
        Warm();
        Results["built-in-text-panel-exact-parity-and-revision-change"] = true;

        boundary.SetContent((MGElement)null!);
        foreach (bool explicitSupport in new[] { false, true })
        {
            window.SetContent((MGElement)null!);
            MGRenderCache derived = explicitSupport ? new OptedInEnumeratingBoundary(window) : new EnumeratingBoundary(window);
            content.Opacity = 0.5f;
            derived.SetContent(content);
            window.SetContent(derived);
            desktop.RenderCaches.Clear();
            long subtypeAllocations = desktop.RenderCaches.Statistics.Allocations;
            long subtypeHits = desktop.RenderCaches.Statistics.Hits;
            Color[]? subtypePixels = null;
            for (int iteration = 0; iteration < 6; iteration++)
            {
                Update();
                int beforePaints = content.Paints;
                subtypePixels = Paint(22);
                Check(content.Paints == beforePaints + 2, "Derived wrapper's ordinary repeated-child traversal changed.");
                Check(desktop.RenderCaches.EntryCount == 0 && desktop.RenderCaches.PendingCount == 0,
                    "Derived wrapper queued or retained a Content-only target.");
            }
            Check(desktop.RenderCaches.Statistics.Allocations == subtypeAllocations
                && desktop.RenderCaches.Statistics.Hits == subtypeHits, "Derived wrapper cached its unverifiable traversal.");
            Check(derived.GetRenderEligibilitySnapshot().IsEligible == explicitSupport,
                "Subtype explicit participation classification changed.");
            derived.IsCacheEnabled = false;
            Check(subtypePixels!.SequenceEqual(Paint(22)), "Derived fallback pixels differ from disabled traversal.");
            derived.SetContent((MGElement)null!);
            window.SetContent((MGElement)null!);
        }
        content.Opacity = 1;
        boundary.SetContent(content);
        window.SetContent(boundary);
        Warm();
        Results["unknown-and-explicitly-supported-derived-wrapper-traversal-bypasses-own-cache"] = true;

        desktop.DisposeRenderCaches();
        Check(desktop.RenderCaches.EntryCount == 0, "Desktop disposal retained targets.");
        paints = content.Paints;
        Paint(19);
        Check(content.Paints == paints + 1, "Disposed manager did not fall back to direct draw.");
        Results["desktop-disposal-preserves-direct-paint"] = true;
        File.WriteAllText(Path.Combine(evidence, "report.json"), JsonSerializer.Serialize(new
        {
            backend = "MonoGame DesktopGL", configuration = "Release", exactFullFrameComparisons = comparisons,
            statistics = desktop.RenderCaches.Statistics, checks = Results
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("ARTIFACTS:" + evidence);
        Completed = true;
        Exit();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ReentrantProbeException : Exception { }

    private class EnumeratingBoundary : MGRenderCache
    {
        public EnumeratingBoundary(MGWindow window) : base(window) { }
        public override IEnumerable<MGElement> GetChildren()
        {
            if (HasContent)
            {
                yield return Content;
                yield return Content;
            }
        }
    }

    private sealed class OptedInEnumeratingBoundary : EnumeratingBoundary
    {
        public OptedInEnumeratingBoundary(MGWindow window) : base(window) { }
        protected override bool SupportsRenderCaching => true;
    }

    private sealed class Probe : MGElement
    {
        public Color Tint = Color.OrangeRed;
        public int Paints;
        public int Updates;
        public int Discoveries;
        protected override bool SupportsRenderCaching => true;
        public Probe(MGWindow window) : base(window, MGElementType.Rectangle) { }
        public override IEnumerable<MGElement> GetChildren()
        {
            Discoveries++;
            return Array.Empty<MGElement>();
        }
        public override void UpdateSelf(ElementUpdateArgs args) { Updates++; }
        public override void DrawSelf(ElementDrawArgs args, Rectangle bounds)
        {
            Paints++;
            new MGSolidFillBrush(Tint).Draw(args, this, bounds);
            new MGSolidFillBrush(Color.DarkBlue).Draw(args, this, new Rectangle(bounds.X + 8, bounds.Y + 9, 17, 13));
        }
    }
}
