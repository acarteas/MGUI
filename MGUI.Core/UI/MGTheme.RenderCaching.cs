using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI
{
    public partial class ThemeManagedGetter<TDataType> : IMGRenderCacheSource, IMGRenderCacheSourceContainer
        where TDataType : class, ICloneable
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
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => new object[] { _Value };
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => true;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
        bool IMGRenderCacheSourceContainer.DependenciesAffectEligibility => false;
    }
    public partial class ThemeFontSettings : IMGRenderCacheSource, IMGRenderCacheSourceContainer
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
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => Array.Empty<object>();
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => true;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
        bool IMGRenderCacheSourceContainer.DependenciesAffectEligibility => false;
    }
    public partial class MGTheme : IMGRenderCacheSource, IMGRenderCacheSourceContainer
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
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => EnumerateThemeSources();
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => true;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
        bool IMGRenderCacheSourceContainer.DependenciesAffectEligibility => false;
        private IEnumerable<object> EnumerateThemeSources()
        {
            foreach (object value in _Backgrounds.Values)
            {
                yield return value;
            }
            foreach (object value in ListBoxItemAlternatingRowBackgrounds)
            {
                yield return value;
            }
            yield return ComboBoxDropdownBackground;
            yield return ComboBoxDropdownItemBackground;
            yield return GridSplitterForeground;
            yield return ListBoxItemBackground;
            yield return TreeViewSelectionBackground;
            yield return ProgressButtonForeground;
            yield return ProgressBarCompletedBrush;
            yield return ProgressBarIncompleteBrush;
            yield return RadioButtonBubbleBackground;
            yield return ResizeGripForeground;
            yield return ScrollBarOuterBrush;
            yield return ScrollBarInnerBrush;
            yield return SliderForeground;
            yield return SliderThumbFillBrush;
            yield return SliderOverlay;
            yield return SpoilerUnspoiledBackground;
            yield return SelectedTabHeaderBackground;
            yield return UnselectedTabHeaderBackground;
            yield return TitleBackground;
            yield return TextBlockFallbackForeground;
            yield return ToolTipTextForeground;
            yield return FontSettings;
            yield return TreeViewBorderBrush;
        }
    }
}
