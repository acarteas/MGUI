# Shader Effects

MGUI can use a caller-owned MonoGame `Effect` to draw a rectangular fill or the texture-backed regions of a nine-slice fill. The application is responsible for compiling an artifact for each graphics backend, loading it, registering it, and disposing it. MGUI does not compile, clone, hot-reload, convert, or dispose effects.

`MGEffectFillBrush` shades its generated rectangular fill. `MGNineSliceFillBrush` shades only its texture-backed regions. Neither brush captures or post-processes an element subtree.

## Registration And Lifetime

Register effects before materializing XAML that references them:

```csharp
Effect effect = Content.Load<Effect>("Effects/HudButton");
Desktop.Resources.AddEffect("HudButtonEffect", effect);
```

Names are application-defined and case-sensitive. `AddEffect` rejects duplicates. `AddOrReplaceEffect`, `RemoveEffect`, and `TryGetEffect` support explicit resource changes. Replacing or removing an effect does not dispose either object.

XAML resolution is a materialization-time snapshot. A materialized brush stores the resolved `Effect` reference, not its resource name. Existing brushes therefore keep their current effect after a resource is replaced or removed; newly materialized XAML observes the new registration.

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

`EffectName` must be nonblank. Custom parameter declarations are validated and converted before effect lookup, so malformed values, blank or duplicate names, unsupported types, and non-finite values fail identically whether the effect is registered or a fallback would be selected. If the effect is absent from `Desktop.Resources.Effects` and the declarations are valid, a configured `FallbackBrush` is materialized without applying the converted effect parameters. Without a fallback, materialization throws an actionable error. Nested fallback brushes and bindable fallback values are supported.

`EffectFillBrush` works in every `IFillBrush` slot, including normal, hovered, pressed, selected, and disabled backgrounds, borders, overlays, and nested border fills.

## Effect-Backed Nine-Slices

Register both resources before materializing the XAML:

```csharp
Effect effect = Content.Load<Effect>("Effects/OrnamentalFrame");
Texture2D texture = Content.Load<Texture2D>("Textures/OrnamentalFrame");

Desktop.Resources.AddEffect("OrnamentalFrameEffect", effect);
Desktop.Resources.AddTexture("OrnamentalFrameTexture", new MGTextureData(texture));
```

This example keeps the center texture-backed, so all nine source regions receive the effect:

```xml
<NineSliceFillBrush SourceName="OrnamentalFrameTexture"
                    SourceMargin="16"
                    TargetMargin="8"
                    EffectName="OrnamentalFrameEffect"
                    UseStandardParameters="True">
    <EffectParameter Name="TintColor" Type="Color" Value="White" />
    <EffectParameter Name="TreatmentDirection" Type="Vector2" Value="1,0.65" />
    <EffectParameter Name="TreatmentStrength" Type="Float" Value="0.75" />
</NineSliceFillBrush>
```

Adding an `InteriorBrush` replaces the center texture region. The effect continues to shade the eight texture-backed frame regions, while the interior brush draws independently:

```xml
<NineSliceFillBrush SourceName="OrnamentalFrameTexture"
                    SourceMargin="16"
                    TargetMargin="8"
                    EffectName="OrnamentalFrameEffect"
                    UseStandardParameters="True">
    <NineSliceFillBrush.InteriorBrush>
        <SolidFillBrush Color="rgb(34,43,52)" />
    </NineSliceFillBrush.InteriorBrush>
    <EffectParameter Name="TintColor" Type="Color" Value="White" />
    <EffectParameter Name="TreatmentDirection" Type="Vector2" Value="1,0.65" />
    <EffectParameter Name="TreatmentStrength" Type="Float" Value="0.75" />
</NineSliceFillBrush>
```

The equivalent runtime surface is the `MGNineSliceFillBrush` constructor overload whose first argument is an `Effect`. Its `UseStandardParameters`, `Parameters`, and optional `ConfigureEffect` properties follow the same ordering and shared-effect rules as `MGEffectFillBrush`. Existing effect-free constructors remain available.

