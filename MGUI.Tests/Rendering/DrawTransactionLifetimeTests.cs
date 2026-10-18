using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Rendering;

public sealed class DrawTransactionLifetimeTests
{
    [Fact]
    public void HostDeviceReplacementCannotMoveAnExistingRenderersLifetimeCounter()
    {
        GraphicsDevice first = DeviceIdentity(), second = DeviceIdentity();
        MutableHost host = new(first);
        MainRenderer renderer = Renderer(host);
        DrawTransaction transaction = Transaction(renderer);
        Assert.True(Active(first));
        host.Device = second;
        transaction.Dispose();
        Assert.False(Active(first));
        Assert.False(Active(second));
        transaction.Dispose();
        Assert.False(Active(first));
    }

    [Fact]
    public void MultipleRenderersShareDeviceCountAndDisposeIsIdempotent()
    {
        GraphicsDevice device = DeviceIdentity();
        DrawTransaction first = Transaction(Renderer(new MutableHost(device)));
        DrawTransaction second = Transaction(Renderer(new MutableHost(device)));
        first.Dispose(); first.Dispose();
        Assert.True(Active(device));
        second.Dispose();
        Assert.False(Active(device));
    }

    [Fact]
    public void EndDrawFailureStillReleasesLifetime()
    {
        GraphicsDevice device = DeviceIdentity();
        DrawTransaction transaction = Transaction(Renderer(new MutableHost(device)));
        typeof(DrawTransaction).GetField("CurrentContext", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(transaction, DrawContext.Sprites);
        Assert.Throws<NullReferenceException>(() => transaction.Dispose());
        Assert.False(Active(device));
    }

    // These are CPU identity/counter tests only; no fake device paints or establishes GPU resource correctness.
    private static GraphicsDevice DeviceIdentity()
    {
        GraphicsDevice device = (GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice));
        GC.SuppressFinalize(device);
        return device;
    }
    private static MainRenderer Renderer(MutableHost host)
    {
        MainRenderer renderer = (MainRenderer)RuntimeHelpers.GetUninitializedObject(typeof(MainRenderer));
        typeof(MainRenderer).GetField("<Host>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, host);
        return renderer;
    }
    private static DrawTransaction Transaction(MainRenderer renderer)
    {
        DrawTransaction transaction = (DrawTransaction)RuntimeHelpers.GetUninitializedObject(typeof(DrawTransaction));
        typeof(DrawTransaction).GetField("<Renderer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(transaction, renderer);
        typeof(DrawTransaction).GetMethod("RegisterLifetime", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(transaction, null);
        return transaction;
    }
    private static bool Active(GraphicsDevice device)
        => (bool)typeof(DrawTransaction).GetMethod("HasActiveTransactions", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { device })!;
    private sealed class MutableHost(GraphicsDevice device) : IRenderHost
    {
        internal GraphicsDevice Device = device;
        public GraphicsDevice GraphicsDevice => Device;
        public event EventHandler<TimeSpan> PreviewUpdate { add { } remove { } }
        public event EventHandler<EventArgs> EndUpdate { add { } remove { } }
        public Rectangle GetBounds() => default;
        public MouseState GetMouseState() => default;
        public KeyboardState GetKeyboardState() => default;
        public object? GetService(Type serviceType) => null;
    }
}
