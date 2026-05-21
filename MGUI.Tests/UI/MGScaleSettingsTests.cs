using System;
using System.Collections.Generic;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.UI
{
    public class MGScaleSettingsTests
    {
        [Fact]
        public void Defaults_AreOne()
        {
            MGScaleSettings settings = new();

            Assert.Equal(1.0f, settings.FontScale);
            Assert.Equal(1.0f, settings.SpacingScale);
            Assert.Equal(1.0f, settings.SizeScale);
            Assert.Equal(1.0f, settings.BorderScale);
            Assert.Equal(1.0f, settings.ImageScale);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(0.0f)]
        [InlineData(-1.0f)]
        public void InvalidScaleValues_Throw(float value)
        {
            MGScaleSettings settings = new();

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.FontScale = value);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetUniformScale(value));
        }

        [Fact]
        public void SetUniformScale_UpdatesAllCategories_AndRaisesOneScaleChangedEvent()
        {
            MGScaleSettings settings = new();
            int scaleChangedCount = 0;
            settings.ScaleChanged += (_, _) => scaleChangedCount++;

            settings.SetUniformScale(1.5f);

            Assert.Equal(1.5f, settings.FontScale);
            Assert.Equal(1.5f, settings.SpacingScale);
            Assert.Equal(1.5f, settings.SizeScale);
            Assert.Equal(1.5f, settings.BorderScale);
            Assert.Equal(1.5f, settings.ImageScale);
            Assert.Equal(1, scaleChangedCount);
        }

        [Fact]
        public void SettingOneCategory_RaisesPropertyChanged_AndOneScaleChangedEvent()
        {
            MGScaleSettings settings = new();
            List<string> changedProperties = new();
            int scaleChangedCount = 0;
            settings.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName ?? string.Empty);
            settings.ScaleChanged += (_, _) => scaleChangedCount++;

            settings.FontScale = 1.25f;

            Assert.Equal(new[] { nameof(MGScaleSettings.FontScale) }, changedProperties);
            Assert.Equal(1, scaleChangedCount);
        }

        [Fact]
        public void SettingCurrentValue_RaisesNoEvents()
        {
            MGScaleSettings settings = new();
            int propertyChangedCount = 0;
            int scaleChangedCount = 0;
            settings.PropertyChanged += (_, _) => propertyChangedCount++;
            settings.ScaleChanged += (_, _) => scaleChangedCount++;

            settings.FontScale = 1.0f;
            settings.SetUniformScale(1.0f);

            Assert.Equal(0, propertyChangedCount);
            Assert.Equal(0, scaleChangedCount);
        }

        [Fact]
        public void GetScale_None_ReturnsOne()
        {
            MGScaleSettings settings = new();
            settings.SetUniformScale(2.0f);

            Assert.Equal(1.0f, settings.GetScale(MGScaleCategory.None));
        }

        [Fact]
        public void ScaleInt_UsesAwayFromZeroRounding()
        {
            MGScaleSettings settings = new();
            settings.SizeScale = 1.5f;
            Assert.Equal(15, settings.ScaleInt(10, MGScaleCategory.Size));

            settings.SizeScale = 1.25f;
            Assert.Equal(6, settings.ScaleInt(5, MGScaleCategory.Size));
            Assert.Equal(-6, settings.ScaleInt(-5, MGScaleCategory.Size));
        }

        [Fact]
        public void ScaleInt_ClampsOverflow()
        {
            MGScaleSettings settings = new();
            settings.SizeScale = 2.0f;

            Assert.Equal(int.MaxValue, settings.ScaleInt(int.MaxValue, MGScaleCategory.Size));
            Assert.Equal(int.MinValue, settings.ScaleInt(int.MinValue, MGScaleCategory.Size));
        }

        [Fact]
        public void BorderAndImage_PreserveNonzeroMinimums()
        {
            MGScaleSettings settings = new();
            settings.BorderScale = 0.25f;
            settings.ImageScale = 0.25f;

            Assert.Equal(1, settings.ScaleInt(1, MGScaleCategory.Border));
            Assert.Equal(-1, settings.ScaleInt(-1, MGScaleCategory.Border));
            Assert.Equal(1, settings.ScaleInt(1, MGScaleCategory.Image));
            Assert.Equal(-1, settings.ScaleInt(-1, MGScaleCategory.Image));
        }

        [Fact]
        public void SpacingAndSize_DoNotForceNonzeroMinimums()
        {
            MGScaleSettings settings = new();
            settings.SpacingScale = 0.25f;
            settings.SizeScale = 0.25f;

            Assert.Equal(0, settings.ScaleInt(1, MGScaleCategory.Spacing));
            Assert.Equal(0, settings.ScaleInt(1, MGScaleCategory.Size));
        }

        [Fact]
        public void CompoundValues_ScaleEveryComponent()
        {
            MGScaleSettings settings = new();
            settings.SpacingScale = 1.5f;

            Point point = settings.ScalePoint(new Point(2, 3), MGScaleCategory.Spacing);
            Size size = settings.ScaleSize(new Size(4, 5), MGScaleCategory.Spacing);
            Thickness thickness = settings.ScaleThickness(new Thickness(1, 2, 3, 4), MGScaleCategory.Spacing);

            Assert.Equal(new Point(3, 5), point);
            Assert.Equal(new Size(6, 8), size);
            Assert.Equal(2, thickness.Left);
            Assert.Equal(3, thickness.Top);
            Assert.Equal(5, thickness.Right);
            Assert.Equal(6, thickness.Bottom);
        }

        [Fact]
        public void NullableAndFloatHelpers_PreserveNoneBehavior()
        {
            MGScaleSettings settings = new();
            settings.ImageScale = 2.0f;

            Assert.Null(settings.ScaleNullableInt(null, MGScaleCategory.Image));
            Assert.Equal(6, settings.ScaleNullableInt(3, MGScaleCategory.Image));
            Assert.Equal(1.5f, settings.ScaleFloat(1.5f, MGScaleCategory.None));
            Assert.Equal(3.0f, settings.ScaleFloat(1.5f, MGScaleCategory.Image));
        }

        [Fact]
        public void UnscaledSettings_AreReadOnly()
        {
            Assert.True(MGScaleSettings.Unscaled.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => MGScaleSettings.Unscaled.FontScale = 1.5f);
            Assert.Throws<InvalidOperationException>(() => MGScaleSettings.Unscaled.SetUniformScale(1.5f));
        }

        [Fact]
        public void SnapshotFromSettings_CopiesScaleValues()
        {
            MGScaleSettings settings = new()
            {
                FontScale = 1.25f,
                SpacingScale = 1.5f,
                SizeScale = 1.75f,
                BorderScale = 2.0f,
                ImageScale = 2.25f
            };

            MGScaleSnapshot snapshot = MGScaleSnapshot.From(settings);

            Assert.Equal(1.25f, snapshot.FontScale);
            Assert.Equal(1.5f, snapshot.SpacingScale);
            Assert.Equal(1.75f, snapshot.SizeScale);
            Assert.Equal(2.0f, snapshot.BorderScale);
            Assert.Equal(2.25f, snapshot.ImageScale);
        }

        [Fact]
        public void SnapshotFromSettings_NullThrows()
        {
            Assert.Throws<ArgumentNullException>(() => MGScaleSnapshot.From(null));
        }
    }
}
