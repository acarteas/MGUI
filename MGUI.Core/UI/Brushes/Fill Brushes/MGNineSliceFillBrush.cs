using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Core.UI.Brushes.Fill_Brushes
{
    /// <summary>An <see cref="IFillBrush"/> that draws a nine-sliced (also called a nine-patch) texture to the destination bounds using a customizable margin to control how each patch scales to the bounds.<para/>
    /// See also: <see href="https://en.wikipedia.org/wiki/9-slice_scaling"/><para/>
    /// Assigning this struct copies its shared runtime effect configuration reference. Use <see cref="Copy"/> for an independently configurable binding and copied interior brush.</summary>
    public readonly struct MGNineSliceFillBrush : IFillBrush
    {
        private readonly MGEffectBinding Binding;

        /// <summary>The caller-owned effect applied to texture-backed slices. This brush does not dispose or clone the effect.</summary>
        public Effect Effect
        {
            get => Binding?.Effect;
            set => GetBinding().Effect = value;
        }

        /// <summary>Optional callback invoked after standard and constant parameters are applied.</summary>
        public Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect
        {
            get => Binding?.ConfigureEffect;
            set => GetBinding().ConfigureEffect = value;
        }

        /// <summary>
        /// Whether MGUI's conventional draw, bounds, time, visual-state, and per-slice texture-coordinate parameters are set when present.
        /// For a texture-backed slice, <c>TextureCoordinate * ElementTextureCoordinateScale + ElementTextureCoordinateOffset</c>
        /// produces normalized coordinates across the complete destination while the original texture coordinate remains available for sampling.
        /// </summary>
        public bool UseStandardParameters
        {
            get => Binding?.UseStandardParameters ?? false;
            set => GetBinding().UseStandardParameters = value;
        }

        /// <summary>Application-specific constant parameters applied before <see cref="ConfigureEffect"/>.</summary>
        public IReadOnlyList<MGEffectParameterValue> Parameters
        {
            get => Binding?.Parameters ?? Array.Empty<MGEffectParameterValue>();
            set => GetBinding().Parameters = value;
        }

        internal bool HasEffectBinding => Binding != null;

        /// <summary>The unscaled UI thickness used for destination slices. Rendering scales this through <see cref="MGScaleCategory.Border"/> on the owning element.</summary>
        public readonly Thickness TargetMargin;

        public readonly MGTextureData TopLeft;
        public readonly MGTextureData TopCenter;
        public readonly MGTextureData TopRight;
        public readonly MGTextureData MiddleLeft;
        public readonly MGTextureData MiddleCenter;
        public readonly MGTextureData MiddleRight;
        public readonly MGTextureData BottomLeft;
        public readonly MGTextureData BottomCenter;
        public readonly MGTextureData BottomRight;
        public readonly IFillBrush InteriorBrush;

        /// <param name="Source">The texture that will be divided up into 9 rectangular regions.</param>
        /// <param name="TargetMargin">Determines the unscaled UI size of each slice when rendering the texture to the destination bounds. This value is scaled with the owning element's border UI scale.<para/>
        /// EX: If margin.Left=10 and margin.Top=16, the top-left region of the source texture will be drawn to the topleft 10x16 pixels (assuming 1x UI scaling) of the destination bounds whenever this brush is rendered.</param>
        /// <param name="SourceMargin">Optional. Determines how the source texture is divided up into 9 rectangular regions.<para/>
        /// If <see langword="null"/>, each region of the source texture is assumed to be equally-sized (and thus its dimensions should be an exact multiple of 3)</param>
        /// <param name="InteriorBrush">Optional. If specified, replaces the center source region and is drawn to the interior destination bounds.</param>
        public MGNineSliceFillBrush(Thickness TargetMargin, MGTextureData Source, Thickness? SourceMargin = null, IFillBrush InteriorBrush = null)
            : this(null, TargetMargin, Source, SourceMargin, InteriorBrush)
        {
        }

        /// <param name="Effect">The caller-owned effect applied to texture-backed slices.</param>
        /// <param name="ConfigureEffect">Optional callback invoked after standard and constant parameters are applied.</param>
        /// <inheritdoc cref="MGNineSliceFillBrush(Thickness, MGTextureData, Thickness?, IFillBrush)"/>
        public MGNineSliceFillBrush(
            Effect Effect,
            Thickness TargetMargin,
            MGTextureData Source,
            Thickness? SourceMargin = null,
            IFillBrush InteriorBrush = null,
            Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect = null)
            : this(new MGEffectBinding(Effect, ConfigureEffect), TargetMargin, Source, SourceMargin, InteriorBrush)
        {
        }

        private MGNineSliceFillBrush(
            MGEffectBinding Binding,
            Thickness TargetMargin,
            MGTextureData Source,
            Thickness? SourceMargin,
            IFillBrush InteriorBrush)
        {
            this.Binding = Binding;
            this.TargetMargin = TargetMargin;
            this.InteriorBrush = InteriorBrush;

            Texture2D Texture = Source.Texture;
            if (Texture == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }

            Rectangle Bounds = Source.SourceRect ?? Texture.Bounds;

            //  Validate the source margin
            Thickness Margin;
            if (SourceMargin.HasValue)
            {
                if (SourceMargin.Value.Sides().Any(x => x <= 0))
                {
                    throw new InvalidDataException($"Invalid {nameof(SourceMargin)}. All sides must have a value greater than zero. Actual value: {SourceMargin.Value}");
                }

                Margin = SourceMargin.Value;
            }
            else
            {
                if (Bounds.Width % 3 != 0 || Bounds.Height % 3 != 0)
                {
                    throw new InvalidDataException($"Invalid input texture dimensions. " +
                        $"The source texture must be evenly-divisible by 3 to calculate each of the 9 regions. Actual dimensions: {Bounds.Width}x{Bounds.Height}");
                }
                Margin = new(Bounds.Width / 3, Bounds.Height / 3);
            }

            NineSliceRegions SourceRegions = GetRegions(Bounds, Margin);
            TopLeft = new(Texture, SourceRegions.TopLeft, Source.Opacity, Source.RenderSizeOverride);
            TopCenter = new(Texture, SourceRegions.TopCenter, Source.Opacity, Source.RenderSizeOverride);
            TopRight = new(Texture, SourceRegions.TopRight, Source.Opacity, Source.RenderSizeOverride);
            MiddleLeft = new(Texture, SourceRegions.MiddleLeft, Source.Opacity, Source.RenderSizeOverride);
            MiddleCenter = new(Texture, SourceRegions.MiddleCenter, Source.Opacity, Source.RenderSizeOverride);
            MiddleRight = new(Texture, SourceRegions.MiddleRight, Source.Opacity, Source.RenderSizeOverride);
            BottomLeft = new(Texture, SourceRegions.BottomLeft, Source.Opacity, Source.RenderSizeOverride);
            BottomCenter = new(Texture, SourceRegions.BottomCenter, Source.Opacity, Source.RenderSizeOverride);
            BottomRight = new(Texture, SourceRegions.BottomRight, Source.Opacity, Source.RenderSizeOverride);
        }

        /// <param name="TargetMargin">Determines the unscaled UI size of each slice when rendering the texture to the destination bounds. This value is scaled with the owning element's border UI scale.<para/>
        /// EX: If margin.Left=10 and margin.Top=16, the top-left region of the source texture will be drawn to the topleft 10x16 pixels (assuming 1x UI scaling) of the destination bounds whenever this brush is rendered.</param>
        public MGNineSliceFillBrush(Thickness TargetMargin,
            MGTextureData TopLeft, MGTextureData TopCenter, MGTextureData TopRight,
            MGTextureData MiddleLeft, MGTextureData MiddleCenter, MGTextureData MiddleRight,
            MGTextureData BottomLeft, MGTextureData BottomCenter, MGTextureData BottomRight,
            IFillBrush InteriorBrush = null)
            : this(null, TargetMargin,
                TopLeft, TopCenter, TopRight,
                MiddleLeft, MiddleCenter, MiddleRight,
                BottomLeft, BottomCenter, BottomRight,
                InteriorBrush)
        {
        }

        /// <param name="Effect">The caller-owned effect applied to texture-backed slices.</param>
        /// <param name="ConfigureEffect">Optional callback invoked after standard and constant parameters are applied.</param>
        /// <inheritdoc cref="MGNineSliceFillBrush(Thickness, MGTextureData, MGTextureData, MGTextureData, MGTextureData, MGTextureData, MGTextureData, MGTextureData, MGTextureData, MGTextureData, IFillBrush)"/>
        public MGNineSliceFillBrush(
            Effect Effect,
            Thickness TargetMargin,
            MGTextureData TopLeft, MGTextureData TopCenter, MGTextureData TopRight,
            MGTextureData MiddleLeft, MGTextureData MiddleCenter, MGTextureData MiddleRight,
            MGTextureData BottomLeft, MGTextureData BottomCenter, MGTextureData BottomRight,
            IFillBrush InteriorBrush = null,
            Action<Effect, ElementDrawArgs, MGElement, Rectangle> ConfigureEffect = null)
            : this(new MGEffectBinding(Effect, ConfigureEffect), TargetMargin,
                TopLeft, TopCenter, TopRight,
                MiddleLeft, MiddleCenter, MiddleRight,
                BottomLeft, BottomCenter, BottomRight,
                InteriorBrush)
        {
        }

        private MGNineSliceFillBrush(
            MGEffectBinding Binding,
            Thickness TargetMargin,
            MGTextureData TopLeft, MGTextureData TopCenter, MGTextureData TopRight,
            MGTextureData MiddleLeft, MGTextureData MiddleCenter, MGTextureData MiddleRight,
            MGTextureData BottomLeft, MGTextureData BottomCenter, MGTextureData BottomRight,
            IFillBrush InteriorBrush)
        {
            this.Binding = Binding;
            this.TargetMargin = TargetMargin;
            this.InteriorBrush = InteriorBrush;

            this.TopLeft = TopLeft;
            this.TopCenter = TopCenter;
            this.TopRight = TopRight;
            this.MiddleLeft = MiddleLeft;
            this.MiddleCenter = MiddleCenter;
            this.MiddleRight = MiddleRight;
            this.BottomLeft = BottomLeft;
            this.BottomCenter = BottomCenter;
            this.BottomRight = BottomRight;
        }

        private MGEffectBinding GetBinding()
            => Binding ?? throw new InvalidOperationException($"A default-initialized {nameof(MGNineSliceFillBrush)} cannot be configured.");

        public IFillBrush Copy() => new MGNineSliceFillBrush(
            Binding?.Copy(),
            TargetMargin,
            TopLeft, TopCenter, TopRight,
            MiddleLeft, MiddleCenter, MiddleRight,
            BottomLeft, BottomCenter, BottomRight,
            InteriorBrush?.Copy());

        internal static Thickness GetEffectiveTargetMargin(MGElement Element, Thickness TargetMargin)
            => Element.EffectiveScaleSettings.ScaleThickness(TargetMargin, MGScaleCategory.Border);

        internal static NineSliceRegions GetDestinationRegions(MGElement Element, Rectangle Bounds, Thickness TargetMargin)
            => GetRegions(Bounds, GetEffectiveTargetMargin(Element, TargetMargin));

        internal static NineSliceRegions GetRegions(Rectangle Bounds, Thickness Margin)
        {
            int LeftColumnSize = Margin.Left;
            int RightColumnSize = Margin.Right;
            int CenterColumnSize = Bounds.Width - LeftColumnSize - RightColumnSize;

            int TopRowSize = Margin.Top;
            int BottomRowSize = Margin.Bottom;
            int CenterRowSize = Bounds.Height - TopRowSize - BottomRowSize;

            return new NineSliceRegions(
                new Rectangle(Bounds.Left, Bounds.Top, LeftColumnSize, TopRowSize),
                new Rectangle(Bounds.Left + LeftColumnSize, Bounds.Top, CenterColumnSize, TopRowSize),
                new Rectangle(Bounds.Left + LeftColumnSize + CenterColumnSize, Bounds.Top, RightColumnSize, TopRowSize),
                new Rectangle(Bounds.Left, Bounds.Top + TopRowSize, LeftColumnSize, CenterRowSize),
                new Rectangle(Bounds.Left + LeftColumnSize, Bounds.Top + TopRowSize, CenterColumnSize, CenterRowSize),
                new Rectangle(Bounds.Left + LeftColumnSize + CenterColumnSize, Bounds.Top + TopRowSize, RightColumnSize, CenterRowSize),
                new Rectangle(Bounds.Left, Bounds.Top + TopRowSize + CenterRowSize, LeftColumnSize, BottomRowSize),
                new Rectangle(Bounds.Left + LeftColumnSize, Bounds.Top + TopRowSize + CenterRowSize, CenterColumnSize, BottomRowSize),
                new Rectangle(Bounds.Left + LeftColumnSize + CenterColumnSize, Bounds.Top + TopRowSize + CenterRowSize, RightColumnSize, BottomRowSize));
        }

        public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
        {
            if (!IsNonEmpty(Bounds))
            {
                return;
            }

            DrawTransaction DT = DA.DT;
            NineSliceRegions Regions = GetDestinationRegions(Element, Bounds, TargetMargin);

            if (Effect == null)
            {
                DrawTextureBackedRegions(DT, Regions, DA.Offset, DA.Opacity);
            }
            else
            {
                MGStandardEffectParameterValues? StandardValues = UseStandardParameters
                    ? MGEffectBinding.CalculateStandardParameters(
                        DA.TS,
                        DT.CurrentSettings.Transform,
                        DT.GD.Viewport,
                        DT.GD.UseHalfPixelOffset,
                        DA.VisualState,
                        DA.Offset,
                        DA.Opacity,
                        Bounds)
                    : null;
                Binding.Apply(StandardValues, DA, Element, Bounds);

                MGNineSliceFillBrush Brush = this;
                DrawWithEffectTemporary(DT, Effect,
                    () => Brush.DrawTextureBackedRegionsWithEffect(DT, Regions, DA.Offset, DA.Opacity, Bounds));
            }

            if (InteriorBrush != null && IsNonEmpty(Regions.MiddleCenter))
            {
                InteriorBrush.Draw(DA, Element, Regions.MiddleCenter);
            }
        }

        private void DrawTextureBackedRegions(DrawTransaction DT, NineSliceRegions Regions, Point Offset, float Opacity)
        {
            TextureRegionDrawer Drawer = new(DT, Binding, Offset, Opacity, Rectangle.Empty, false);
            VisitTextureBackedRegions(Regions, ref Drawer);
        }

        private void DrawTextureBackedRegionsWithEffect(
            DrawTransaction DT,
            NineSliceRegions Regions,
            Point Offset,
            float Opacity,
            Rectangle CompleteDestination)
        {
            TextureRegionDrawer Drawer = new(DT, Binding, Offset, Opacity, CompleteDestination, UseStandardParameters);
            VisitTextureBackedRegions(Regions, ref Drawer);
        }

        internal IReadOnlyList<(MGTextureData Texture, Rectangle Destination)> GetTextureBackedRegions(NineSliceRegions Regions)
        {
            TextureRegionCollector Collector = new();
            VisitTextureBackedRegions(Regions, ref Collector);
            return Collector.Regions;
        }

        private void VisitTextureBackedRegions<TVisitor>(NineSliceRegions Regions, ref TVisitor Visitor)
            where TVisitor : struct, ITextureRegionVisitor
        {
            VisitIfNonEmpty(TopLeft, Regions.TopLeft, ref Visitor);
            VisitIfNonEmpty(TopCenter, Regions.TopCenter, ref Visitor);
            VisitIfNonEmpty(TopRight, Regions.TopRight, ref Visitor);
            VisitIfNonEmpty(MiddleLeft, Regions.MiddleLeft, ref Visitor);
            if (InteriorBrush == null)
            {
                VisitIfNonEmpty(MiddleCenter, Regions.MiddleCenter, ref Visitor);
            }
            VisitIfNonEmpty(MiddleRight, Regions.MiddleRight, ref Visitor);
            VisitIfNonEmpty(BottomLeft, Regions.BottomLeft, ref Visitor);
            VisitIfNonEmpty(BottomCenter, Regions.BottomCenter, ref Visitor);
            VisitIfNonEmpty(BottomRight, Regions.BottomRight, ref Visitor);
        }

        private static void VisitIfNonEmpty<TVisitor>(MGTextureData Texture, Rectangle Region, ref TVisitor Visitor)
            where TVisitor : struct, ITextureRegionVisitor
        {
            if (IsNonEmpty(Region) && (!Texture.SourceRect.HasValue || IsNonEmpty(Texture.SourceRect.Value)))
            {
                Visitor.Visit(Texture, Region);
            }
        }

        internal void ApplyEffectConfiguration(
            MGStandardEffectParameterValues? StandardValues,
            ElementDrawArgs DA,
            MGElement Element,
            Rectangle Bounds)
            => Binding?.Apply(StandardValues, DA, Element, Bounds);

        internal void ApplyElementTextureCoordinateMapping(MGElementTextureCoordinateMapping Mapping)
            => Binding?.ApplyElementTextureCoordinateMapping(Mapping);

        internal static MGElementTextureCoordinateMapping CalculateElementTextureCoordinateMapping(
            Rectangle Source,
            Point TextureSize,
            Rectangle SliceDestination,
            Rectangle CompleteDestination)
        {
            if (!IsNonEmpty(Source))
            {
                throw new ArgumentOutOfRangeException(nameof(Source), "The source rectangle must have positive dimensions.");
            }

            if (TextureSize.X <= 0 || TextureSize.Y <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(TextureSize), "The texture size must have positive dimensions.");
            }

            if (!IsNonEmpty(SliceDestination))
            {
                throw new ArgumentOutOfRangeException(nameof(SliceDestination), "The slice destination must have positive dimensions.");
            }

            if (!IsNonEmpty(CompleteDestination))
            {
                throw new ArgumentOutOfRangeException(nameof(CompleteDestination), "The complete destination must have positive dimensions.");
            }

            Vector2 SourceCoordinateStart = new(
                (float)Source.Left / TextureSize.X,
                (float)Source.Top / TextureSize.Y);
            Vector2 SourceCoordinateSize = new(
                (float)Source.Width / TextureSize.X,
                (float)Source.Height / TextureSize.Y);
            Vector2 ElementCoordinateStart = new(
                (float)(SliceDestination.Left - CompleteDestination.Left) / CompleteDestination.Width,
                (float)(SliceDestination.Top - CompleteDestination.Top) / CompleteDestination.Height);
            Vector2 ElementCoordinateSize = new(
                (float)SliceDestination.Width / CompleteDestination.Width,
                (float)SliceDestination.Height / CompleteDestination.Height);
            Vector2 Scale = ElementCoordinateSize / SourceCoordinateSize;
            Vector2 Offset = ElementCoordinateStart - SourceCoordinateStart * Scale;
            return new MGElementTextureCoordinateMapping(Scale, Offset);
        }

        internal static void DrawWithEffectTemporary(DrawTransaction DT, Effect Effect, Action Draw)
        {
            using (DT.SetDrawSettingsTemporary(DT.CurrentSettings with
            {
                Effect = Effect,
                Sort = SpriteSortMode.Immediate
            }))
            {
                Draw();
            }
        }

        private static bool IsNonEmpty(Rectangle Region) => Region.Width > 0 && Region.Height > 0;

        private interface ITextureRegionVisitor
        {
            void Visit(MGTextureData Texture, Rectangle Destination);
        }

        private readonly struct TextureRegionDrawer : ITextureRegionVisitor
        {
            private readonly DrawTransaction DT;
            private readonly MGEffectBinding Binding;
            private readonly Point Offset;
            private readonly float Opacity;
            private readonly Rectangle CompleteDestination;
            private readonly bool ApplyTextureCoordinateMapping;

            public TextureRegionDrawer(
                DrawTransaction DT,
                MGEffectBinding Binding,
                Point Offset,
                float Opacity,
                Rectangle CompleteDestination,
                bool ApplyTextureCoordinateMapping)
            {
                this.DT = DT;
                this.Binding = Binding;
                this.Offset = Offset;
                this.Opacity = Opacity;
                this.CompleteDestination = CompleteDestination;
                this.ApplyTextureCoordinateMapping = ApplyTextureCoordinateMapping;
            }

            public void Visit(MGTextureData Texture, Rectangle Destination)
            {
                if (ApplyTextureCoordinateMapping)
                {
                    Rectangle Source = Texture.SourceRect ?? Texture.Texture.Bounds;
                    MGElementTextureCoordinateMapping Mapping = CalculateElementTextureCoordinateMapping(
                        Source,
                        new Point(Texture.Texture.Width, Texture.Texture.Height),
                        Destination,
                        CompleteDestination);
                    Binding.ApplyElementTextureCoordinateMapping(Mapping);
                }

                Texture.Draw(DT, Destination.GetTranslated(Offset), null, Opacity);
            }
        }

        private struct TextureRegionCollector : ITextureRegionVisitor
        {
            public readonly List<(MGTextureData Texture, Rectangle Destination)> Regions;

            public TextureRegionCollector()
            {
                Regions = new();
            }

            public void Visit(MGTextureData Texture, Rectangle Destination)
                => Regions.Add((Texture, Destination));
        }

        internal readonly record struct NineSliceRegions(
            Rectangle TopLeft,
            Rectangle TopCenter,
            Rectangle TopRight,
            Rectangle MiddleLeft,
            Rectangle MiddleCenter,
            Rectangle MiddleRight,
            Rectangle BottomLeft,
            Rectangle BottomCenter,
            Rectangle BottomRight);
    }
}
