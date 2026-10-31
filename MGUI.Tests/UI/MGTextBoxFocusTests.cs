using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.UI;

public sealed class MGTextBoxFocusTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ReleaseFocusClearsOnlyOwnedCurrentAndQueuedFocus(bool ownsCurrent, bool ownsQueued)
    {
        MGWindow window = CreateWindow();
        MGDesktop desktop = window.Desktop;
        MGTextBox target = new(window);
        MGTextBox other = new(window);
        PropertyInfo current = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler))!;
        PropertyInfo queued = typeof(MGDesktop).GetProperty("QueuedFocusedKeyboardHandler", BindingFlags.Instance | BindingFlags.NonPublic)!;
        current.SetValue(desktop, ownsCurrent ? target : other);
        (ownsQueued ? target : other).RequestFocus();
        int transitions = 0;
        desktop.FocusedKeyboardHandlerChanged += (_, _) => transitions++;

        target.ReleaseFocus();

        Assert.Same(ownsCurrent ? null : other, desktop.FocusedKeyboardHandler);
        Assert.Same(ownsQueued ? null : other, queued.GetValue(desktop));
        Assert.Equal(ownsCurrent ? 1 : 0, transitions);

        // Reproduce the desktop's end-of-update focus promotion; released focus must not return.
        current.SetValue(desktop, queued.GetValue(desktop));
        Assert.NotSame(target, desktop.FocusedKeyboardHandler);
        target.ReleaseFocus();
        Assert.Same(ownsQueued ? null : other, desktop.FocusedKeyboardHandler);
    }

    private static MGWindow CreateWindow()
    {
        MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
        MainRenderer renderer = (MainRenderer)RuntimeHelpers.GetUninitializedObject(typeof(MainRenderer));
        SetField(renderer, "<Host>k__BackingField", new FixedRenderHost());
        SetField(renderer, "<Input>k__BackingField", new InputTracker());
        SetField(renderer, "<FontManager>k__BackingField", new MGUI.Shared.Text.FontManager());
        byte[] fontBytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "NotoSans-Regular.ttf"));
        global::FontStashSharp.FontSystem fontSystem = new();
        fontSystem.AddFont(fontBytes);
        MGUI.FontStashSharp.FontStashSharpTextEngine engine = new();
        engine.AddFontSystem("TestFont", MGUI.Shared.Text.CustomFontStyles.Normal, fontSystem, fontBytes);
        SetField(renderer, "_textEngine", engine);
        SetField(desktop, "<Renderer>k__BackingField", renderer);
        SetField(desktop, "_UIScale", new MGScaleSettings());
        SetField(desktop, "<Resources>k__BackingField", new MGResources(new MGTheme("TestFont")));
        SetField(desktop, "<Windows>k__BackingField", new List<MGWindow>());
        return new MGWindow(desktop, 0, 0, 300, 200);
    }

    private static void SetField(object instance, string name, object value)
    {
        for (Type? type = instance.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }
        }
        throw new MissingFieldException(instance.GetType().FullName, name);
    }

    private sealed class FixedRenderHost : IRenderHost
    {
        public GraphicsDevice GraphicsDevice => null!;
        public event EventHandler<TimeSpan> PreviewUpdate { add { } remove { } }
        public event EventHandler<EventArgs> EndUpdate { add { } remove { } }
        public Rectangle GetBounds() => new(0, 0, 800, 600);
        public MouseState GetMouseState() => default;
        public KeyboardState GetKeyboardState() => default;
        public object GetService(Type serviceType) => null!;
    }
}
