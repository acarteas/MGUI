using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    public partial class MGResources : IMGRenderCacheSource, IMGRenderCacheSourceContainer
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
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources
        {
            get
            {
                yield return DefaultTheme;
                foreach (MGTheme theme in Themes.Values)
                {
                    yield return theme;
                }
            }
        }
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => true;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
        bool IMGRenderCacheSourceContainer.DependenciesAffectEligibility => false;
    }
}
