using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;

namespace MGUI.Core.UI
{
    public enum MGScaleCategory
    {
        None,
        Font,
        Spacing,
        Size,
        Border,
        Image
    }

    public enum UIScaleMode
    {
        Inherit,
        Enabled,
        Disabled
    }

    public class MGScaleSettings : ViewModelBase
    {
        internal static MGScaleSettings Unscaled { get; } = CreateReadOnlyUnscaled();

        private bool _IsReadOnly;
        public bool IsReadOnly => _IsReadOnly;

        private float _FontScale = 1.0f;
        public float FontScale
        {
            get => _FontScale;
            set => SetScaleValue(ref _FontScale, value, nameof(FontScale), true);
        }

        private float _SpacingScale = 1.0f;
        public float SpacingScale
        {
            get => _SpacingScale;
            set => SetScaleValue(ref _SpacingScale, value, nameof(SpacingScale), true);
        }

        private float _SizeScale = 1.0f;
        public float SizeScale
        {
            get => _SizeScale;
            set => SetScaleValue(ref _SizeScale, value, nameof(SizeScale), true);
        }

        private float _BorderScale = 1.0f;
        public float BorderScale
        {
            get => _BorderScale;
            set => SetScaleValue(ref _BorderScale, value, nameof(BorderScale), true);
        }

        private float _ImageScale = 1.0f;
        public float ImageScale
        {
            get => _ImageScale;
            set => SetScaleValue(ref _ImageScale, value, nameof(ImageScale), true);
        }

        public event EventHandler ScaleChanged;

        public void SetUniformScale(float scale)
        {
            ThrowIfReadOnly();
            ValidateScale(scale);

            bool AnyChanged = false;
            AnyChanged |= SetScaleValue(ref _FontScale, scale, nameof(FontScale), false);
            AnyChanged |= SetScaleValue(ref _SpacingScale, scale, nameof(SpacingScale), false);
            AnyChanged |= SetScaleValue(ref _SizeScale, scale, nameof(SizeScale), false);
            AnyChanged |= SetScaleValue(ref _BorderScale, scale, nameof(BorderScale), false);
            AnyChanged |= SetScaleValue(ref _ImageScale, scale, nameof(ImageScale), false);

            if (AnyChanged)
            {
                ScaleChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public float GetScale(MGScaleCategory category)
        {
            return category switch
            {
                MGScaleCategory.None => 1.0f,
                MGScaleCategory.Font => FontScale,
                MGScaleCategory.Spacing => SpacingScale,
                MGScaleCategory.Size => SizeScale,
                MGScaleCategory.Border => BorderScale,
                MGScaleCategory.Image => ImageScale,
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        public int ScaleInt(int value, MGScaleCategory category)
        {
            if (category == MGScaleCategory.None || value == 0)
            {
                return value;
            }

            double scaled = Math.Round((double)value * GetScale(category), MidpointRounding.AwayFromZero);
            int scaledValue = scaled switch
            {
                > int.MaxValue => int.MaxValue,
                < int.MinValue => int.MinValue,
                _ => (int)scaled
            };

            if (scaledValue == 0 && ShouldPreserveNonzeroMinimum(category))
            {
                return value > 0 ? 1 : -1;
            }

            return scaledValue;
        }

        public int? ScaleNullableInt(int? value, MGScaleCategory category)
            => value.HasValue ? ScaleInt(value.Value, category) : null;

        public float ScaleFloat(float value, MGScaleCategory category)
            => category == MGScaleCategory.None ? value : value * GetScale(category);

        public Point ScalePoint(Point value, MGScaleCategory category)
            => new(ScaleInt(value.X, category), ScaleInt(value.Y, category));

        public Size ScaleSize(Size value, MGScaleCategory category)
            => new(ScaleInt(value.Width, category), ScaleInt(value.Height, category));

        public Thickness ScaleThickness(Thickness value, MGScaleCategory category)
            => new(
                ScaleInt(value.Left, category),
                ScaleInt(value.Top, category),
                ScaleInt(value.Right, category),
                ScaleInt(value.Bottom, category));

        private bool SetScaleValue(ref float field, float value, string propertyName, bool raiseScaleChanged)
        {
            ThrowIfReadOnly();
            ValidateScale(value);

            if (field == value)
            {
                return false;
            }

            field = value;
            NPC(propertyName);

            if (raiseScaleChanged)
            {
                ScaleChanged?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        private static void ValidateScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Scale values must be finite and greater than zero.");
            }
        }

        private static bool ShouldPreserveNonzeroMinimum(MGScaleCategory category)
            => category is MGScaleCategory.Border or MGScaleCategory.Image;

        private void ThrowIfReadOnly()
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException($"{nameof(MGScaleSettings)} is read-only.");
            }
        }

        private static MGScaleSettings CreateReadOnlyUnscaled()
        {
            MGScaleSettings settings = new();
            settings._IsReadOnly = true;
            return settings;
        }
    }
}