Unlike `EffectFillBrush`, `NineSliceFillBrush` has no fallback brush for a missing effect. A nonblank `EffectName` must resolve from `MGResources.Effects` during materialization or an `InvalidOperationException` identifies the missing name and tells the application to register it first. Blank names, effect parameters without an `EffectName`, and standard-parameter opt-in without an `EffectName` also fail with actionable diagnostics instead of silently drawing without the effect.

## Standard Parameters

Standard binding is opt-in for both XAML and programmatic brushes with `UseStandardParameters="True"`. Missing shader parameters are skipped.

| Name | Shader type | Value |
| --- | --- | --- |
| `MatrixTransform` | `float4x4` | Current transform times a SpriteBatch-compatible orthographic projection, including the configured half-pixel convention |
| `ElementPosition` | `float2` | Fill top-left in pre-transform destination space after the current draw offset |
| `ElementSize` | `float2` | Fill width and height in pre-transform destination space, each clamped to at least one |
| `Opacity` | `float` | Effective draw opacity |
| `TimeSeconds` | `float` | Total renderer time in seconds |
| `HoverAmount` | `float` | `1` when hovered, otherwise `0` |
| `PressAmount` | `float` | `1` when pressed, otherwise `0` |
| `SelectedAmount` | `float` | `1` when selected, otherwise `0` |
| `DisabledAmount` | `float` | `1` when disabled, otherwise `0` |
| `ElementTextureCoordinateScale` | `float2` | Per-slice scale used to reconstruct continuous whole-element coordinates for an effect-backed nine-slice |
| `ElementTextureCoordinateOffset` | `float2` | Per-slice offset used to reconstruct continuous whole-element coordinates for an effect-backed nine-slice |

The draw color already carries effective opacity. A shader should not multiply alpha by both `input.Color.a` and `Opacity`; use one alpha application. `Opacity` remains available for non-alpha decisions.

`TextureCoordinate` is the normalized fill-local coordinate that moves with the geometry through `MatrixTransform`, so it is the preferred source for procedural fill effects that need stable local UVs. If a shader combines `SV_POSITION` with `ElementPosition` or `ElementSize`, it must explicitly convert both values into the same coordinate space first.

`MatrixTransform` may include window scaling, element render scaling, or other affine draw transforms. `ElementPosition` and `ElementSize` remain the untransformed destination rectangle for the fill.

### Nine-Slice Coordinates And Texture Sampling

A nine-slice is emitted as separate SpriteBatch draws. `TextureCoordinate` must remain unchanged because it addresses the current source rectangle in the SpriteBatch-bound texture. For each texture-backed slice, compute the continuous normalized coordinate across the complete destination with this exact formula:

```hlsl
float2 wholeElementCoordinate =
    TextureCoordinate * ElementTextureCoordinateScale
    + ElementTextureCoordinateOffset;
```

MGUI derives the mapping as follows, component by component:

```text
sourceStart = sourceRectangle.xy / textureSize
sourceSize = sourceRectangle.wh / textureSize
elementStart = (sliceDestination.xy - completeDestination.xy) / completeDestination.wh
elementSize = sliceDestination.wh / completeDestination.wh
ElementTextureCoordinateScale = elementSize / sourceSize
ElementTextureCoordinateOffset =
    elementStart - sourceStart * ElementTextureCoordinateScale
```

This makes `wholeElementCoordinate` run continuously from `(0,0)` at the complete destination's top-left to `(1,1)` at its bottom-right instead of restarting in each slice.

SpriteBatch binds the current source texture to sampler slot `s0` on the validated DesktopGL path. Sample the original `TextureCoordinate`, not `wholeElementCoordinate`:

```hlsl
sampler2D SpriteTextureSampler : register(s0);

float4 sampledColor = tex2D(SpriteTextureSampler, TextureCoordinate);
```

Do not assign the source texture as an MGUI custom parameter. The texture comes from each `MGTextureData` draw, while the affine parameters describe where that draw lies in the complete nine-slice destination. Backend-compatible shader syntax and sampler declarations remain the application's responsibility.

