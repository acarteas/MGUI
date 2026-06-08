# Shader Effects

MGUI can use a caller-owned MonoGame `Effect` to draw a rectangular fill. The game is responsible for compiling an artifact for each graphics backend, loading it, registering it, and disposing it. MGUI does not compile, clone, hot-reload, or dispose effects.

`MGEffectFillBrush` shades only its fill rectangle. It does not capture or post-process an element subtree.

## Registration And Lifetime

Register effects before materializing XAML that references them:

```csharp
Effect effect = Content.Load<Effect>("Effects/HudButton");
Desktop.Resources.AddEffect("HudButtonEffect", effect);
```

Names are application-defined and case-sensitive. `AddEffect` rejects duplicates. `AddOrReplaceEffect`, `RemoveEffect`, and `TryGetEffect` support explicit resource changes. Replacing or removing an effect does not dispose either object.

XAML resolution is a materialization-time snapshot. Existing brushes keep their current effect after a resource is replaced or removed; newly materialized XAML observes the new registration.

## XAML Effect Fills

```xml
<Button ToolTip="Shader-backed action">
    <Button.Background>
        <EffectFillBrush EffectName="HudButtonEffect"
                         UseStandardParameters="True">
            <EffectFillBrush.FallbackBrush>
                <SolidFillBrush Color="rgb(46,41,40)" />
            </EffectFillBrush.FallbackBrush>
            <EffectParameter Name="AccentColor" Type="Color" Value="rgb(210,165,72)" />
            <EffectParameter Name="ButtonRole" Type="Float" Value="1" />
        </EffectFillBrush>
    </Button.Background>
</Button>
```

`EffectName` must be nonblank. If it is absent from `Desktop.Resources.Effects`, a configured `FallbackBrush` is materialized without failing. Without a fallback, materialization throws an actionable error. Nested fallback brushes and bindable fallback values are supported.

`EffectFillBrush` works in every `IFillBrush` slot, including normal, hovered, pressed, selected, and disabled backgrounds, borders, overlays, and nested border fills.

## Standard Parameters

Standard binding is opt-in for both XAML and programmatic brushes with `UseStandardParameters="True"`. Missing shader parameters are skipped.

| Name | Shader type | Value |
| --- | --- | --- |
| `MatrixTransform` | `float4x4` | Current transform times a SpriteBatch-compatible orthographic projection, including the configured half-pixel convention |
| `ElementPosition` | `float2` | Fill top-left after the current draw offset |
| `ElementSize` | `float2` | Fill width and height, each clamped to at least one |
| `Opacity` | `float` | Effective draw opacity |
| `TimeSeconds` | `float` | Total renderer time in seconds |
| `HoverAmount` | `float` | `1` when hovered, otherwise `0` |
| `PressAmount` | `float` | `1` when pressed, otherwise `0` |
| `SelectedAmount` | `float` | `1` when selected, otherwise `0` |
| `DisabledAmount` | `float` | `1` when disabled, otherwise `0` |

The draw color already carries effective opacity. A shader should not multiply alpha by both `input.Color.a` and `Opacity`; use one alpha application. `Opacity` remains available for non-alpha decisions.

## Custom Parameters

Supported types are `Float`, `Int`, `Bool`, `Color`, `Vector2`, `Vector3`, and `Vector4`. Parsing is culture-invariant. Boolean, integer, floating-point, and color values can be inferred; vectors require an explicit `Type` because comma-separated text is ambiguous. Vector values use comma-separated components. `Color` is bound as a normalized `float4`.

Parameter names must be nonblank and unique using ordinal, case-sensitive comparison. Scalars and vector components must be finite. Diagnostics identify the name, requested runtime type, supplied value, and expected format. Missing shader parameters are skipped. Incompatible present parameters throw with both the requested type and the shader parameter class/type.

Custom parameters are constants applied before every draw. Per-frame application prevents state leakage when one mutable effect is shared by multiple brushes. Declare every value that varies between those brushes; an A/B/A draw sequence then restores A's complete state.

## Application Order And Caching

Each draw applies values in this order:

1. Standard parameters.
2. Custom parameters, which may deliberately override a standard name.
3. `ConfigureEffect`, which may override either set.

Parameter lookups, including misses, are cached per brush/effect pair. Assigning a different `Effect` invalidates the cache. `Copy()` creates independent parameter and cache state while retaining the same caller-owned effect reference until either brush is reassigned.

## Conventional Button Features

Shader-backed controls can be compared with conventional image/button states:

- `MGImage` state tint precedence is disabled, pressed, hovered, selected, then normal `TextureColor`; each missing state tint falls directly back to normal or white.
- `HoveredRenderScale` and `PressedRenderScale` are independent optional overrides. Pressed wins while both states are active. Existing `RenderScale` remains the fallback.
- `PressedContentOffset` translates button content only while pressed and enabled. It uses spacing scale, does not affect background/border drawing, and does not change measurement or layout.
- A tooltip is sufficient when it names or explains the action. Icon-only actions need one; visible labels generally do not unless extra context is useful.

The sample at `MGUI.Samples/Features/ShaderEffects.xaml` demonstrates shader normal, primary, selected, and disabled actions plus a nonshader comparison, predominantly in XAML.

## Backend Compatibility

MonoGame effects are backend-specific runtime objects. DesktopGL, Vulkan, DirectX, mobile, and other targets may need different profiles or artifacts. Register an effect compiled for the active backend. An effect that fails to compile or load cannot use the runtime XAML fallback because no brush has materialized yet; the application should omit the registration and allow XAML resolution to select its fallback.

## Limitations

- Effect fills are rectangular and do not automatically clip shader calculations to rounded geometry.
- MGUI does not validate backend portability or manage graphics-resource recreation.
- Custom XAML values are constants. Dynamic values require standard parameters or `ConfigureEffect`.
- Shared effects are mutable and sequential; they are not safe for concurrent drawing from multiple threads.
