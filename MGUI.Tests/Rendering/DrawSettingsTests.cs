using MGUI.Shared.Rendering;
using MGUI.Core.UI;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;

namespace MGUI.Tests.Rendering
{
    public class DrawSettingsTests
    {
        [Fact]
        public void OpacityHelpersPreserveInheritedRasterizerContextWithoutGrowingX64Arguments()
        {
            DrawTransaction transaction = (DrawTransaction)RuntimeHelpers.GetUninitializedObject(typeof(DrawTransaction));
            typeof(DrawTransaction).GetField("<CurrentSettings>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(transaction, DrawSettings.Default with { RasterizerType = RasterizerType.Solid });
            ElementDrawArgs inherited = new ElementDrawArgs(new(TimeSpan.FromSeconds(1), transaction, 1), default, new(2, 3))
                .ObserveInheritedRasterizer();
            Assert.True(inherited.HasUnsupportedInheritedRasterizer);
            Assert.True((inherited with { BA = inherited.BA.SetOpacity(0.5f) }).HasUnsupportedInheritedRasterizer);
            Assert.True((inherited with { BA = inherited.BA.MultiplyOpacity(0.5f) }).HasUnsupportedInheritedRasterizer);
            Assert.True(inherited.SetOpacity(0.5f).AsZeroOffset().HasUnsupportedInheritedRasterizer);
            Assert.Equal(0.5f, inherited.BA.SetOpacity(0.5f).Opacity);
            Assert.Equal(0.5f, inherited.BA.MultiplyOpacity(0.5f).Opacity);
            var (time, originalTransaction, opacity) = inherited.BA;
            Assert.Equal(TimeSpan.FromSeconds(1), time);
            Assert.Same(transaction, originalTransaction);
            Assert.Equal(1, opacity);

            if (IntPtr.Size == 8)
            {
                Assert.Equal(24, Unsafe.SizeOf<DrawBaseArgs>());
                Assert.Equal(40, Unsafe.SizeOf<ElementDrawArgs>());
            }
        }

        [Fact]
        public void Default_HasNoEffect()
        {
            DrawSettings settings = DrawSettings.Default;

            Assert.Null(settings.Effect);
        }

        [Fact]
        public void Constructor_PreservesExistingPositionalArgumentsWithNullEffect()
        {
            DrawSettings settings = new(Matrix.Identity, RasterizerType.Solid, SpriteSortMode.Immediate,
                BlendType.Opaque, SamplerType.LinearClamp, DepthStencilType.DepthRead);

            Assert.Equal(Matrix.Identity, settings.Transform);
            Assert.Equal(RasterizerType.Solid, settings.RasterizerType);
            Assert.Equal(SpriteSortMode.Immediate, settings.Sort);
            Assert.Equal(BlendType.Opaque, settings.BlendType);
            Assert.Equal(SamplerType.LinearClamp, settings.SamplerType);
            Assert.Equal(DepthStencilType.DepthRead, settings.DepthStencilType);
            Assert.Null(settings.Effect);
        }

        [Fact]
        public void WithExpression_CanSetEffect()
        {
            Effect effect = (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));

            DrawSettings settings = DrawSettings.Default with { Effect = effect };

            Assert.Same(effect, settings.Effect);
        }
    }
}
