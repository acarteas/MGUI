using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;

namespace MGUI.Tests.Rendering
{
    public class DrawSettingsTests
    {
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
