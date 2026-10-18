using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MGUI.Shared.Rendering
{
    /// <summary>Opt-in diagnostic build trace. Calls are erased unless MGUI_RENDER_TRACE is defined.
    /// Never use trace-build timings for performance conclusions.</summary>
    public static class DrawTransactionTrace
    {
#if MGUI_RENDER_TRACE
        private static List<object> Events;
        private static readonly Dictionary<object, int> Ids = new(ReferenceEqualityComparer.Instance);
        private static int Id(object value)
        {
            if (value == null) { return 0; }
            if (!Ids.TryGetValue(value, out int id)) { id = Ids.Count + 1; Ids.Add(value, id); }
            return id;
        }
#endif
        public static void Start(bool enabled)
        {
#if MGUI_RENDER_TRACE
            Ids.Clear();
            Events = enabled ? new() : null;
#endif
        }
        public static object Finish()
        {
#if MGUI_RENDER_TRACE
            object result = Events;
            Events = null;
            Ids.Clear();
            return result;
#else
            return null;
#endif
        }
        [Conditional("MGUI_RENDER_TRACE")]
        public static void State(string operation, DrawContext context, DrawSettings settings, Rectangle clip)
        {
#if MGUI_RENDER_TRACE
            Events?.Add(new { operation, context = context.ToString(), effect = Id(settings.Effect),
                sort = settings.Sort.ToString(), blend = settings.BlendType.ToString(),
                sampler = settings.SamplerType.ToString(), clip = new[] { clip.X, clip.Y, clip.Width, clip.Height } });
#endif
        }
        [Conditional("MGUI_RENDER_TRACE")]
        public static void Texture(Texture2D texture)
        {
#if MGUI_RENDER_TRACE
            Events?.Add(new { operation = "texture-submission", texture = Id(texture), texture.Width, texture.Height });
#endif
        }
        [Conditional("MGUI_RENDER_TRACE")]
        public static void Marker(string operation)
        {
#if MGUI_RENDER_TRACE
            Events?.Add(new { operation });
#endif
        }
        [Conditional("MGUI_RENDER_TRACE")]
        public static void Configuration(Effect effect, bool callback)
        {
#if MGUI_RENDER_TRACE
            Events?.Add(new { operation = "effect-configuration-before-callback", effect = Id(effect), callback });
#endif
        }
        [Conditional("MGUI_RENDER_TRACE")]
        public static void Text(string text)
        {
#if MGUI_RENDER_TRACE
            Events?.Add(new { operation = "text-submission", text });
#endif
        }
    }
}
