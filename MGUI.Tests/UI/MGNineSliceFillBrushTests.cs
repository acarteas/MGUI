using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.UI
{
    public class MGNineSliceFillBrushTests
    {
        [Fact]
        public void EffectiveTargetMargin_ScalesUniformMarginByBorderScale()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);

            Thickness actual = MGNineSliceFillBrush.GetEffectiveTargetMargin(element, new Thickness(26));

            Assert.Equal(new Thickness(39), actual);
        }

        [Fact]
        public void EffectiveTargetMargin_ScalesAsymmetricMarginsSideBySide()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);

            Thickness actual = MGNineSliceFillBrush.GetEffectiveTargetMargin(element, new Thickness(1, 2, 3, 4));

            Assert.Equal(new Thickness(2, 3, 5, 6), actual);
        }

        [Fact]
        public void EffectiveTargetMargin_UsesUnscaledMarginWhenElementUIScaleIsDisabled()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);
            element.UIScaleMode = UIScaleMode.Disabled;

            Thickness actual = MGNineSliceFillBrush.GetEffectiveTargetMargin(element, new Thickness(26));

            Assert.Equal(new Thickness(26), actual);
        }

        [Fact]
        public void SourceMarginRegionCalculation_UsesRawTexturePixels()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);
            Thickness sourceMargin = new(7, 11, 13, 17);
            Rectangle sourceBounds = new(10, 20, 100, 80);

            MGNineSliceFillBrush.NineSliceRegions actual = MGNineSliceFillBrush.GetRegions(sourceBounds, sourceMargin);

            Assert.Equal(new Thickness(11, 17, 20, 26), MGNineSliceFillBrush.GetEffectiveTargetMargin(element, new Thickness(7, 11, 13, 17)));
            Assert.Equal(new Rectangle(10, 20, 7, 11), actual.TopLeft);
            Assert.Equal(new Rectangle(17, 20, 80, 11), actual.TopCenter);
            Assert.Equal(new Rectangle(97, 20, 13, 11), actual.TopRight);
            Assert.Equal(new Rectangle(10, 31, 7, 52), actual.MiddleLeft);
            Assert.Equal(new Rectangle(17, 31, 80, 52), actual.MiddleCenter);
            Assert.Equal(new Rectangle(97, 31, 13, 52), actual.MiddleRight);
            Assert.Equal(new Rectangle(10, 83, 7, 17), actual.BottomLeft);
            Assert.Equal(new Rectangle(17, 83, 80, 17), actual.BottomCenter);
            Assert.Equal(new Rectangle(97, 83, 13, 17), actual.BottomRight);
        }

        [Fact]
        public void DestinationRegionCalculation_UsesEffectiveTargetMargin()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 2.0f);
            Rectangle destinationBounds = new(10, 20, 200, 100);

            MGNineSliceFillBrush.NineSliceRegions actual = MGNineSliceFillBrush.GetDestinationRegions(element, destinationBounds, new Thickness(8));

            Assert.Equal(new Rectangle(26, 36, 168, 68), actual.MiddleCenter);
        }

        [Fact]
        public void DestinationRegionCalculation_UsesAsymmetricEffectiveTargetMargin()
        {
            TestElement element = CreateElement(scale => scale.BorderScale = 1.5f);
            Rectangle destinationBounds = new(10, 20, 100, 80);

            MGNineSliceFillBrush.NineSliceRegions actual = MGNineSliceFillBrush.GetDestinationRegions(element, destinationBounds, new Thickness(4, 8, 12, 16));

            Assert.Equal(new Rectangle(16, 32, 76, 44), actual.MiddleCenter);
        }

        [Fact]
        public void Copy_CopiesInteriorBrush()
        {
            TestFillBrush interiorBrush = new();
            MGNineSliceFillBrush brush = new(
                new Thickness(10),
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                default,
                interiorBrush);

            MGNineSliceFillBrush actual = Assert.IsType<MGNineSliceFillBrush>(brush.Copy());

            TestFillBrush copiedInteriorBrush = Assert.IsType<TestFillBrush>(actual.InteriorBrush);
            Assert.NotSame(interiorBrush, copiedInteriorBrush);
            Assert.True(interiorBrush.WasCopied);
        }

        [Fact]
        public void EffectConstructor_StoresRuntimeConfiguration()
        {
            Effect effect = CreateEffect();
            Action<Effect, ElementDrawArgs, MGElement, Rectangle> configureEffect = (_, _, _, _) => { };

            MGNineSliceFillBrush brush = CreateBrush(effect, null, configureEffect);

            Assert.Same(effect, brush.Effect);
            Assert.Same(configureEffect, brush.ConfigureEffect);
            Assert.False(brush.UseStandardParameters);
            Assert.Empty(brush.Parameters);
            Assert.True(brush.HasEffectBinding);
        }

        [Fact]
        public void LegacySourceConstructor_DoesNotCreateEffectBinding()
        {
            Texture2D texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
            MGTextureData source = new(texture, new Rectangle(0, 0, 3, 3));

            MGNineSliceFillBrush brush = new(new Thickness(1), source, new Thickness(1));

            Assert.False(brush.HasEffectBinding);
            Assert.False(Assert.IsType<MGNineSliceFillBrush>(brush.Copy()).HasEffectBinding);
        }

        [Fact]
        public void LegacyExplicitSliceConstructor_DoesNotCreateEffectBinding()
        {
            MGNineSliceFillBrush brush = CreateBrush();

            Assert.False(brush.HasEffectBinding);
            Assert.False(Assert.IsType<MGNineSliceFillBrush>(brush.Copy()).HasEffectBinding);
        }

        [Fact]
        public void EffectFreeBrush_IncludesAllNineNonEmptyTextureRegions()
        {
            MGNineSliceFillBrush brush = CreateBrush();

            IReadOnlyList<(MGTextureData Texture, Rectangle Destination)> regions =
                brush.GetTextureBackedRegions(CreateUnitRegions());

            Assert.Equal(9, regions.Count);
            Assert.Same(brush.MiddleCenter.Texture, regions[4].Texture.Texture);
        }

        [Fact]
        public void ExplicitInteriorBrush_ExcludesCenterFromTextureEffectParticipation()
        {
            MGNineSliceFillBrush brush = CreateBrush(null, new TestFillBrush());

            IReadOnlyList<(MGTextureData Texture, Rectangle Destination)> regions =
                brush.GetTextureBackedRegions(CreateUnitRegions());

            Assert.Equal(8, regions.Count);
            Assert.DoesNotContain(regions, x => ReferenceEquals(x.Texture.Texture, brush.MiddleCenter.Texture));
        }

        [Fact]
        public void TextureRegionParticipation_ExcludesEmptyRegions()
        {
            MGNineSliceFillBrush brush = CreateBrush();
            MGNineSliceFillBrush.NineSliceRegions regions = CreateUnitRegions() with
            {
                TopCenter = new Rectangle(1, 0, 0, 1),
                MiddleLeft = new Rectangle(0, 1, 1, -1)
            };

            IReadOnlyList<(MGTextureData Texture, Rectangle Destination)> actual =
                brush.GetTextureBackedRegions(regions);

            Assert.Equal(7, actual.Count);
            Assert.DoesNotContain(actual, x => ReferenceEquals(x.Texture.Texture, brush.TopCenter.Texture));
            Assert.DoesNotContain(actual, x => ReferenceEquals(x.Texture.Texture, brush.MiddleLeft.Texture));
        }

        [Fact]
        public void EffectScope_ActivatesEffectAndRestoresPreviousEffect()
        {
            Effect previousEffect = CreateEffect();
            Effect frameEffect = CreateEffect();
            DrawTransaction transaction = CreateTransaction(DrawSettings.Default with { Effect = previousEffect });
            Effect? observedEffect = null;

            MGNineSliceFillBrush.DrawWithEffectTemporary(
                transaction,
                frameEffect,
                () => observedEffect = transaction.CurrentSettings.Effect);

            Assert.Same(frameEffect, observedEffect);
            Assert.Same(previousEffect, transaction.CurrentSettings.Effect);
        }

        [Fact]
        public void EffectScope_RestoresPreviousEffectAfterException()
        {
            Effect previousEffect = CreateEffect();
            Effect frameEffect = CreateEffect();
            DrawTransaction transaction = CreateTransaction(DrawSettings.Default with { Effect = previousEffect });

            Assert.Throws<InvalidOperationException>(() =>
                MGNineSliceFillBrush.DrawWithEffectTemporary(
                    transaction,
                    frameEffect,
                    () => throw new InvalidOperationException("Test failure.")));

            Assert.Same(previousEffect, transaction.CurrentSettings.Effect);
        }

        [Fact]
        public void Copy_PreservesEffectConfigurationAndCopiesMutableStateIndependently()
        {
            Effect effect = CreateEffect();
            TestFillBrush interiorBrush = new();
            Action<Effect, ElementDrawArgs, MGElement, Rectangle> configureEffect = (_, _, _, _) => { };
            MGNineSliceFillBrush brush = CreateBrush(effect, interiorBrush, configureEffect);
            brush.UseStandardParameters = true;
            brush.Parameters = new[] { new MGEffectParameterValue("Role", MGEffectParameterType.Int, 3) };

            MGNineSliceFillBrush copy = Assert.IsType<MGNineSliceFillBrush>(brush.Copy());
            brush.Parameters = new[] { new MGEffectParameterValue("Role", MGEffectParameterType.Int, 4) };

            Assert.Same(effect, copy.Effect);
            Assert.Same(configureEffect, copy.ConfigureEffect);
            Assert.True(copy.UseStandardParameters);
            Assert.Equal(3, copy.Parameters[0].Value);
            Assert.NotSame(brush.Parameters, copy.Parameters);
            Assert.IsType<TestFillBrush>(copy.InteriorBrush);
            Assert.NotSame(interiorBrush, copy.InteriorBrush);
        }

        private static MGNineSliceFillBrush CreateBrush(
            Effect? effect = null,
            IFillBrush? interiorBrush = null,
            Action<Effect, ElementDrawArgs, MGElement, Rectangle>? configureEffect = null)
        {
            MGTextureData[] textures = Enumerable.Range(0, 9)
                .Select(_ => new MGTextureData((Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D))))
                .ToArray();

            return effect == null
                ? new MGNineSliceFillBrush(
                    new Thickness(1),
                    textures[0], textures[1], textures[2],
                    textures[3], textures[4], textures[5],
                    textures[6], textures[7], textures[8],
                    interiorBrush)
                : new MGNineSliceFillBrush(
                    effect,
                    new Thickness(1),
                    textures[0], textures[1], textures[2],
                    textures[3], textures[4], textures[5],
                    textures[6], textures[7], textures[8],
                    interiorBrush,
                    configureEffect);
        }

        private static MGNineSliceFillBrush.NineSliceRegions CreateUnitRegions()
            => new(
                new Rectangle(0, 0, 1, 1),
                new Rectangle(1, 0, 1, 1),
                new Rectangle(2, 0, 1, 1),
                new Rectangle(0, 1, 1, 1),
                new Rectangle(1, 1, 1, 1),
                new Rectangle(2, 1, 1, 1),
                new Rectangle(0, 2, 1, 1),
                new Rectangle(1, 2, 1, 1),
                new Rectangle(2, 2, 1, 1));

        private static DrawTransaction CreateTransaction(DrawSettings settings)
        {
            DrawTransaction transaction = (DrawTransaction)RuntimeHelpers.GetUninitializedObject(typeof(DrawTransaction));
            SetField(transaction, "<CurrentSettings>k__BackingField", settings);
            return transaction;
        }

        private static Effect CreateEffect()
            => (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));

        private static TestElement CreateElement(Action<MGScaleSettings> configureScale)
        {
            MGScaleSettings scale = new();
            configureScale(scale);

            MGDesktop desktop = (MGDesktop)RuntimeHelpers.GetUninitializedObject(typeof(MGDesktop));
            SetField(desktop, "<Windows>k__BackingField", new List<MGWindow>());
            desktop.UIScale = scale;

            MGWindow window = (MGWindow)RuntimeHelpers.GetUninitializedObject(typeof(MGWindow));
            InitializeElementForScaleRefresh(window);
            SetField(window, "<Desktop>k__BackingField", desktop);
            SetField(window, "<ElementType>k__BackingField", MGElementType.Window);
            SetField(window, "_NestedWindows", new List<MGWindow>());

            TestElement element = (TestElement)RuntimeHelpers.GetUninitializedObject(typeof(TestElement));
            InitializeElementForScaleRefresh(element);
            SetField(element, "<ParentWindow>k__BackingField", window);
            SetField(element, "<ElementType>k__BackingField", MGElementType.Border);
            return element;
        }

        private static void InitializeElementForScaleRefresh(MGElement element)
        {
            SetField(element, "<InitializationManager>k__BackingField", new DeferEventsManager(() => { }));
            SetField(element, "<Components>k__BackingField", new List<MGComponentBase>());
            SetField(element, "<RecentMeasurementsFull>k__BackingField", new List<ElementMeasurement>());
            SetField(element, "<RecentMeasurementsSelfOnly>k__BackingField", new List<ElementMeasurement>());
        }

        private static void SetField(object instance, string name, object? value)
        {
            Type? type = instance.GetType();
            while (type != null)
            {
                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(instance, value);
                    return;
                }

                type = type.BaseType;
            }

            throw new MissingFieldException(instance.GetType().FullName, name);
        }

        private sealed class TestElement : MGElement
        {
            private TestElement()
                : base(default(MGWindow), MGElementType.Border)
            {
            }
        }

        private sealed class TestFillBrush : IFillBrush
        {
            public bool WasCopied { get; private set; }

            public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
            {
            }

            public IFillBrush Copy()
            {
                WasCopied = true;
                return new TestFillBrush();
            }
        }
    }
}
