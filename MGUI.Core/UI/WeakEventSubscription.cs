using System;

namespace MGUI.Core.UI
{

    /// <summary>Forwards an event without extending the observer's lifetime. Callbacks must not capture the observer.</summary>
    internal sealed class WeakEventSubscription<TObserver, TPublisher, TArgs> where TObserver : class
    {
        private readonly WeakReference<TObserver> Observer;
        private readonly Action<TObserver, object, TArgs> Callback;
        private readonly TPublisher Publisher;
        private readonly Action<TPublisher, EventHandler<TArgs>> Unsubscribe;

        internal WeakEventSubscription(TObserver Observer, TPublisher Publisher, Action<TObserver, object, TArgs> Callback,
            Action<TPublisher, EventHandler<TArgs>> Subscribe, Action<TPublisher, EventHandler<TArgs>> Unsubscribe)
        {
            this.Observer = new(Observer);
            this.Publisher = Publisher;
            this.Callback = Callback;
            this.Unsubscribe = Unsubscribe;
            Subscribe(Publisher, OnEvent);
        }

        private void OnEvent(object Sender, TArgs Args)
        {
            if (Observer.TryGetTarget(out TObserver Target))
            {
                Callback(Target, Sender, Args);
            }
            else
            {
                Unsubscribe(Publisher, OnEvent);
            }
        }
    }
}
