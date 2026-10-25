using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;

namespace MGUI.RenderCacheTestHost;

internal static class SourceTests
{
    internal static void Run(GraphicsDevice device)
    {
        using Texture2D texture = new(device, 3, 3);
        texture.SetData(Enumerable.Repeat(Color.Red, 9).ToArray());
        var composite = new MGCompositedFillBrush(new MGTextureFillBrush(new(texture)));
        object owner = new();
        int changes = 0;
        using var graph = new MGRenderCacheSourceDependencies(owner, _ => changes++);
        graph.ReplaceSources(new object[] { composite });
        Check(graph.SupportsRenderCaching && graph.ResourcesAreAlive, "Live texture did not qualify.");
        ulong before = graph.VisualRevision;
        texture.SetData(Enumerable.Repeat(Color.Blue, 9).ToArray());
        Check(graph.VisualRevision == before, "Opaque texture upload was falsely claimed observed.");
        graph.Reconcile();
        Check(graph.VisualRevision > before && !texture.IsDisposed, "Explicit upload invalidation failed or disposed caller texture.");
        bool disposalObserved = false;
        bool forwardedDisposalObserved = false;
        composite.VisualInvalidated += (sender, args) =>
        {
            Check(!graph.ResourcesAreAlive && !composite.SupportsRenderCaching,
                "Forwarded source disposal temporarily restored outer graph eligibility.");
            forwardedDisposalObserved = true;
        };
        texture.Disposing += (sender, args) =>
        {
            Check(!graph.ResourcesAreAlive && !composite.SupportsRenderCaching, "Resource still qualified during its disposal event.");
            disposalObserved = true;
        };
        texture.Dispose();
        Check(disposalObserved && forwardedDisposalObserved && !graph.ResourcesAreAlive && changes > 0, "Disposed texture remained qualified.");
        using BasicEffect effect = new(device);
        var nine = new MGNineSliceFillBrush((Effect)null!, new MonoGame.Extended.Thickness(1), new MGTextureData(new Texture2D(device, 3, 3)));
        using Texture2D nineTexture = nine.TopLeft.Texture;
        using var effects = new MGRenderCacheSourceDependencies(owner, _ => { });
        effects.ReplaceSources(new object[] { nine });
        Check(effects.SupportsRenderCaching, "Texture-only nine slice rejected.");
        nine.Effect = effect;
        Check(!effects.SupportsRenderCaching, "Effect graph qualified without explicit static assertion.");
        nine.Effect = null;
        Check(effects.SupportsRenderCaching, "Removing effect did not restore tracked source support.");
        effects.Dispose();
        Check(!effect.IsDisposed && !nineTexture.IsDisposed, "Dependency tracker disposed caller GPU resources.");
        var (weakOwner, weakGraph) = MakeWeak(nineTexture);
        for (int i = 0; i < 4; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Check(!weakOwner.IsAlive && !weakGraph.IsAlive, "Live GPU resource subscription rooted its owner or graph.");
        GC.KeepAlive(owner);
        GC.KeepAlive(device);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, WeakReference) MakeWeak(Texture2D texture)
    {
        object owner = new();
        var graph = new MGRenderCacheSourceDependencies(owner, static _ => { });
        graph.ReplaceSources(new object[] { new MGTextureFillBrush(new(texture)) });
        return (new(owner), new(graph));
    }
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
