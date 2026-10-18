using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.UI
{
    [Collection("Render cache clock")]
    public sealed class MGElementRenderRevisionTests
    {
        [Fact]
        public void RevisionsStartAtOneAndReadsDoNotAdvanceThem()
        {
            TestElement element = CreateElement();
            MGRenderRevisionSnapshot initial = element.GetRenderRevisionSnapshot();

            Assert.Equal(1UL, element.VisualRevision);
            Assert.Equal(1UL, element.LayoutRevision);
            Assert.Equal(1UL, element.StructureRevision);
            Assert.Equal(initial, element.GetRenderRevisionSnapshot());
        }

        [Fact]
        public void ExplicitVisualInvalidationPreservesLayoutAndMeasurementsAndRequestsReconciliation()
        {
            TestElement element = CreateElement();
            SeedMeasurement(element);
            MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();
            MGRenderInvalidationKind notified = MGRenderInvalidationKind.None;
            element.RenderInvalidated += (_, kind, _) => notified |= kind;

            element.InvalidateVisual();

            Assert.Equal(before.Visual + 1, element.VisualRevision);
            Assert.Equal(before.Layout, element.LayoutRevision);
            Assert.Equal(before.Structure, element.StructureRevision);
            Assert.True(element.IsLayoutValid);
            Assert.True(element.HasCachedMeasurement());
            Assert.Equal(MGRenderInvalidationKind.Visual | MGRenderInvalidationKind.Dependencies, notified);
        }

        [Fact]
        public void LayoutInvalidationAdvancesVisualAndLayoutAndClearsMeasurements()
        {
            TestElement element = CreateElement();
            SeedMeasurement(element);
            MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();

            element.InvalidateLayout();

            Assert.Equal(before.Visual + 1, element.VisualRevision);
            Assert.Equal(before.Layout + 1, element.LayoutRevision);
            Assert.Equal(before.Structure, element.StructureRevision);
            Assert.False(element.IsLayoutValid);
            Assert.False(element.HasCachedMeasurement());
            Assert.Empty(GetMeasurements(element, "RecentMeasurementsFull"));
            Assert.Empty(GetMeasurements(element, "RecentMeasurementsSelfOnly"));
        }

        [Fact]
        public void StructureInvalidationDoesNotForceMeasurement()
        {
            TestElement element = CreateElement();
            SeedMeasurement(element);
            MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();

            element.InvalidateRenderState(MGRenderInvalidationKind.Structure);

            Assert.Equal(before.Visual + 1, element.VisualRevision);
            Assert.Equal(before.Layout, element.LayoutRevision);
            Assert.Equal(before.Structure + 1, element.StructureRevision);
            Assert.True(element.IsLayoutValid);
            Assert.True(element.HasCachedMeasurement());
        }

        [Fact]
        public void EqualLayoutSetterAndBookkeepingNotificationsDoNotInvalidate()
        {
            TestElement element = CreateElement();
            element.PreferredWidth = 20;
            SeedMeasurement(element);
            MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();

            element.PreferredWidth = 20;
            element.Tag = new object();
            element.NotifyBookkeeping();

            Assert.True(element.IsRenderRevisionCurrent(before));
            Assert.True(element.HasCachedMeasurement());
        }

        [Fact]
        public void DeferredLayoutMutationInvalidatesSnapshotBeforeMeasurementsAreCleared()
        {
            TestElement element = CreateElement();
            SeedMeasurement(element);
            MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();

            using (element.BeginInitializing())
            {
                element.PreferredWidth = 20;

                Assert.False(element.IsRenderRevisionCurrent(before));
                Assert.True(element.IsLayoutValid);
                Assert.True(element.HasCachedMeasurement());
            }

            Assert.False(element.IsLayoutValid);
            Assert.Empty(GetMeasurements(element, "RecentMeasurementsFull"));
        }

        [Fact]
        public void DeferredExplicitInvalidationCannotBeAcknowledgedByAnOlderCapture()
        {
            TestElement element = CreateElement();
            using (element.BeginInitializing())
            {
                MGRenderRevisionSnapshot captured = element.GetRenderRevisionSnapshot();
                element.InvalidateVisual();
                MGRenderRevisionSnapshot newer = element.GetRenderRevisionSnapshot();
                element.InvalidateVisual();

                Assert.False(element.IsRenderRevisionCurrent(captured));
                Assert.False(element.IsRenderRevisionCurrent(newer));
                Assert.True(element.IsRenderRevisionCurrent(element.GetRenderRevisionSnapshot()));
                Assert.False(element.IsRenderRevisionCurrent(captured));
            }
        }

        [Fact]
        public void SameMutationCoalescesAliasesButLaterMutationRemainsVisible()
        {
            TestElement element = CreateElement();
            MGRenderMutationStamp stamp = MGRenderRevisionClock.NextMutation();
            int notifications = 0;
            element.RenderInvalidated += (_, _, _) => ++notifications;
            element.InvalidateRenderState(MGRenderInvalidationKind.Layout, stamp);
            MGRenderRevisionSnapshot captured = element.GetRenderRevisionSnapshot();

            element.InvalidateRenderState(MGRenderInvalidationKind.Layout, stamp);
            Assert.True(element.IsRenderRevisionCurrent(captured));
            Assert.Equal(1, notifications);

            element.InvalidateRenderState(MGRenderInvalidationKind.Structure, stamp);
            Assert.Equal(captured.Visual, element.VisualRevision);
            Assert.Equal(captured.Structure + 1, element.StructureRevision);

            element.InvalidateVisual();
            Assert.False(element.IsRenderRevisionCurrent(captured));
            Assert.Equal(captured.Visual + 1, element.VisualRevision);
        }

        [Fact]
        public void ReentrantNewMutationCannotBeErasedByOlderPropagation()
        {
            TestElement element = CreateElement();
            MGRenderMutationStamp original = MGRenderRevisionClock.NextMutation();
            MGRenderRevisionSnapshot capture = default;
            bool reentered = false;
            element.RenderInvalidated += (_, _, _) =>
            {
                if (!reentered)
                {
                    reentered = true;
                    capture = element.GetRenderRevisionSnapshot();
                    element.InvalidateVisual();
                }
            };

            element.InvalidateRenderState(MGRenderInvalidationKind.Visual, original);
            MGRenderRevisionSnapshot latest = element.GetRenderRevisionSnapshot();
            element.InvalidateRenderState(MGRenderInvalidationKind.Visual, original);

            Assert.False(element.IsRenderRevisionCurrent(capture));
            Assert.True(element.IsRenderRevisionCurrent(latest));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LayoutParentForwardingCoalescesWithTypedPropagationWithoutHidingReentrantMutation(bool deferParent)
        {
            TestElement parent = CreateElement();
            TestElement child = CreateElement();
            SetField(child, "_Parent", parent);
            MGRenderRevisionSnapshot initial = parent.GetRenderRevisionSnapshot();
            MGRenderRevisionSnapshot duringCapture = default;
            bool reentered = false;
            child.RenderInvalidated += (_, kind, stamp) => parent.InvalidateRenderState(kind, stamp);
            parent.RenderInvalidated += (_, _, _) =>
            {
                if (!reentered)
                {
                    reentered = true;
                    duringCapture = parent.GetRenderRevisionSnapshot();
                    parent.PreferredWidth = 31;
                }
            };

            using (deferParent ? parent.BeginInitializing() : null)
            {
                child.PreferredWidth = 20;
                MGRenderRevisionSnapshot latest = parent.GetRenderRevisionSnapshot();

                Assert.Equal(initial.Visual + 2, latest.Visual);
                Assert.Equal(initial.Layout + 2, latest.Layout);
                Assert.False(parent.IsRenderRevisionCurrent(duringCapture));
                Assert.True(parent.IsRenderRevisionCurrent(latest));
            }
        }

        [Fact]
        public void ParentOverrideNestedSetterDoesNotConsumeForwardedChildStamp()
        {
            TestElement parent = CreateElement();
            TestElement child = CreateElement();
            SetField(child, "_Parent", parent);
            child.RenderInvalidated += (_, kind, stamp) => parent.InvalidateRenderState(kind, stamp);
            MGRenderRevisionSnapshot before = parent.GetRenderRevisionSnapshot();
            bool nested = false;
            parent.BeforeLayoutChanged = () =>
            {
                if (!nested)
                {
                    nested = true;
                    parent.PreferredHeight = 42;
                }
            };

            child.PreferredWidth = 20;

            Assert.Equal(before.Visual + 2, parent.VisualRevision);
            Assert.Equal(before.Layout + 2, parent.LayoutRevision);
            MGRenderRevisionSnapshot after = parent.GetRenderRevisionSnapshot();
            parent.PreferredHeight = 43;
            Assert.False(parent.IsRenderRevisionCurrent(after));
            Assert.Equal(after.Visual + 1, parent.VisualRevision);
        }

        [Fact]
        public void ThrowingLayoutOverrideRestoresForwardingBridge()
        {
            TestElement parent = CreateElement();
            TestElement child = CreateElement();
            SetField(child, "_Parent", parent);
            child.RenderInvalidated += (_, kind, stamp) => parent.InvalidateRenderState(kind, stamp);
            parent.BeforeLayoutChanged = () => throw new InvalidOperationException("Layout callback failed");

            Assert.Throws<InvalidOperationException>(() => child.PreferredWidth = 20);
            Assert.Null(typeof(MGElement).GetField("ForwardedLayoutMutation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(parent));

            parent.BeforeLayoutChanged = null;
            MGRenderRevisionSnapshot before = parent.GetRenderRevisionSnapshot();
            parent.PreferredHeight = 42;
            Assert.Equal(before.Visual + 1, parent.VisualRevision);
        }

        [Fact]
        public void ChangedGeometryInvalidatesWithoutSchedulingLayoutAndRepeatedGeometryDoesNot()
        {
            TestElement element = CreateElement();
            SeedMeasurement(element);
            MGRenderRevisionSnapshot initial = element.GetRenderRevisionSnapshot();
            SetProperty(element, nameof(MGElement.LayoutBounds), new Rectangle(1, 2, 30, 40));
            MGRenderRevisionSnapshot geometry = element.GetRenderRevisionSnapshot();

            Assert.Equal(initial.Layout + 1, geometry.Layout);
            Assert.True(element.IsLayoutValid);
            Assert.True(element.HasCachedMeasurement());
            SetProperty(element, nameof(MGElement.LayoutBounds), new Rectangle(1, 2, 30, 40));
            Assert.True(element.IsRenderRevisionCurrent(geometry));

            SetProperty(element, nameof(MGElement.ActualLayoutBounds), new Rectangle(1, 2, 20, 40));
            Assert.False(element.IsRenderRevisionCurrent(geometry));
            Assert.Equal(geometry.Layout, element.LayoutRevision);
            Assert.True(element.IsLayoutValid);
        }

        [Fact]
        public void LayoutCompletionChangesSnapshotButRepeatedUnchangedArrangeDoesNot()
        {
            TestElement element = CreateElement();
            MGRenderRevisionSnapshot unresolved = element.GetRenderRevisionSnapshot();

            element.UpdateLayout(Rectangle.Empty);
            MGRenderRevisionSnapshot resolved = element.GetRenderRevisionSnapshot();
            element.UpdateLayout(Rectangle.Empty);

            Assert.False(element.IsRenderRevisionCurrent(unresolved));
            Assert.True(element.IsRenderRevisionCurrent(resolved));
            Assert.True(element.IsLayoutValid);
        }

        [Fact]
        public void IdleUpdateAndGeometryFreeDrawDoNotAdvanceRevisions()
        {
            TestElement element = CreateElement();
            MGRenderRevisionSnapshot before = element.GetRenderRevisionSnapshot();
            for (int index = 0; index < 3; ++index)
            {
                element.Update(new(default, false, false, false, Point.Zero, Rectangle.Empty));
                element.Draw(new(new DrawBaseArgs(default, null!, 1), default, Point.Zero));
            }

            Assert.True(element.IsRenderRevisionCurrent(before));
            Assert.True(element.RecentDrawWasClipped);
        }

        [Theory]
        [InlineData("CurrentVisualRevision", 1)]
        [InlineData("CurrentLayoutRevision", 2)]
        [InlineData("CurrentStructureRevision", 4)]
        public void RevisionOverflowRejectsAllOldEpochSnapshots(string field, int kind)
        {
            TestElement element = CreateElement();
            TestElement other = CreateElement();
            MGRenderRevisionSnapshot otherSnapshot = other.GetRenderRevisionSnapshot();
            element.GetRenderRevisionSnapshot();
            SetField(element, field, ulong.MaxValue);
            MGRenderRevisionSnapshot old = element.GetRenderRevisionSnapshot();

            element.InvalidateRenderState((MGRenderInvalidationKind)kind);

            Assert.NotSame(old.Epoch, MGRenderRevisionClock.CurrentEpoch);
            Assert.False(element.IsRenderRevisionCurrent(old));
            Assert.False(other.IsRenderRevisionCurrent(otherSnapshot));
            Assert.Equal(2UL, element.VisualRevision);
            Assert.Equal(1UL, other.VisualRevision);
            Assert.True(element.IsRenderRevisionCurrent(element.GetRenderRevisionSnapshot()));
        }

        [Fact]
        public void MutationStampOverflowStartsANewEpochBeforeMutation()
        {
            TestElement element = CreateElement();
            MGRenderRevisionSnapshot old = element.GetRenderRevisionSnapshot();
            typeof(MGRenderRevisionClock).GetField("Sequence", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, ulong.MaxValue);

            element.InvalidateVisual();

            Assert.NotSame(old.Epoch, MGRenderRevisionClock.CurrentEpoch);
            Assert.False(element.IsRenderRevisionCurrent(old));
            Assert.Equal(2UL, element.VisualRevision);
        }

        private static TestElement CreateElement()
        {
            TestElement element = (TestElement)RuntimeHelpers.GetUninitializedObject(typeof(TestElement));
            SetField(element, "<InitializationManager>k__BackingField", new DeferEventsManager(element.SignalLayout));
            SetField(element, "<Components>k__BackingField", new List<MGComponentBase>());
            SetField(element, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
            SetField(element, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
            return element;
        }

        private static void SeedMeasurement(TestElement element)
        {
            ElementMeasurement measurement = new(new Size(100, 100), new Thickness(10), new Thickness(0), new Thickness(0));
            GetMeasurements(element, "RecentMeasurementsFull").Add(measurement);
            GetMeasurements(element, "RecentMeasurementsSelfOnly").Add(measurement);
            SetField(element, "_IsLayoutValid", true);
        }

        private static List<ElementMeasurement> GetMeasurements(TestElement element, string name)
            => (List<ElementMeasurement>)typeof(MGElement).GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(element)!;

        private static void SetField(object element, string name, object value)
            => typeof(MGElement).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(element, value);

        private static void SetProperty(object element, string name, object value)
            => typeof(MGElement).GetProperty(name)!.SetValue(element, value);

        private sealed class TestElement : MGElement
        {
            private TestElement() : base(default(MGWindow), MGElementType.Border) { }

            public Action? BeforeLayoutChanged { get; set; }

            protected override void LayoutChanged(MGElement source, bool notifyParent)
            {
                BeforeLayoutChanged?.Invoke();
                base.LayoutChanged(source, notifyParent);
            }

            public void SignalLayout() => LayoutChanged(this, false);
            public bool HasCachedMeasurement() => TryGetCachedMeasurement(new Size(100, 100), out _, out _);
            public void NotifyBookkeeping() => NPC(nameof(RecentDrawWasClipped));
        }
    }
}
