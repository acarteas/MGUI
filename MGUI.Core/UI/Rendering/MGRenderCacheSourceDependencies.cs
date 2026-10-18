using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MGUI.Core.UI
{
    /// <summary>Optional contract for deterministic, bounded, translation-safe render sources.
    /// Implementations notify after any output, support or volatility change. Opaque texture uploads,
    /// effect writes and raw collection edits require explicit owner/wrapper invalidation before drawing.</summary>
    public interface IMGRenderCacheSource
    {
        ulong VisualRevision { get; }
        bool IsRenderVolatile { get; }
        bool SupportsRenderCaching { get; }
        event EventHandler VisualInvalidated;
    }

    // Built-ins expose local facts separately so discovery can reject cycles without recursive getters.
    internal interface IMGRenderCacheSourceContainer
    {
        IEnumerable<object> RenderCacheSources { get; }
        bool LocalSupportsRenderCaching { get; }
        bool LocalIsRenderVolatile { get; }
        bool DependenciesAffectEligibility => true;
        bool DependencySupportAffectsEligibility => true;
        bool LocalHasUnboundedPaint => false;
        void ReconcileRenderCacheSources();
    }

    /// <summary>Lazy per-resource revision state. No state/subscription is allocated until caching observes it.</summary>
    internal sealed class MGRenderCacheSourceState
    {
        private readonly MGRenderCacheSourceDependencies Dependencies;
        private MGRenderMutationStamp LastEmittedStamp;
        private bool Initialized;
        private ulong Revision = 1;
        internal ulong VisualRevision { get { EnsureTracking(); return Revision; } }
        internal bool SupportsRenderCaching { get { EnsureTracking(); return Dependencies.SupportsRenderCaching && Dependencies.ResourcesAreAlive; } }
        internal bool IsRenderVolatile { get { EnsureTracking(); return Dependencies.IsRenderVolatile; } }
        internal event EventHandler VisualInvalidated;
        private readonly WeakReference<object> Owner;
        internal MGRenderCacheSourceState(object owner)
        {
            Owner = new(owner);
            Dependencies = new(this, static state => ((MGRenderCacheSourceState)state).Notify(), owner);
        }
        internal void EnsureTracking()
        {
            if (!Initialized && Owner.TryGetTarget(out object owner))
            {
                Initialized = true;
                Dependencies.ReplaceSources(new[] { owner });
            }
        }
        internal void ReconcileDependencies() { EnsureTracking(); Dependencies.Reconcile(); }
        internal void Invalidate()
        {
            EnsureTracking();
            Dependencies.Reconcile();
            Notify(MGRenderRevisionClock.NextMutation());
        }
        private void Notify() => Notify(Dependencies.LastNotificationStamp);
        private void Notify(MGRenderMutationStamp stamp)
        {
            if (ReferenceEquals(LastEmittedStamp.Epoch, stamp.Epoch) && stamp.Sequence <= LastEmittedStamp.Sequence)
            {
                return;
            }
            LastEmittedStamp = stamp;
            if (Revision == ulong.MaxValue)
            {
                MGRenderRevisionClock.RestartEpoch();
                Revision = 1;
            }
            else
            {
                Revision++;
            }
            if (Owner.TryGetTarget(out object owner))
            {
                VisualInvalidated?.Invoke(owner, new MGRenderCacheSourceInvalidatedEventArgs(stamp));
            }
        }
    }

    /// <summary>Change-time graph discovery; idle consumers read maintained facts. Publishers retain
    /// only weak forwarders, so a shared brush/resource never roots its consuming owner.</summary>
    internal sealed class MGRenderCacheSourceDependencies : IDisposable
    {
        private readonly WeakReference<object> Owner;
        private readonly Action<object> Changed;
        private readonly object IgnoredPublisher;
        private object[] Sources = Array.Empty<object>();
        private readonly List<Action> Unsubscribe = new();
        private sealed class ResourceLifetime
        {
            internal bool IsDisposing;
            internal MGRenderMutationStamp DisposingStamp;
        }
        private static readonly ConditionalWeakTable<GraphicsResource, ResourceLifetime> ResourceLifetimes = new();
        private bool Alive = true;
        internal MGRenderMutationStamp LastNotificationStamp { get; private set; }
        private bool Reconciling;
        private bool HasDiscovered;
        private MGRenderMutationStamp? DeferredChange;
        private bool Disposed;
        internal ulong VisualRevision { get; private set; } = 1;
        internal bool SupportsRenderCaching { get; private set; } = true;
        internal bool IsRenderVolatile { get; private set; }
        internal bool HasUnboundedPaint { get; private set; }
        internal bool ResourcesAreAlive => Alive;
        internal MGRenderCacheSourceDependencies(object owner, Action<object> changed, object ignoredPublisher = null)
        {
            Owner = new(owner ?? throw new ArgumentNullException(nameof(owner)));
            Changed = changed ?? throw new ArgumentNullException(nameof(changed));
            IgnoredPublisher = ignoredPublisher;
        }
        internal void ReplaceSources(IEnumerable<object> sources)
        {
            Sources = sources?.Where(source => source != null).ToArray() ?? Array.Empty<object>();
            Reconcile();
        }
        internal void Reconcile()
        {
            if (Disposed || Reconciling)
            {
                return;
            }
            Reconciling = true;
            try
            {
                Detach();
                SupportsRenderCaching = true;
                IsRenderVolatile = false;
                Alive = true;
                HasUnboundedPaint = false;
                var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                var path = new HashSet<object>(ReferenceEqualityComparer.Instance);
                foreach (object source in Sources)
                {
                    Visit(source, visited, path, true);
                }
                if (HasDiscovered)
                {
                    AdvanceRevision();
                }
                HasDiscovered = true;
            }
            finally
            {
                Reconciling = false;
                if (DeferredChange is { } deferred)
                {
                    DeferredChange = null;
                    OnChanged(deferred);
                }
            }
        }
        private void AdvanceRevision()
        {
            if (VisualRevision == ulong.MaxValue)
            {
                MGRenderRevisionClock.RestartEpoch();
                VisualRevision = 1;
            }
            else
            {
                VisualRevision++;
            }
        }
        private void OnChanged(MGRenderMutationStamp stamp)
        {
            if (Disposed || (ReferenceEquals(LastNotificationStamp.Epoch, stamp.Epoch) && stamp.Sequence <= LastNotificationStamp.Sequence))
            {
                return;
            }
            if (Reconciling)
            {
                DeferredChange = stamp;
                return;
            }
            LastNotificationStamp = stamp;
            if (!Owner.TryGetTarget(out object owner))
            {
                Dispose();
                return;
            }
            Reconcile();
            Changed(owner);
        }
        private void Subscribe(IMGRenderCacheSource source)
        {
            Unsubscribe.Add(MGRenderCacheSourceEventHub.Subscribe(source, this));
        }
        internal void SourceChanged(MGRenderMutationStamp stamp) => OnChanged(stamp);
        private void SubscribeProperty(INotifyPropertyChanged source, params string[] properties)
        {
            var weak = new WeakReference<MGRenderCacheSourceDependencies>(this);
            PropertyChangedEventHandler forward = null;
            forward = (sender, args) =>
            {
                if (!weak.TryGetTarget(out var target))
                {
                    source.PropertyChanged -= forward;
                }
                else if (properties.Contains(args.PropertyName))
                {
                    target.OnChanged(MGRenderRevisionClock.NextMutation());
                }
            };
            source.PropertyChanged += forward;
            Unsubscribe.Add(() => source.PropertyChanged -= forward);
        }
        private void Visit(object source, HashSet<object> visited, HashSet<object> path, bool root = false)
        {
            if (source == null)
            {
                return;
            }
            if (path.Contains(source))
            {
                HasUnboundedPaint = true;
                SupportsRenderCaching = false;
                return;
            }
            if (!visited.Add(source))
            {
                return;
            }
            path.Add(source);
            if (source is IMGRenderCacheSourceContainer container)
            {
                if (!ReferenceEquals(source, IgnoredPublisher))
                {
                    container.ReconcileRenderCacheSources();
                }
                SupportsRenderCaching &= container.LocalSupportsRenderCaching;
                IsRenderVolatile |= container.LocalIsRenderVolatile;
                HasUnboundedPaint |= container.LocalHasUnboundedPaint;
                if (!ReferenceEquals(source, IgnoredPublisher) && source is IMGRenderCacheSource observable)
                {
                    Subscribe(observable);
                }
                bool supported = SupportsRenderCaching;
                bool volatileSource = IsRenderVolatile;
                bool alive = Alive;
                bool unbounded = HasUnboundedPaint;
                HashSet<object> childVisited = container.DependenciesAffectEligibility && container.DependencySupportAffectsEligibility ? visited
                    : new HashSet<object>(path, ReferenceEqualityComparer.Instance);
                foreach (object child in container.RenderCacheSources)
                {
                    Visit(child, childVisited, path);
                }
                if (!container.DependencySupportAffectsEligibility)
                {
                    SupportsRenderCaching = supported;
                }
                if (!container.DependenciesAffectEligibility)
                {
                    SupportsRenderCaching = supported;
                    IsRenderVolatile = volatileSource;
                    Alive = alive;
                    HasUnboundedPaint = unbounded;
                }
            }
            else
            {
                switch (source)
                {
                    case IMGRenderCacheSource observable:
                        SupportsRenderCaching &= observable.SupportsRenderCaching;
                        IsRenderVolatile |= observable.IsRenderVolatile;
                        Subscribe(observable);
                        break;
                    case MGSolidFillBrush or MGGradientFillBrush or MGDiagonalGradientFillBrush or Color:
                        break;
                    case MGTextureFillBrush texture:
                        bool boundedTexture = texture.Stretch == Stretch.Fill || texture.Stretch == Stretch.Uniform;
                        SupportsRenderCaching &= boundedTexture;
                        HasUnboundedPaint |= !boundedTexture;
                        Visit(texture.Source, visited, path);
                        break;
                    case MGTextureData texture:
                        SupportsRenderCaching &= texture.Texture != null;
                        Visit(texture.Texture, visited, path);
                        break;
                    case Texture2D texture:
                        TrackLifetime(texture);
                        break;
                    case Effect effect:
                        TrackLifetime(effect);
                        SupportsRenderCaching = false;
                        break;
                    case MGUniformBorderBrush uniform:
                        Visit(uniform.Brush, visited, path);
                        break;
                    case MGDockedBorderBrush docked:
                        foreach (object child in new object[] { docked.Left, docked.Top, docked.Right, docked.Bottom })
                        {
                            Visit(child, visited, path);
                        }
                        break;
                    case MGBandedBorderBrush banded:
                        bool boundedBands = banded.Bands != null && banded.Bands.All(band => band.ThicknessWeight >= 0 && double.IsFinite(band.ThicknessWeight))
                            && (banded.Bands.Count == 0 || (banded.Bands.Sum(band => band.ThicknessWeight) > 0 && double.IsFinite(banded.Bands.Sum(band => band.ThicknessWeight))));
                        SupportsRenderCaching &= boundedBands;
                        HasUnboundedPaint |= !boundedBands;
                        if (banded.Bands != null)
                        {
                            foreach (var band in banded.Bands)
                            {
                                Visit(band.Brush, visited, path);
                            }
                        }
                        break;
                    case MGTexturedBorderBrush textured:
                        bool boundedBorder = textured.Transforms == new TextureTransforms();
                        SupportsRenderCaching &= boundedBorder;
                        HasUnboundedPaint |= !boundedBorder;
                        Visit(textured.EdgeTexture, visited, path);
                        Visit(textured.CornerTexture, visited, path);
                        break;
                    case MGNineSliceFillBrush nine:
                        HasUnboundedPaint |= !Nonnegative(nine.TargetMargin);
                        SupportsRenderCaching &= nine.Effect == null && nine.ConfigureEffect == null && Nonnegative(nine.TargetMargin);
                        Visit(nine.RenderCacheEffectBinding, visited, path);
                        Visit(nine.InteriorBrush, visited, path);
                        foreach (var texture in new[] { nine.TopLeft, nine.TopCenter, nine.TopRight, nine.MiddleLeft, nine.MiddleCenter, nine.MiddleRight, nine.BottomLeft, nine.BottomCenter, nine.BottomRight })
                        {
                            Visit(texture, visited, path);
                        }
                        break;
                    case MGProgressBarGradientBrush progress:
                        if (progress.ProgressBar != null)
                        {
                            SubscribeProperty(progress.ProgressBar, nameof(MGProgressBar.Value), nameof(MGProgressBar.Minimum), nameof(MGProgressBar.Maximum));
                        }
                        break;
                    default:
                        SupportsRenderCaching = false;
                        break;
                }
            }
            path.Remove(source);
        }
        internal static bool Nonnegative(Thickness value) => value.Left >= 0 && value.Top >= 0 && value.Right >= 0 && value.Bottom >= 0;
        private void TrackLifetime(GraphicsResource resource)
        {
            ResourceLifetime lifetime = ResourceLifetimes.GetValue(resource, static _ => new());
            Alive &= !resource.IsDisposed && !lifetime.IsDisposing;
            var weak = new WeakReference<MGRenderCacheSourceDependencies>(this);
            EventHandler<EventArgs> forward = null;
            forward = (sender, args) =>
            {
                if (!lifetime.IsDisposing)
                {
                    lifetime.IsDisposing = true;
                    lifetime.DisposingStamp = MGRenderRevisionClock.NextMutation();
                }
                if (weak.TryGetTarget(out var target))
                {
                    target.Alive = false;
                    target.OnChanged(lifetime.DisposingStamp);
                }
                else
                {
                    resource.Disposing -= forward;
                }
            };
            resource.Disposing += forward;
            Unsubscribe.Add(() => resource.Disposing -= forward);
        }
        private void Detach()
        {
            foreach (Action unsubscribe in Unsubscribe)
            {
                unsubscribe();
            }
            Unsubscribe.Clear();

        }
        public void Dispose()
        {
            if (!Disposed)
            {
                Disposed = true;
                Detach();
                Sources = Array.Empty<object>();
            }
        }
    }
}
