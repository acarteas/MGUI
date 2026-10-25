using System;

namespace MGUI.Core.UI
{
    public abstract partial class MGElement
    {
        private EventHandler<MGElementDrawEventArgs> BeginDrawHandlers;
        private EventHandler<MGElementDrawEventArgs> EndingDrawHandlers;
        private EventHandler<MGElementDrawEventArgs> EndDrawHandlers;
        internal int PublicDrawCallbackCount { get; private set; }

        private void ChangePublicDrawHandlers(ref EventHandler<MGElementDrawEventArgs> handlers,
            EventHandler<MGElementDrawEventArgs> value, bool add)
        {
            EventHandler<MGElementDrawEventArgs> Previous = handlers;
            EventHandler<MGElementDrawEventArgs> Current = add ? Previous + value : Previous - value;
            if (Equals(Previous, Current))
            {
                return;
            }

            // Inspect delegates only when a subscription actually changes, never on a cache read or draw.
            PublicDrawCallbackCount += (Current?.GetInvocationList().Length ?? 0) - (Previous?.GetInvocationList().Length ?? 0);
            handlers = Current;
            InvalidateRenderState(MGRenderInvalidationKind.Visual);
        }
    }
}