## Custom Parameters

Supported types are `Float`, `Int`, `Bool`, `Color`, `Vector2`, `Vector3`, and `Vector4`. Parsing is culture-invariant. Boolean, integer, floating-point, and color values can be inferred; vectors require an explicit `Type` because comma-separated text is ambiguous. Vector values use comma-separated components. `Color` is bound as a normalized `float4`.

Parameter names must be nonblank and unique using ordinal, case-sensitive comparison. Scalars and vector components must be finite. Diagnostics identify the name, requested runtime type, supplied value, and expected format. Missing shader parameters are skipped. Incompatible present parameters throw with both the requested type and the shader parameter class/type.

Custom parameters are constants applied before every draw. Per-frame application prevents state leakage when one mutable effect is shared by multiple brushes only when each brush declares the complete custom state vector that can affect its shader path. Include explicit neutral values for parameters unused by that brush mode instead of relying on shader defaults or the previous draw; an A/B/A draw sequence then restores A's complete state. For nine-slices, the whole-element standard and custom values are applied before drawing the texture-backed region sequence, and the coordinate scale and offset are updated for each slice.

## Application Order And Caching

Each draw applies values in this order:

1. Standard parameters.
2. Custom parameters, which may deliberately override a standard name.
3. `ConfigureEffect`, which may override either set.

Parameter lookups, including misses, are cached per brush/effect pair. Assigning a different `Effect` invalidates the cache. `Copy()` creates independent parameter and cache state while retaining the same caller-owned effect reference until either brush is reassigned.

## Conventional Button Features

Shader-backed controls can be compared with conventional image/button states:

- `MGImage` state tint precedence is disabled, pressed, hovered, selected, then normal `TextureColor`; each missing state tint falls directly back to normal or white.
- `RenderScale`, `HoveredRenderScale`, and `PressedRenderScale` each reject explicitly supplied non-finite or non-positive values. Hover uses `HoveredRenderScale ?? RenderScale ?? 1`; press uses `PressedRenderScale ?? RenderScale ?? 1`, and pressed wins while both states are active.
- `PressedContentOffset` translates button content only while pressed and enabled. It uses spacing scale, does not affect background/border drawing, and does not change measurement or layout.
- A tooltip is sufficient when it names or explains the action. Icon-only actions need one; visible labels generally do not unless extra context is useful.

The sample at `MGUI.Samples/Features/ShaderEffects.xaml` demonstrates shader normal, primary, selected, and disabled actions; a nonshader comparison; and effect-backed ornamental nine-slices with both a texture-backed center and a separate `InteriorBrush`, predominantly in XAML.

## Backend Compatibility

MonoGame effects are backend-specific runtime objects. DesktopGL, Vulkan, DirectX, mobile, and other targets may need different profiles, bytecode, sampler declarations, or artifacts. Register an effect compiled for the active backend. The sample's `s0` convention has been executed and validated on this repository's DesktopGL backend; it is not a claim that compiled MGFX bytecode is portable to other backends.

An effect that fails to compile or load cannot use the runtime XAML fallback because no brush has materialized yet. For `EffectFillBrush`, the application can omit the registration and allow valid XAML resolution to select its configured fallback. `NineSliceFillBrush` has no effect fallback, so the application must avoid materializing that declaration or surface the load failure.

## Limitations

- Effect fills are rectangular and do not automatically clip shader calculations to rounded geometry.
- MGUI does not validate backend portability or manage graphics-resource recreation.
- Custom XAML values are constants. Dynamic values require standard parameters or `ConfigureEffect`.
- Shared effects are mutable and sequential; they are not safe for concurrent drawing from multiple threads.
- A nine-slice effect shades only texture-backed regions. An explicit `InteriorBrush` runs according to its own brush behavior, and the frame effect does not implicitly wrap it.
- Effects do not shade child content, labels, overlays drawn by other brushes, or an element subtree. Subtree effects require a separate render-target or post-processing design outside these fill brushes.
