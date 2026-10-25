using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;

namespace MGUI.Tests.UI;

[Collection("Render cache clock")]
public sealed class MGRenderContentObservationTests
{
    [Fact]
    public void RegistrationUsesDistinctRetainedPaintEdgesAndClearsRemovedPublishers()
    {
        MGContentPresenter child = Publisher();
        MGContentPresenter root = Publisher();
        Set(root, "RetainedRenderPaintChildren", new MGElement[] { child, child });
        MGRenderCache boundary = Boundary(root);
        MGRenderContentObservation observer = boundary.RenderContentBoundary;
        observer.EnsureRegistered();
        Assert.Equal(2, observer.PublisherCount);
        Assert.Equal(1, root.RenderContentObserverSlots);
        Assert.Equal(1, child.RenderContentObserverSlots);
        Set(root, "RetainedRenderPaintChildren", Array.Empty<MGElement>());
        observer.MembershipChanged();
        Assert.Equal(0, root.RenderContentObserverSlots);
        Assert.Equal(0, child.RenderContentObserverSlots);
        observer.EnsureRegistered();
        Assert.Equal(1, observer.PublisherCount);
        Assert.Equal(0, child.RenderContentObserverSlots);
        observer.Clear();
        Assert.Equal(0, root.RenderContentObserverSlots);
    }

    [Fact]
    public void WarmRegistrationChecksAllocateNothingAndIndependentBoundaryRemovalIsLocal()
    {
        MGContentPresenter publisher = Publisher();
        MGRenderCache first = Boundary(publisher);
        MGRenderCache second = Boundary(publisher);
        first.RenderContentBoundary.EnsureRegistered();
        second.RenderContentBoundary.EnsureRegistered();
        Assert.Equal(2, publisher.RenderContentObserverSlots);
        for (int index = 0; index < 100; index++)
        {
            first.RenderContentBoundary.EnsureRegistered();
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            first.RenderContentBoundary.EnsureRegistered();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        first.RenderContentBoundary.Clear();
        Assert.Equal(1, publisher.RenderContentObserverSlots);
        Assert.Equal(1, second.RenderContentBoundary.PublisherCount);
        second.RenderContentBoundary.Clear();
        Assert.Equal(0, publisher.RenderContentObserverSlots);
    }

    [Fact]
    public void LivePublisherDoesNotRootAbandonedBoundaryAndDeadSlotsArePruned()
    {
        MGContentPresenter publisher = Publisher();
        for (int cohort = 0; cohort < 24; cohort++)
        {
            WeakReference weak = RegisterAbandonedBoundary(publisher);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.False(weak.IsAlive);
            Assert.Equal(1, publisher.RenderContentObserverSlots);
        }
        MGRenderCache survivor = Boundary(publisher);
        survivor.RenderContentBoundary.EnsureRegistered();
        Assert.Equal(1, publisher.RenderContentObserverSlots);
        survivor.RenderContentBoundary.Clear();
        Assert.Equal(0, publisher.RenderContentObserverSlots);
        GC.KeepAlive(publisher);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RegisterAbandonedBoundary(MGContentPresenter publisher)
    {
        MGRenderCache boundary = Boundary(publisher);
        boundary.RenderContentBoundary.EnsureRegistered();
        return new(boundary);
    }

    // CPU-only observer graphs isolate weak registration from ordinary input/control constructor retention.
    // No fabricated graphics device is constructed or used as GPU evidence.
    private static MGContentPresenter Publisher() => (MGContentPresenter)RuntimeHelpers.GetUninitializedObject(typeof(MGContentPresenter));
    private static MGRenderCache Boundary(MGElement content)
    {
        MGRenderCache boundary = (MGRenderCache)RuntimeHelpers.GetUninitializedObject(typeof(MGRenderCache));
        Set(boundary, "_Content", content);
        boundary.RenderContentBoundary = new(boundary);
        return boundary;
    }
    private static void Set(object owner, string name, object value)
    {
        for (Type? type = owner.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(owner, value);
                return;
            }
        }
        throw new InvalidOperationException(name);
    }
}
