using Microsoft.Xna.Framework;
using MGUI.Core.UI.Data_Binding;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    /// <summary>How a cached brush establishes deterministic, bounded source painting.</summary>
    public enum MGRenderCacheSourceMode
    {
        Tracked = 0,
        ExplicitlyStatic = 1
    }

    /// <summary>Explicitly caches a static fill at supported built-in paint sites. Call the desktop's
    /// PrepareRenderCaches before rendering the scene. First use and invalidated draws paint normally.</summary>
    /// <remarks>ExplicitlyStatic asserts bounded premultiplied local/UV-only painting without time, screen-position,
    /// interaction or untracked external dependencies, side effects or target switches. Notify opaque effect writes,
    /// texture uploads and raw list edits with InvalidateVisual before drawing. Known volatility still bypasses.</remarks>
    public sealed class MGCachedFillBrush : XAMLBindableBase, IFillBrush, IMGRenderCacheSource, IMGRenderCacheSourceContainer,
        IElementNameResolver, IResourcesResolver, IDesktopResolver
    {
        private IFillBrush _Brush;
        private bool _IsCacheEnabled = true;
        private MGRenderCacheSourceMode _SourceMode;
        private ulong Revision = 1;
        private MGRenderCacheSourceDependencies Sources;
        private readonly ConditionalWeakTable<MGElement, SiteOperations> Owners = new();
        private readonly List<WeakReference<BrushOperation>> Operations = new();

        private WeakReference<MGElement> BindingSourceElement;

        /// <summary>Only used to resolve XAML binding sources; does not retain the owning element.</summary>
        internal MGElement SourceElement
        {
            get => BindingSourceElement != null && BindingSourceElement.TryGetTarget(out var owner) ? owner : null;
            set => BindingSourceElement = value == null ? null : new(value);
        }

        public bool TryGetElementByName(string Name, out MGElement NamedElement)
        {
            MGElement owner = SourceElement;
            if (owner != null)
            {
                return owner.TryGetElementByName(Name, out NamedElement);
            }

            NamedElement = null;
            return false;
        }

        public MGResources GetResources() => SourceElement?.GetResources();
        public MGDesktop GetDesktop() => SourceElement?.GetDesktop();

        public MGCachedFillBrush(IFillBrush Brush)
        {
            _Brush = Brush ?? throw new ArgumentNullException(nameof(Brush));
        }

        public IFillBrush Brush
        {
            get => _Brush;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (!EqualityComparer<IFillBrush>.Default.Equals(_Brush, value))
                {
                    _Brush = value;
                    Sources?.ReplaceSources(new object[] { _Brush });
                    Changed(MGRenderRevisionClock.NextMutation());
                    NPC(nameof(Brush));
                }
            }
        }

        public bool IsCacheEnabled
        {
            get => _IsCacheEnabled;
            set
            {
                if (_IsCacheEnabled != value)
                {
                    _IsCacheEnabled = value;
                    Changed(MGRenderRevisionClock.NextMutation());
                    NPC(nameof(IsCacheEnabled));
                }
            }
        }

        public MGRenderCacheSourceMode SourceMode
        {
            get => _SourceMode;
            set
            {
                if (value != MGRenderCacheSourceMode.Tracked && value != MGRenderCacheSourceMode.ExplicitlyStatic)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                if (_SourceMode != value)
                {
                    _SourceMode = value;
                    Sources?.Reconcile();
                    Changed(MGRenderRevisionClock.NextMutation());
                    NPC(nameof(SourceMode));
                }
            }
        }

        public ulong VisualRevision { get { EnsureTracking(); return Revision; } }
        public bool IsRenderVolatile { get { EnsureTracking(); return Sources.IsRenderVolatile; } }
        public bool SupportsRenderCaching
        {
            get
            {
                EnsureTracking();
                return Sources.ResourcesAreAlive && !Sources.HasUnboundedPaint
                    && (SourceMode == MGRenderCacheSourceMode.ExplicitlyStatic || Sources.SupportsRenderCaching);
            }
        }
        private event EventHandler Invalidated;
        public event EventHandler VisualInvalidated
        {
            add { EnsureTracking(); Invalidated += value; }
            remove { Invalidated -= value; }
        }

        private void EnsureTracking()
        {
            if (Sources == null)
            {
                Sources = new(this, static owner => ((MGCachedFillBrush)owner).SourceChanged(), this);
                Sources.ReplaceSources(new object[] { Brush });
            }
        }
        private void SourceChanged() => Changed(Sources.LastNotificationStamp);
        private void Changed(MGRenderMutationStamp stamp)
        {
            if (Revision == ulong.MaxValue)
            {
                MGRenderRevisionClock.RestartEpoch();
                Revision = 1;
            }
            else
            {
                Revision++;
            }
            for (int index = Operations.Count - 1; index >= 0; index--)
            {
                if (Operations[index].TryGetTarget(out var operation))
                {
                    operation.Invalidate();
                }
                else
                {
                    Operations.RemoveAt(index);
                }
            }
            Invalidated?.Invoke(this, new MGRenderCacheSourceInvalidatedEventArgs(stamp));
        }

        /// <summary>Reconciles opaque source changes and invalidates every registered owner/site without changing layout.</summary>
        public void InvalidateVisual()
        {
            Sources?.Reconcile();
            Changed(MGRenderRevisionClock.NextMutation());
        }

        public IFillBrush Copy() => new MGCachedFillBrush(Brush.Copy()) { IsCacheEnabled = IsCacheEnabled, SourceMode = SourceMode };

        /// <summary>Direct callers have no stable paint-site identity and deliberately paint the original source.</summary>
        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
        {
            if (Bounds.Width > 0 && Bounds.Height > 0)
            {
                Brush.Draw(DA, Element, Bounds);
            }
        }

        internal void DrawAtSite(ElementDrawArgs args, MGElement owner, Rectangle bounds, MGFillBrushPaintSite site)
        {
            if (args.DT.IsCacheCapture || owner.SelfOrParentWindow.Desktop.IsPreparingRenderCaches)
            {
                Draw(args, owner, bounds);
                return;
            }
            BrushOperation operation = null;
            if (Owners.TryGetValue(owner, out var sites))
            {
                sites.Values.TryGetValue(site, out operation);
            }
            if (!IsCacheEnabled || bounds.Width <= 0 || bounds.Height <= 0)
            {
                operation?.Invalidate();
                Draw(args, owner, bounds);
                return;
            }
            EnsureTracking();
            if (operation == null)
            {
                sites ??= Owners.GetValue(owner, static _ => new());
                operation = new(this, owner);
                sites.Values.Add(site, operation);
                for (int index = Operations.Count - 1; index >= 0; index--)
                {
                    if (!Operations[index].TryGetTarget(out _))
                    {
                        Operations.RemoveAt(index);
                    }
                }
                Operations.Add(new(operation));
            }
            if (operation.TryDraw(args, bounds, VisualRevision))
            {
                return;
            }
            var observation = operation.BeginDirect(args, bounds, VisualRevision, IsEligible(owner));
            Brush.Draw(args, owner, bounds);
            operation.CompleteDirect(observation);
        }

        private bool IsEligible(MGElement owner) => IsCacheEnabled && !owner.IsRenderVolatile && !owner.IsBuiltInRenderVolatile
            && !Sources.IsRenderVolatile && Sources.ResourcesAreAlive && !Sources.HasUnboundedPaint
            && (SourceMode == MGRenderCacheSourceMode.ExplicitlyStatic || Sources.SupportsRenderCaching);

        private sealed class SiteOperations
        {
            internal readonly Dictionary<MGFillBrushPaintSite, BrushOperation> Values = new();
        }
        private sealed class BrushOperation : MGRenderCacheOperation
        {
            private readonly WeakReference<MGCachedFillBrush> Brush;
            internal BrushOperation(MGCachedFillBrush brush, MGElement owner) : base(owner.SelfOrParentWindow.Desktop, owner)
            {
                Brush = new(brush);
            }
            protected override bool IsSourceCurrent(MGElement owner, MGRenderCacheKey key)
                => Brush.TryGetTarget(out var brush) && brush.VisualRevision == key.SourceRevision && brush.IsEligible(owner);
            protected override void Paint(MGElement owner, ElementDrawArgs args, Rectangle bounds)
            {
                if (Brush.TryGetTarget(out var brush))
                {
                    brush.Brush.Draw(args, owner, bounds);
                }
            }
        }

        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => new object[] { Brush };
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => SupportsRenderCaching;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => IsRenderVolatile;
        bool IMGRenderCacheSourceContainer.LocalHasUnboundedPaint { get { EnsureTracking(); return Sources.HasUnboundedPaint; } }
        bool IMGRenderCacheSourceContainer.DependencySupportAffectsEligibility => SourceMode != MGRenderCacheSourceMode.ExplicitlyStatic;
        void IMGRenderCacheSourceContainer.ReconcileRenderCacheSources() { EnsureTracking(); Sources.Reconcile(); }
    }
}
