using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    public partial class MGPaddedFillBrush : IMGRenderCacheSource, IMGRenderCacheSourceContainer
    {
        private MGRenderCacheSourceState _RenderCacheSource;
        private MGRenderCacheSourceState RenderCacheSource => _RenderCacheSource ??= new(this);
        public ulong VisualRevision => RenderCacheSource.VisualRevision;
        public bool SupportsRenderCaching => RenderCacheSource.SupportsRenderCaching;
        public bool IsRenderVolatile => RenderCacheSource.IsRenderVolatile;
        public event EventHandler VisualInvalidated
        {
            add
            {
                RenderCacheSource.EnsureTracking();
                RenderCacheSource.VisualInvalidated += value;
            }
            remove
            {
                if (_RenderCacheSource != null)
                {
                    _RenderCacheSource.VisualInvalidated -= value;
                }
            }
        }
        void IMGRenderCacheSourceContainer.ReconcileRenderCacheSources() => _RenderCacheSource?.ReconcileDependencies();
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => new object[] { Brush };
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => GetType() == typeof(MGPaddedFillBrush) && MGRenderCacheSourceDependencies.Nonnegative(Padding) && (!Scale.HasValue || (float.IsFinite(Scale.Value) && Scale.Value >= 0 && Scale.Value <= 1)) && (!MinWidth.HasValue || MinWidth <= 0) && (!MinHeight.HasValue || MinHeight <= 0);
        bool IMGRenderCacheSourceContainer.LocalHasUnboundedPaint => !(MGRenderCacheSourceDependencies.Nonnegative(Padding) && (!Scale.HasValue || (float.IsFinite(Scale.Value) && Scale.Value >= 0 && Scale.Value <= 1)) && (!MinWidth.HasValue || MinWidth <= 0) && (!MinHeight.HasValue || MinHeight <= 0));
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
    }
}

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    public partial class MGBorderedFillBrush : IMGRenderCacheSource, IMGRenderCacheSourceContainer
    {
        private MGRenderCacheSourceState _RenderCacheSource;
        private MGRenderCacheSourceState RenderCacheSource => _RenderCacheSource ??= new(this);
        public ulong VisualRevision => RenderCacheSource.VisualRevision;
        public bool SupportsRenderCaching => RenderCacheSource.SupportsRenderCaching;
        public bool IsRenderVolatile => RenderCacheSource.IsRenderVolatile;
        public event EventHandler VisualInvalidated
        {
            add
            {
                RenderCacheSource.EnsureTracking();
                RenderCacheSource.VisualInvalidated += value;
            }
            remove
            {
                if (_RenderCacheSource != null)
                {
                    _RenderCacheSource.VisualInvalidated -= value;
                }
            }
        }
        void IMGRenderCacheSourceContainer.ReconcileRenderCacheSources() => _RenderCacheSource?.ReconcileDependencies();
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => new object[] { BorderBrush, FillBrush };
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => GetType() == typeof(MGBorderedFillBrush) && MGRenderCacheSourceDependencies.Nonnegative(BorderThickness);
        bool IMGRenderCacheSourceContainer.LocalHasUnboundedPaint => !(MGRenderCacheSourceDependencies.Nonnegative(BorderThickness));
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
    }
}

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    public partial class MGCompositedFillBrush : IMGRenderCacheSource, IMGRenderCacheSourceContainer
    {
        private MGRenderCacheSourceState _RenderCacheSource;
        private MGRenderCacheSourceState RenderCacheSource => _RenderCacheSource ??= new(this);
        public ulong VisualRevision => RenderCacheSource.VisualRevision;
        public bool SupportsRenderCaching => RenderCacheSource.SupportsRenderCaching;
        public bool IsRenderVolatile => RenderCacheSource.IsRenderVolatile;
        public event EventHandler VisualInvalidated
        {
            add
            {
                RenderCacheSource.EnsureTracking();
                RenderCacheSource.VisualInvalidated += value;
            }
            remove
            {
                if (_RenderCacheSource != null)
                {
                    _RenderCacheSource.VisualInvalidated -= value;
                }
            }
        }
        void IMGRenderCacheSourceContainer.ReconcileRenderCacheSources() => _RenderCacheSource?.ReconcileDependencies();
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => Brushes;
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => GetType() == typeof(MGCompositedFillBrush) && true;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
    }
}

