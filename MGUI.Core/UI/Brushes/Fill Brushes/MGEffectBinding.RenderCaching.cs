using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    internal sealed partial class MGEffectBinding : IMGRenderCacheSource, IMGRenderCacheSourceContainer
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
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources => new object[] { Effect };
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => Effect == null && ConfigureEffect == null;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
    }
}
