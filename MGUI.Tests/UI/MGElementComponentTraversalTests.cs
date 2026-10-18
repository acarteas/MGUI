using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.UI;

[Collection("Render cache clock")]
public sealed class MGElementComponentTraversalTests
{
    [Fact]
    public void UpdatePreservesPriorityInsertionOrderAndFreshAfterContentsPhase()
    {
        List<string> calls = new();
        Probe owner = Create("owner", calls);
        Probe beforeFirst = Create("before-first", calls);
        Probe beforeSecond = Create("before-second", calls);
        Probe afterFirst = Create("after-first", calls);
        Probe afterAdded = Create("after-added", calls);
        owner.Add(afterFirst, ComponentUpdatePriority.AfterContents);
        owner.Add(beforeFirst, ComponentUpdatePriority.BeforeContents);
        owner.Add(beforeSecond, ComponentUpdatePriority.BeforeContents);
        owner.OnContents = () =>
        {
            calls.Add("contents");
            owner.Add(afterAdded, ComponentUpdatePriority.AfterContents);
        };

        owner.RunUpdate();

        Assert.Equal(new[] { "before-first", "before-second", "contents", "after-first", "after-added", "owner" }, calls);
    }

    [Theory]
    [InlineData(ComponentUpdatePriority.BeforeContents)]
    [InlineData(ComponentUpdatePriority.AfterContents)]
    public void MutationDuringAnUpdateComponentEnumerationStillThrows(ComponentUpdatePriority priority)
    {
        List<string> calls = new();
        Probe owner = Create("owner", calls);
        Probe child = Create("child", calls);
        owner.Add(child, priority);
        child.OnSelf = () => owner.RawComponents.Clear();

        Assert.Throws<InvalidOperationException>(owner.RunUpdate);
        Assert.Equal(new[] { "child" }, calls);
    }

    [Fact]
    public void ReentrantUpdateUsesAnIndependentEnumerator()
    {
        List<string> calls = new();
        Probe owner = Create("owner", calls);
        Probe first = Create("first", calls);
        Probe second = Create("second", calls);
        owner.Add(first, ComponentUpdatePriority.BeforeContents);
        owner.Add(second, ComponentUpdatePriority.BeforeContents);
        bool reentered = false;
        first.OnSelf = () =>
        {
            if (!reentered)
            {
                reentered = true;
                owner.RunUpdate();
            }
        };

        owner.RunUpdate();

        Assert.Equal(new[] { "first", "first", "second", "owner", "second", "owner" }, calls);
    }

    [Fact]
    public void PickingPreservesAllFourPaintStagesAndContentPosition()
    {
        List<string> calls = new();
        Probe owner = Create("owner", calls);
        Probe background = Create("background", calls);
        Probe self = Create("self", calls);
        Probe before = Create("before", calls);
        Probe after = Create("after", calls);
        Probe content = Create("content", calls);
        owner.Add(after, draw: ComponentDrawPriority.AfterContents);
        owner.Add(before, draw: ComponentDrawPriority.BeforeContents);
        owner.Add(background, draw: ComponentDrawPriority.BeforeBackground);
        owner.Add(self, draw: ComponentDrawPriority.BeforeSelf);
        owner.Children.Add(content);
        foreach (Probe element in new[] { owner, background, self, before, after, content })
        {
            element.ObservePicking = true;
        }

        owner.RunPicking();

        Assert.Equal(new[] { "background", "self", "before", "owner", "content", "after" }, calls);
    }

    [Theory]
    [InlineData(ComponentDrawPriority.BeforeBackground)]
    [InlineData(ComponentDrawPriority.BeforeSelf)]
    [InlineData(ComponentDrawPriority.BeforeContents)]
    [InlineData(ComponentDrawPriority.AfterContents)]
    public void MutationDuringAPickingComponentEnumerationStillThrows(ComponentDrawPriority priority)
    {
        List<string> calls = new();
        Probe owner = Create("owner", calls);
        Probe child = Create("child", calls);
        owner.Add(child, draw: priority);
        child.OnEnumerate = () => owner.RawComponents.Clear();

        Assert.Throws<InvalidOperationException>(owner.RunPicking);
    }

    private static Probe Create(string name, List<string> calls)
    {
        Probe element = (Probe)RuntimeHelpers.GetUninitializedObject(typeof(Probe));
        typeof(MGElement).GetField("<InitializationManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(element, new DeferEventsManager(() => { }));
        typeof(MGElement).GetField("<Components>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(element, new List<MGComponentBase>());
        element.Label = name;
        element.Calls = calls;
        element.Children = new();
        return element;
    }

    private sealed class Probe : MGElement
    {
        private Probe() : base(default(MGWindow), MGElementType.Border) { }
        public string Label = "";
        public List<string> Calls = null!;
        public List<MGElement> Children = null!;
        public Action? OnContents;
        public Action? OnSelf;
        public Action? OnEnumerate;
        public bool ObservePicking;
        public List<MGComponentBase> RawComponents => Components;
        public void Add(Probe child, ComponentUpdatePriority update = ComponentUpdatePriority.BeforeContents,
            ComponentDrawPriority draw = ComponentDrawPriority.BeforeContents)
        {
            Components.Add(new MGComponent<Probe>(child, update, draw, false, false, false, false, false, false, false,
                (bounds, _) => bounds));
        }
        public override IEnumerable<MGElement> GetVisualTreeChildren(bool includeInactive, bool includeActive)
        {
            if (ObservePicking)
            {
                Calls.Add(Label);
            }
            OnEnumerate?.Invoke();
            return includeActive ? Children : Array.Empty<MGElement>();
        }
        protected override void UpdateContents(ElementUpdateArgs args)
        {
            OnContents?.Invoke();
            base.UpdateContents(args);
        }
        public override void UpdateSelf(ElementUpdateArgs args)
        {
            Calls.Add(Label);
            OnSelf?.Invoke();
        }
        public void RunUpdate() => Update(new(default, false, false, false, Point.Zero, Rectangle.Empty));
        public void RunPicking() => GetTopmostHoveredElement(new(default, false, false, false, Point.Zero, Rectangle.Empty));
    }
}
