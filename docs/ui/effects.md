# Shader Effects

MGUI can apply a caller-supplied MonoGame `Effect` while it draws sprites. This is backend-neutral plumbing: MGUI passes the runtime `Effect` object to MonoGame, but your game owns the shader asset and the shader pipeline that produced it.

Use this feature when game code already has a backend-compatible `Effect` and wants MGUI drawing to use it. Do not treat MGUI as a shader compiler, shader loader, shader asset package, shader parameter convention, or cross-backend compatibility layer.

## Public API

- `DrawSettings.Effect` is the low-level setting passed to `SpriteBatch.Begin(...)` through MGUI's draw transaction.
- `DrawTransaction.SetEffect(effect)` changes the active effect for later drawing in the current transaction.
- `DrawTransaction.SetEffectTemporary(effect)` scopes an effect change and restores the previous effect when disposed.
- `MGEffectFillBrush` is a C#-only fill brush that shades the rectangular fill region it draws.

## Backend Compatibility

MonoGame effects are backend-specific runtime objects. A DesktopGL game and a Vulkan game may need different compiled shader profiles or artifacts, depending on that game's MonoGame backend and content pipeline.

MGUI does not compile shader source, load shader source, ship shader assets, own `Effect` instances, define shader parameter names, or guarantee that one shader bytecode artifact works across DesktopGL and Vulkan. The consuming game must provide an `Effect` compatible with the active backend.

## Fill Brush Usage

`MGEffectFillBrush` fills its destination bounds by drawing MGUI's white pixel through the supplied `Effect`. The optional `ConfigureEffect` callback runs immediately before the fill is drawn, so game code can set parameters for the current element and bounds.

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;

public void ApplyPanelShader(MGElement panel, GameTime gameTime, Effect panelEffect)
{
    panel.BackgroundBrush.NormalValue = new MGEffectFillBrush(
        panelEffect,
        (effect, drawArgs, element, bounds) =>
        {
            SetIfPresent(effect, "Time", (float)gameTime.TotalGameTime.TotalSeconds);
            SetIfPresent(effect, "Opacity", drawArgs.Opacity);
            SetIfPresent(effect, "BoundsSize", new Vector2(bounds.Width, bounds.Height));
            SetIfPresent(effect, "VisualState", element.IsSelected ? 1.0f : 0.0f);
        });
}

private static void SetIfPresent(Effect effect, string parameterName, float value)
{
    EffectParameter parameter = effect.Parameters[parameterName];
    if (parameter != null)
    {
        parameter.SetValue(value);
    }
}

private static void SetIfPresent(Effect effect, string parameterName, Vector2 value)
{
    EffectParameter parameter = effect.Parameters[parameterName];
    if (parameter != null)
    {
        parameter.SetValue(value);
    }
}
```

`Time`, `Opacity`, `BoundsSize`, and `VisualState` are only example parameter names. MGUI does not define required parameter names or automatically bind shader parameters in this increment.

The `Effect` may be created or loaded however the game normally creates backend-compatible MonoGame effects, such as through its own content pipeline:

```csharp
Effect panelEffect = Content.Load<Effect>("Effects/PanelFill");
```

You can assign the brush to any C# fill-brush slot, including `BackgroundBrush.NormalValue`, other `BackgroundBrush` visual-state values, `OverlayBrush`, or control-specific fill-brush properties.

## Null Effects and Lifetime

`MGEffectFillBrush` accepts a null `Effect`. When `Effect` is null, it draws the same white rectangular fill without applying a shader effect.

`MGEffectFillBrush` does not dispose or clone the `Effect`. The consuming game owns the `Effect` lifetime and should dispose it according to the same rules it uses for other MonoGame graphics resources.

## Limitations

`MGEffectFillBrush` only shades the rectangular fill region it draws. It does not capture the element, its children, or previously drawn UI into a render target, and it is not a post-processing system.

`MGEffectFillBrush` is C#-only. There is no XAML brush syntax, XAML schema support, sample shader asset, DesktopGL shader, Vulkan shader, or content pipeline integration in this increment.

## Custom Drawing Hooks

For custom draw code, use `DrawTransaction.SetEffectTemporary` directly. This is useful in events such as `OnEndingDraw`, where you want to draw extra sprites with an effect and then restore MGUI's previous draw settings.

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;

public void AttachGlowOverlay(MGElement element, Texture2D glowTexture, Effect glowEffect)
{
    element.OnEndingDraw += (sender, eventArgs) =>
    {
        Rectangle destination = element.LayoutBounds.GetTranslated(eventArgs.DA.Offset);

        using (eventArgs.DA.DT.SetEffectTemporary(glowEffect))
        {
            eventArgs.DA.DT.DrawTextureTo(
                glowTexture,
                null,
                destination,
                Color.White * eventArgs.DA.Opacity);
        }
    };
}
```

Use `SetEffect(effect)` only when you intentionally want subsequent draw calls in the current transaction to keep using that effect. Prefer `SetEffectTemporary(effect)` for scoped custom drawing because it restores the previous effect automatically.
