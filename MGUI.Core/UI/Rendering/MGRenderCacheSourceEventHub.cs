using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MGUI.Core.UI
{
    internal sealed class MGRenderCacheSourceInvalidatedEventArgs : EventArgs
    {
        internal MGRenderMutationStamp Stamp { get; }
        internal MGRenderCacheSourceInvalidatedEventArgs(MGRenderMutationStamp stamp) { Stamp = stamp; }
    }

    // One forwarding subscription per publisher gives a custom event (even without a revision bump)
    // one identity across nested/diamond dependency paths. Listeners are weak, never owner closures.
    internal sealed class MGRenderCacheSourceEventHub
    {
        private static readonly ConditionalWeakTable<IMGRenderCacheSource, MGRenderCacheSourceEventHub> Hubs = new();
        private readonly IMGRenderCacheSource Source;
        private readonly List<WeakReference<MGRenderCacheSourceDependencies>> Listeners = new();
        private bool Attached;
        private MGRenderCacheSourceEventHub(IMGRenderCacheSource source) { Source = source; }
        internal static Action Subscribe(IMGRenderCacheSource source, MGRenderCacheSourceDependencies listener)
        {
            var hub = Hubs.GetValue(source, static publisher => new(publisher));
            var weak = new WeakReference<MGRenderCacheSourceDependencies>(listener);
            hub.Listeners.Add(weak);
            if (!hub.Attached)
            {
                hub.Attached = true;
                source.VisualInvalidated += hub.OnInvalidated;
            }
            return () => hub.Remove(weak);
        }
        private void Remove(WeakReference<MGRenderCacheSourceDependencies> listener)
        {
            Listeners.Remove(listener);
            if (Listeners.Count == 0 && Attached)
            {
                Attached = false;
                Source.VisualInvalidated -= OnInvalidated;
            }
        }
        private void OnInvalidated(object sender, EventArgs args)
        {
            MGRenderMutationStamp stamp = args is MGRenderCacheSourceInvalidatedEventArgs invalidated
                ? invalidated.Stamp : MGRenderRevisionClock.NextMutation();
            foreach (var listener in Listeners.ToArray())
            {
                if (listener.TryGetTarget(out var target))
                {
                    target.SourceChanged(stamp);
                }
                else
                {
                    Remove(listener);
                }
            }
        }
    }
}