namespace MGUI.Core.UI.Brushes.Border_Brushes
{
    public partial class MGCompositedBorderBrush : IMGRenderCacheSource, IMGRenderCacheSourceContainer
    {
        private MGRenderCacheSourceState _RenderCacheSource;
        private MGRenderCacheSourceState RenderCacheSource => _RenderCacheSource ??= new(this);
        public ulong VisualRevision => RenderCacheSource.VisualRevision;
        public bool SupportsRenderCaching => RenderCacheSource.SupportsRenderCaching;
        public bool IsRenderVolatile => RenderCacheSource.IsRenderVolatile;
        public event EventHandler VisualInvalidated
        {
            add
            {
                RenderCacheSource.EnsureTracking();
                RenderCacheSource.VisualInvalidated += value;
            }
            remove
            {
                if (_RenderCacheSource != null)
                {
                    _RenderCacheSource.VisualInvalidated -= value;
                }
            }
        }
        void IMGRenderCacheSourceContainer.ReconcileRenderCacheSources() => _RenderCacheSource?.ReconcileDependencies();
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => Brushes;
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => GetType() == typeof(MGCompositedBorderBrush) && true;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
    }
}

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    public partial class MGHighlightFillBrush : IMGRenderCacheSource, IMGRenderCacheSourceContainer
    {
        private MGRenderCacheSourceState _RenderCacheSource;
        private MGRenderCacheSourceState RenderCacheSource => _RenderCacheSource ??= new(this);
        public ulong VisualRevision => RenderCacheSource.VisualRevision;
        public bool SupportsRenderCaching => RenderCacheSource.SupportsRenderCaching;
        public bool IsRenderVolatile => RenderCacheSource.IsRenderVolatile;
        public event EventHandler VisualInvalidated
        {
            add
            {
                RenderCacheSource.EnsureTracking();
                RenderCacheSource.VisualInvalidated += value;
            }
            remove
            {
                if (_RenderCacheSource != null)
                {
                    _RenderCacheSource.VisualInvalidated -= value;
                }
            }
        }
        void IMGRenderCacheSourceContainer.ReconcileRenderCacheSources()
        {
            DetachFocusedElementNotifications();
            AttachFocusedElementNotifications();
            CachedUnfocusedRegions.Clear();
            _RenderCacheSource?.ReconcileDependencies();
        }
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => Array.Empty<object>();
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => GetType() == typeof(MGHighlightFillBrush) && !IsEnabled;
        bool IMGRenderCacheSourceContainer.LocalHasUnboundedPaint => !(!IsEnabled);
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
    }
}

namespace MGUI.Core.UI.Brushes.Border_Brushes
{
    public partial class MGHighlightBorderBrush : IMGRenderCacheSource, IMGRenderCacheSourceContainer
    {
        private MGRenderCacheSourceState _RenderCacheSource;
        private MGRenderCacheSourceState RenderCacheSource => _RenderCacheSource ??= new(this);
        public ulong VisualRevision => RenderCacheSource.VisualRevision;
        public bool SupportsRenderCaching => RenderCacheSource.SupportsRenderCaching;
        public bool IsRenderVolatile => RenderCacheSource.IsRenderVolatile;
        public event EventHandler VisualInvalidated
        {
            add
            {
                RenderCacheSource.EnsureTracking();
                RenderCacheSource.VisualInvalidated += value;
            }
            remove
            {
                if (_RenderCacheSource != null)
                {
                    _RenderCacheSource.VisualInvalidated -= value;
                }
            }
        }
        void IMGRenderCacheSourceContainer.ReconcileRenderCacheSources() => _RenderCacheSource?.ReconcileDependencies();
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => new object[] { Underlay };
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => GetType() == typeof(MGHighlightBorderBrush) && true;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => IsEnabled;
    }
}

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    public partial class MGEffectFillBrush : IMGRenderCacheSource, IMGRenderCacheSourceContainer
    {
        private MGRenderCacheSourceState _RenderCacheSource;
        private MGRenderCacheSourceState RenderCacheSource => _RenderCacheSource ??= new(this);
        public ulong VisualRevision => RenderCacheSource.VisualRevision;
        public bool SupportsRenderCaching => RenderCacheSource.SupportsRenderCaching;
        public bool IsRenderVolatile => RenderCacheSource.IsRenderVolatile;
        public event EventHandler VisualInvalidated
        {
            add
            {
                RenderCacheSource.EnsureTracking();
                RenderCacheSource.VisualInvalidated += value;
            }
            remove
            {
                if (_RenderCacheSource != null)
                {
                    _RenderCacheSource.VisualInvalidated -= value;
                }
            }
        }
        void IMGRenderCacheSourceContainer.ReconcileRenderCacheSources() => _RenderCacheSource?.ReconcileDependencies();
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => new object[] { Binding };
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => false;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
    }
}
