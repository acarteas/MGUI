namespace MGUI.Core.UI
{
    public abstract partial class MGElement
    {
        /// <summary>Maintained control-specific public painter count; read without inspecting delegates or descendants.</summary>
        internal virtual int ControlPublicDrawCallbackCount => 0;

        /// <summary>Opaque control state or external paint ownership that requires ordinary live rendering.</summary>
        internal virtual bool HasUnsupportedControlRenderState => false;
    }
}
