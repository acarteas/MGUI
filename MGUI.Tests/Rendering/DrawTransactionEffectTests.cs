using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MGUI.Tests.Rendering
{
    public class DrawTransactionEffectTests
    {
        [Fact]
        public void SetEffect_UpdatesCurrentSettingsEffect()
        {
            DrawTransaction transaction = CreateTransaction(DrawSettings.Default);
            Effect effect = CreateEffect();

            transaction.SetEffect(effect);

            Assert.Same(effect, transaction.CurrentSettings.Effect);
        }

        [Fact]
        public void SetEffect_WithNull_ClearsCurrentSettingsEffect()
        {
            Effect effect = CreateEffect();
            DrawTransaction transaction = CreateTransaction(DrawSettings.Default with { Effect = effect });

            transaction.SetEffect(null);

            Assert.Null(transaction.CurrentSettings.Effect);
        }

        [Fact]
        public void SetEffectTemporary_RestoresPreviousEffectOnDispose()
        {
            Effect previousEffect = CreateEffect();
            Effect temporaryEffect = CreateEffect();
            DrawTransaction transaction = CreateTransaction(DrawSettings.Default with { Effect = previousEffect });

            using (transaction.SetEffectTemporary(temporaryEffect))
            {
                Assert.Same(temporaryEffect, transaction.CurrentSettings.Effect);
            }

            Assert.Same(previousEffect, transaction.CurrentSettings.Effect);
        }

        [Fact]
        public void SetEffectTemporary_NestedScopesRestoreEffectsInLifoOrder()
        {
            Effect firstEffect = CreateEffect();
            Effect secondEffect = CreateEffect();
            Effect thirdEffect = CreateEffect();
            DrawTransaction transaction = CreateTransaction(DrawSettings.Default with { Effect = firstEffect });

            using (transaction.SetEffectTemporary(secondEffect))
            {
                Assert.Same(secondEffect, transaction.CurrentSettings.Effect);

                using (transaction.SetEffectTemporary(thirdEffect))
                {
                    Assert.Same(thirdEffect, transaction.CurrentSettings.Effect);
                }

                Assert.Same(secondEffect, transaction.CurrentSettings.Effect);
            }

            Assert.Same(firstEffect, transaction.CurrentSettings.Effect);
        }

        [Fact]
        public void SetEffectTemporary_WithNull_RestoresToNoEffect()
        {
            Effect temporaryEffect = CreateEffect();
            DrawTransaction transaction = CreateTransaction(DrawSettings.Default);

            using (transaction.SetEffectTemporary(temporaryEffect))
            {
                Assert.Same(temporaryEffect, transaction.CurrentSettings.Effect);
            }

            Assert.Null(transaction.CurrentSettings.Effect);
        }

        private static DrawTransaction CreateTransaction(DrawSettings settings)
        {
            DrawTransaction transaction = (DrawTransaction)RuntimeHelpers.GetUninitializedObject(typeof(DrawTransaction));
            SetAutoProperty(transaction, nameof(DrawTransaction.CurrentSettings), settings);
            return transaction;
        }

        private static Effect CreateEffect()
        {
            return (Effect)RuntimeHelpers.GetUninitializedObject(typeof(Effect));
        }

        private static void SetAutoProperty<TValue>(DrawTransaction transaction, string propertyName, TValue value)
        {
            FieldInfo field = typeof(DrawTransaction).GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Could not find backing field for {propertyName}.");

            field.SetValue(transaction, value);
        }
    }
}
