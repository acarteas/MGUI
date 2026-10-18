using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;

namespace MGUI.Shared.Rendering
{
    public partial class DrawTransaction
    {
        private sealed class TransactionCount { internal int Value; }
        private static readonly ConditionalWeakTable<GraphicsDevice, TransactionCount> Lifetimes = new();
        private TransactionCount Lifetime;
        internal bool IsCacheCapture { get; private set; }
        private Point CaptureOrigin;
        internal static bool HasActiveTransactions(GraphicsDevice device)
            => Lifetimes.TryGetValue(device, out var count) && count.Value != 0;
        private void RegisterLifetime()
        {
            Lifetime = Lifetimes.GetOrCreateValue(GD);
            Lifetime.Value++;
        }
        private void ReleaseLifetime()
        {
            if (Lifetime != null)
            {
                Lifetime.Value--;
                Lifetime = null;
            }
        }
        internal void ConfigureCacheCapture(Point screenOrigin)
        {
            IsCacheCapture = true;
            CaptureOrigin = screenOrigin;
        }

        // Explicit screen scissors do not pass through the drawing transform.
        internal Rectangle MapScreenClip(Rectangle bounds)
        {
            if (IsCacheCapture)
            {
                bounds.Offset(-CaptureOrigin.X, -CaptureOrigin.Y);
                bounds = Rectangle.Intersect(bounds, GD.Viewport.Bounds);
            }
            return bounds;
        }
    }
}
