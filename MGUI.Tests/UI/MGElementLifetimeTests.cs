using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Data_Binding;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.UI;

[Collection("Render cache clock")]
public sealed class MGElementLifetimeTests
{
    [Fact]
    public void RemovedConstructedElementIsCollectibleWithWindowAndInputRooted()
    {
        MGWindow window = CreateWindow();
        WeakReference removed = AddAndRemoveElement(window);
        Collect();
        Assert.False(removed.IsAlive);
        window.WindowDataContext = new object();
        window.Left += 10;
        GC.KeepAlive(window);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovedConstructedElementAndBindingAreCollectibleWithSourceWindowRooted(bool useContext)
    {
        MGWindow window = CreateWindow();
        window.WindowDataContext = new { WindowWidth = 60 };
        (WeakReference element, WeakReference binding) = BindAndRemoveElement(window, useContext);
        Collect();
        Assert.False(element.IsAlive);
        Assert.False(binding.IsAlive);
        window.WindowDataContext = new { WindowWidth = 80 };
        GC.KeepAlive(window);
    }

    [Fact]
    public void RemovedRootWindowIsCollectibleWithDesktopAndInputRooted()
    {
        MGDesktop desktop = CreateWindow().Desktop;
        WeakReference window = AddAndRemoveRootWindow(desktop);
        Collect();
        Assert.False(window.IsAlive);
        GC.KeepAlive(desktop);
    }

    [Fact]
    public void RetainedDetachedAndReattachedElementsKeepContextAndPositionNotifications()
    {
        MGWindow window = CreateWindow();
        MGSpacer element = new(window, 20, 20);
        int contexts = 0;
        element.DataContextChanged += (_, _) => contexts++;
        window.SetContent(element);
        object first = new();
        window.WindowDataContext = first;
        Assert.Same(first, element.DataContext);
        Assert.Equal(1, contexts);
        window.SetContent(null);
        object second = new();
        window.WindowDataContext = second;
        Assert.Same(second, element.DataContext);
        Assert.Equal(2, contexts);
        window.SetContent(element);
        Collect();
        Rectangle before = element.LayoutBounds;
        window.Left += 13;
        window.Top += 7;
        window.ValidateWindowSizeAndPosition();
        Assert.Equal(before.X + 13, element.LayoutBounds.X);
        Assert.Equal(before.Y + 7, element.LayoutBounds.Y);
        window.WindowDataContext = first;
        Assert.Equal(3, contexts);
        element.DataContextOverride = second;
        window.WindowDataContext = new object();
        Assert.Same(second, element.DataContext);
        Assert.Equal(4, contexts);
    }

    [Fact]
    public void RemovedNestedWindowAndItsContentAreCollectibleWithParentAndInputRooted()
    {
        MGWindow parent = CreateWindow();
        (WeakReference window, WeakReference content) = AddAndRemoveNestedWindow(parent);
        Collect();
        Assert.False(window.IsAlive);
        Assert.False(content.IsAlive);
        parent.WindowDataContext = new object();
        GC.KeepAlive(parent);
    }

    [Fact]
    public void RetainedNestedWindowInheritsContextBeforeAndAfterReattachment()
    {
        MGWindow parent = CreateWindow();
        MGWindow nested = new(parent, 0, 0, 100, 100);
        MGSpacer content = new(nested, 10, 10);
        nested.SetContent(content);
        int changes = 0;
        content.DataContextChanged += (_, _) => changes++;
        parent.AddNestedWindow(nested);
        object first = new();
        parent.WindowDataContext = first;
        Assert.Same(first, content.DataContext);
        Assert.Equal(1, changes);
        Assert.True(parent.RemoveNestedWindow(nested));
        Collect();
        object second = new();
        parent.WindowDataContext = second;
        Assert.Same(second, content.DataContext);
        Assert.Equal(2, changes);
        parent.AddNestedWindow(nested);
        nested.WindowDataContext = first;
        parent.WindowDataContext = new object();
        Assert.Same(first, content.DataContext);
        Assert.Equal(3, changes);
        nested.WindowDataContext = null;
        Assert.Same(parent.WindowDataContext, content.DataContext);
        Assert.Equal(4, changes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddAndRemoveElement(MGWindow window)
    {
        MGSpacer element = new(window, 20, 20);
        _ = element.MouseHandler;
        _ = element.KeyboardHandler;
        window.SetContent(element);
        window.SetContent(null);
        return new(element);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, WeakReference) AddAndRemoveNestedWindow(MGWindow parent)
    {
        MGWindow nested = new(parent, 0, 0, 100, 100);
        MGSpacer content = new(nested, 10, 10);
        nested.SetContent(content);
        parent.AddNestedWindow(nested);
        Assert.True(parent.RemoveNestedWindow(nested));
        return (new(nested), new(content));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, WeakReference) BindAndRemoveElement(MGWindow window, bool useContext)
    {
        MGSpacer element = new(window, 20, 20);
        DataBinding binding = DataBindingManager.AddBinding(new BindingConfig(
            nameof(MGElement.PreferredWidth), nameof(MGWindow.WindowWidth),
            SourceResolver: new FixedSource(window),
            DataContextResolver: useContext ? DataContextResolver.DataContext : DataContextResolver.Self), element);
        window.SetContent(element);
        window.SetContent(null);
        Assert.Equal(1, element.RemoveDataBindings(true));
        return (new(element), new(binding));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddAndRemoveRootWindow(MGDesktop desktop)
    {
        MGWindow window = new(desktop, 0, 0, 100, 100);
        window.SetContent(new MGSpacer(window, 10, 10));
        desktop.Windows.Add(window);
        desktop.ReconcileRenderRootAttachments();
        desktop.Windows.Remove(window);
        desktop.ReconcileRenderRootAttachments();
        return new(window);
    }

    private sealed record FixedSource(MGWindow Window) : ISourceObjectResolver
    {
        public object ResolveSourceObject(object target) => Window;
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

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
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
