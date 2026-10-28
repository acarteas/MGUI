using System.Runtime.CompilerServices;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Input;

public sealed class InputHandlerLifetimeTests
{
    [Fact]
    public void AbandonedManualHandlersDoNotRootTheirOwnersAndArePruned()
    {
        InputTracker tracker = new();
        (WeakReference owner, WeakReference mouse, WeakReference keyboard) = CreateHandlers(tracker, true);
        Collect();
        Assert.False(owner.IsAlive);
        Assert.False(mouse.IsAlive);
        Assert.False(keyboard.IsAlive);
        Assert.Empty(tracker.Mouse.Handlers);
        Assert.Empty(tracker.Keyboard.Handlers);
        tracker.Mouse.UpdateHandlers();
        tracker.Keyboard.UpdateHandlers();
        GC.KeepAlive(tracker);
    }

    [Fact]
    public void AutomaticHandlersRemainOwnedByTrackerUntilUnsubscribed()
    {
        InputTracker tracker = new();
        (WeakReference owner, WeakReference mouse, WeakReference keyboard) = CreateHandlers(tracker, false);
        Collect();
        Assert.True(owner.IsAlive);
        Assert.True(mouse.IsAlive);
        Assert.True(keyboard.IsAlive);
        Unsubscribe(tracker);
        Collect();
        Assert.False(owner.IsAlive);
        Assert.False(mouse.IsAlive);
        Assert.False(keyboard.IsAlive);
        Assert.Empty(tracker.Mouse.Handlers);
        Assert.Empty(tracker.Keyboard.Handlers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedHandlersDeliverInputAndSupportIdempotentUnsubscribe(bool manual)
    {
        InputTracker tracker = new();
        Host owner = new();
        MouseHandler mouse = tracker.Mouse.CreateHandler(owner, manual ? null : 50);
        KeyboardHandler keyboard = tracker.Keyboard.CreateHandler(owner, manual ? null : 50);
        int mouseCalls = 0, keyCalls = 0;
        mouse.PressedInside += (_, _) => mouseCalls++;
        keyboard.Pressed += (_, _) => keyCalls++;
        Collect();
        tracker.Update(new(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(16),
            new MouseState(5, 5, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released,
                ButtonState.Released, ButtonState.Released), new KeyboardState(Keys.A)));
        tracker.Mouse.UpdateHandlers();
        tracker.Keyboard.UpdateHandlers();
        if (manual)
        {
            Assert.Equal(0, mouseCalls);
            Assert.Equal(0, keyCalls);
            mouse.ManualUpdate();
            keyboard.ManualUpdate();
        }
        Assert.Equal(1, mouseCalls);
        Assert.Equal(1, keyCalls);
        mouse.Unsubscribe();
        keyboard.Unsubscribe();
        mouse.Unsubscribe();
        keyboard.Unsubscribe();
        Assert.Empty(tracker.Mouse.Handlers);
        Assert.Empty(tracker.Keyboard.Handlers);
        GC.KeepAlive(owner);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, WeakReference, WeakReference) CreateHandlers(InputTracker tracker, bool manual)
    {
        Host owner = new();
        MouseHandler mouse = tracker.Mouse.CreateHandler(owner, manual ? null : 50);
        KeyboardHandler keyboard = tracker.Keyboard.CreateHandler(owner, manual ? null : 50);
        return (new(owner), new(mouse), new(keyboard));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Unsubscribe(InputTracker tracker)
    {
        tracker.Mouse.Handlers[0].Unsubscribe();
        tracker.Keyboard.Handlers[0].Unsubscribe();
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class Host : IMouseHandlerHost, IKeyboardHandlerHost
    {
        public bool IsInside(Vector2 position) => true;
        public Vector2 GetOffset() => Vector2.Zero;
    }
}
