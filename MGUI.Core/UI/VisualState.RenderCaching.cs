using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    public partial class VisualStateSetting<TDataType> : IMGRenderCacheSource, IMGRenderCacheSourceContainer
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
        protected void NotifyRenderSourceChanged() => _RenderCacheSource?.Invalidate();
        void IMGRenderCacheSourceContainer.ReconcileRenderCacheSources() => _RenderCacheSource?.ReconcileDependencies();
        IEnumerable<object> IMGRenderCacheSourceContainer.RenderCacheSources
        {
            get
            {
                foreach (object value in new object[] { NormalValue, SelectedValue, DisabledValue })
                {
                    if (value is Brushes.Fill_Brushes.IFillBrush or Brushes.Border_Brushes.IBorderBrush or IMGRenderCacheSource)
                    {
                        yield return value;
                    }
                }
            }
        }
        bool IMGRenderCacheSourceContainer.LocalSupportsRenderCaching => true;
        bool IMGRenderCacheSourceContainer.LocalIsRenderVolatile => false;
    }
}
