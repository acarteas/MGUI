# Effect Fill Requirement Matrix

| Bead | Requirement | Implementation | Automated evidence | Documentation/sample | Status |
| --- | --- | --- | --- | --- | --- |
| `mgui-h5v.1` | Effect registry ownership, uniqueness, replacement, removal, lifetime | `MGResources` effect APIs | `MGResourcesEffectTests` | `effects.md`: Registration And Lifetime | Pass |
| `mgui-h5v.3` | XAML effect/fallback materialization and actionable validation | `XAML.EffectFillBrush` | `EffectFillBrushXamlTests` missing, nested, bound, replacement, and removal cases | `effects.md`: XAML Effect Fills | Pass |
| `mgui-h5v.3` | All representative fill slots | Existing `IFillBrush` materialization path | Background, disabled, selected, overlay, rectangle, border, and padded-fill tests | Shader sample state backgrounds | Pass |
| `mgui-h5v.3` | Standard binding remains opt-in | `UseStandardParameters` defaults to false | XAML and runtime default tests | Standard Parameters | Pass |
| `mgui-h5v.4` | Exact standard values and SpriteBatch projection | `CalculateStandardParameters` and standard binder | Unit matrix/half-pixel tests plus compiled-effect `standard-values` | Standard parameter table | Pass |
| `mgui-h5v.4` | Missing parameters and application order | Cached optional lookup; standard, custom, callback pipeline | Compiled-effect `missing-parameter`, `callback-last`, and `standard-custom-callback-order` | Application Order And Caching | Pass |
| `mgui-h5v.4` | Shared effect reuse, cache invalidation, copy independence | Per-draw rebind, effect-setter invalidation, independent copied arrays/cache | Compiled-effect `shared-aba`, `cache-invalidation`, `copy-independent` | Custom Parameters and Caching | Pass |
| `mgui-h5v.5` | Float/int/bool/color/vector parsing | `EffectParameter.ToParameterValue` | Invariant-culture XAML tests and compiled-effect `custom-types` | Custom Parameters | Pass |
| `mgui-h5v.5` | Name/value validation and incompatible diagnostics | Ordinal duplicate checks and enriched binding exceptions | Malformed, duplicate, unsupported, and compiled incompatible tests | Custom Parameters | Pass |
| `mgui-h5v.5` | Constants-only semantics | Immutable parsed values rebound on every draw | Shared A/B/A compiled-effect test | Custom Parameters and Limitations | Pass |
| `mgui-h5v.2` | Image state tints and fallback precedence | `MGImage.GetTextureColor` | `MGImageVisualStateTests` | Conventional Button Features | Pass |
| `mgui-h5v.2` | Independent hover/press scales | XAML `Element.GetRenderScale` | `ConventionalButtonXamlTests` | Conventional Button Features | Pass |
| `mgui-h5v.2` | Pressed content offset only, spacing-scaled and layout-neutral | `MGButton.DrawContents` and offset calculation | `MGButtonPressedContentOffsetTests` | Conventional Button Features | Pass |
| `mgui-h5v.2` | Tooltip sufficiency assessment | Existing tooltip API; guidance clarified | Sample source regression | Conventional Button Features and sample note | Pass |
| `mgui-h5v.6` | Integrated primarily-XAML state gallery and nonshader comparison | `ShaderEffects.xaml` with registration-only code-behind | `ShaderEffectsSampleTests` parse and feature assertions | Shader sample | Pass |
| `mgui-h5v.6` | Real compiled effect and failure-path execution | `MGUI.EffectTestHost` DesktopGL content pipeline host | `MGEffectFillBrushRealEffectTests` | This matrix and effects guide | Pass |
| `mgui-h5v.6` | Opacity applied once | `UiEffects.fx` uses draw color alpha once | Shader source regression plus compiled sample build | Standard Parameters warning | Pass |
| `mgui-h5v.6` | Manual visual exercise of loaded/fallback paths and interactions | Sample executable | GUI launch was blocked before process creation by the environment approval quota | Manual checklist in bead acceptance criteria | Partial; independent verification required |
| `mgui-h5v` | Preserve programmatic behavior and compatibility | Existing constructor/callback and low-level draw APIs retained | Full solution build and 213-test suite | Programmatic order and limitations | Pass except inherited manual item |
