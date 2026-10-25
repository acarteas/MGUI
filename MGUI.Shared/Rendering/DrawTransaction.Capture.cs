using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;

namespace MGUI.Shared.Rendering
{
    public partial class DrawTransaction
    {
        internal sealed class TransactionCount { internal int Value; }
        private sealed class CaptureContext { internal Point Origin; }
        private static readonly ConditionalWeakTable<GraphicsDevice, TransactionCount> Lifetimes = new();

        // Ordinary transactions retain only inline flags; capture origins need storage only during capture.
        private static readonly ConditionalWeakTable<DrawTransaction, CaptureContext> CaptureContexts = new();
        private bool LifetimeRegistered;
        internal bool IsCacheCapture { get; private set; }
        internal static TransactionCount GetLifetime(GraphicsDevice device) => Lifetimes.GetOrCreateValue(device);
        internal static bool HasActiveTransactions(GraphicsDevice device)
            => Lifetimes.TryGetValue(device, out var count) && count.Value != 0;
        private void RegisterLifetime()
        {
            Renderer.TransactionLifetime.Value++;
            LifetimeRegistered = true;
        }
        private void ReleaseLifetime()
        {
            if (LifetimeRegistered)
            {
                Renderer.TransactionLifetime.Value--;
                LifetimeRegistered = false;
            }
            if (IsCacheCapture)
            {
                CaptureContexts.Remove(this);
                IsCacheCapture = false;
            }
        }
        internal void ConfigureCacheCapture(Point screenOrigin)
        {
            CaptureContexts.GetOrCreateValue(this).Origin = screenOrigin;
            IsCacheCapture = true;
        }

        // Explicit screen scissors do not pass through the drawing transform.
        internal Rectangle MapScreenClip(Rectangle bounds)
        {
            if (IsCacheCapture)
            {
                Point origin = CaptureContexts.GetOrCreateValue(this).Origin;
                bounds.Offset(-origin.X, -origin.Y);
                bounds = Rectangle.Intersect(bounds, GD.Viewport.Bounds);
            }
            return bounds;
        }
    }
}
