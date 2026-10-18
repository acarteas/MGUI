using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    public partial class MGDesktop
    {
        private ulong CacheUpdateToken;
        private ulong AttemptedPreparationToken;
        private ulong PreparedToken;
        private Viewport PreparationViewport;
        internal Viewport RenderCacheViewport => IsPreparingRenderCaches ? PreparationViewport : Renderer.GraphicsDevice.Viewport;
        internal Point RenderCacheBackBufferSize => new(Renderer.GraphicsDevice.PresentationParameters.BackBufferWidth,
            Renderer.GraphicsDevice.PresentationParameters.BackBufferHeight);
        internal bool IsPreparingRenderCaches { get; private set; }
        internal bool CanUseRenderCaches => !IsPreparingRenderCaches && CacheUpdateToken != 0
            && PreparedToken == CacheUpdateToken && !RenderCaches.IsDisposed;
        private void CompleteRenderCacheUpdate()
        {
            if (CacheUpdateToken == ulong.MaxValue)
            {
                CacheUpdateToken = 0;
                RenderCaches.InvalidateGeneration();
            }
            CacheUpdateToken++;
            PreparedToken = 0;
        }

        /// <summary>
        /// Optionally prepares caches after a completed Update and BEFORE clearing or drawing the host scene.
        /// Call outside ALL SpriteBatch, PrimitiveBatch and DrawTransaction sessions. MGUI transactions are detected;
        /// raw external batches cannot be detected through MonoGame public APIs. Bound host targets are unsupported
        /// and are left untouched. Rebinding DiscardContents targets cannot preserve their pixels.
        /// Each Update permits one attempt; missing preparation always selects direct painting.
        /// </summary>
        /// <remarks>Public device state is restored. MonoGame exposes no getter for the bound shader/effect program;
        /// hosts must apply their own effect pass when starting subsequent scene rendering, as with normal SpriteBatch.
        /// This method neither flushes nor changes a caller transaction.</remarks>
        public void PrepareRenderCaches()
        {
            if (IsPreparingRenderCaches || RenderCaches.IsDisposed)
            {
                return;
            }
            ReconcileRenderRootAttachments();
            if (CacheUpdateToken == 0 || AttemptedPreparationToken == CacheUpdateToken)
            {
                return;
            }
            AttemptedPreparationToken = CacheUpdateToken;
            GraphicsDevice device = Renderer.GraphicsDevice;
            if (DrawTransaction.HasActiveTransactions(device) || device.GetRenderTargets().Length != 0)
            {
                RenderCaches.RecordBypass(MGRenderCacheBypassReason.UnsupportedCallContext);
                return;
            }
            ulong frame = CacheUpdateToken;
            PreparationViewport = device.Viewport;
            IsPreparingRenderCaches = true;
            try
            {
                using var preparation = RenderCaches.BeginPreparation();
                if (preparation == null)
                {
                    return;
                }
                while (RenderCaches.TryDequeue(out var request))
                {
                    if (!Resolve(request, out object owner, out MGRenderCacheOperation operation))
                    {
                        continue;
                    }
                    if (!RenderCaches.TryAllocate(request, out var candidate))
                    {
                        continue;
                    }
                    using (candidate)
                    {
                        bool painted = false;
                        Exception bindFailure = null;
                        var state = new CacheDeviceState(device);
                        try
                        {
                            if (!Resolve(request, out owner, out operation) || frame != CacheUpdateToken)
                            {
                                continue;
                            }
                            try
                            {
                                device.SetRenderTarget(candidate.Target);
                                RenderCaches.RecordTargetSwitch();
                                device.Clear(Color.Transparent);
                            }
                            catch (Exception error)
                            {
                                bindFailure = error;
                            }
                            if (bindFailure == null && Resolve(request, out owner, out operation))
                            {
                                MGRenderCacheKey key = request.Key;
                                Point origin = key.Bounds.Location + key.Offset
                                    + new Point((int)key.Transform.M41, (int)key.Transform.M42);
                                Matrix transform = key.Transform * Matrix.CreateTranslation(-origin.X, -origin.Y, 0);
                                DrawSettings settings = new(transform);

                                // PrimitiveBatch.Begin does not apply DrawSettings, so a primitive-first capture
                                // must not inherit the host's blend, culling, depth or sampling state.
                                device.BlendState = settings.BlendState;
                                device.RasterizerState = settings.RasterizerState;
                                device.DepthStencilState = settings.DepthStencilState;
                                device.SamplerStates[0] = settings.SamplerState;
                                using (DrawTransaction transaction = new(Renderer, settings, true))
                                {
                                    transaction.ConfigureCacheCapture(origin);
                                    operation.Capture(owner, key, transaction);
                                }
                                painted = true;
                            }
                        }
                        finally
                        {
                            try
                            {
                                if (device.GetRenderTargets().Length != 0)
                                {
                                    device.SetRenderTarget(null);
                                    RenderCaches.RecordTargetSwitch();
                                }
                            }
                            finally
                            {
                                state.Restore(device);
                            }
                        }
                        if (bindFailure != null)
                        {
                            candidate.RejectNativeFailure(bindFailure);
                        }
                        else if (painted && frame == CacheUpdateToken && Resolve(request, out _, out _))
                        {
                            candidate.Publish();
                        }
                    }
                }
                if (frame == CacheUpdateToken && !RenderCaches.IsDisposed)
                {
                    PreparedToken = frame;
                }
            }
            finally
            {
                IsPreparingRenderCaches = false;
            }
        }
        private bool Resolve(MGRenderCacheRequest request, out object owner, out MGRenderCacheOperation operation)
        {
            operation = null;
            owner = null;
            return RenderCaches.IsCurrent(request) && request.Registration.TryGetIdentities(out owner, out var identity)
                && (operation = identity as MGRenderCacheOperation) != null && operation.Validate(owner, request.Key);
        }

        // Snapshot only publicly readable/restorable state. Raw shader programs have no public getter.
        private sealed class CacheDeviceState
        {
            private readonly Viewport Viewport;
            private readonly Rectangle Scissor;
            private readonly RasterizerState Rasterizer;
            private readonly BlendState Blend;
            private readonly DepthStencilState Depth;
            private readonly Color BlendFactor;
            private readonly IndexBuffer Indices;
            private readonly SamplerState[] Samplers, VertexSamplers;
            private readonly Texture[] Textures, VertexTextures;
            internal CacheDeviceState(GraphicsDevice device)
            {
                Viewport = device.Viewport; Scissor = device.ScissorRectangle;
                Rasterizer = device.RasterizerState; Blend = device.BlendState; Depth = device.DepthStencilState;
                BlendFactor = device.BlendFactor;
                Indices = device.Indices;
                Samplers = ReadSlots(index => device.SamplerStates[index]);
                VertexSamplers = ReadSlots(index => device.VertexSamplerStates[index]);
                Textures = ReadSlots(index => device.Textures[index]);
                VertexTextures = ReadSlots(index => device.VertexTextures[index]);
            }
            private static T[] ReadSlots<T>(Func<int, T> read)
            {
                List<T> values = new();
                for (int index = 0; ; index++)
                {
                    try { values.Add(read(index)); }
                    catch (IndexOutOfRangeException) { return values.ToArray(); }
                }
            }
            internal void Restore(GraphicsDevice device)
            {
                device.Viewport = Viewport; device.ScissorRectangle = Scissor;
                device.RasterizerState = Rasterizer; device.BlendState = Blend; device.DepthStencilState = Depth;
                device.BlendFactor = BlendFactor;
                device.Indices = Indices;
                for (int i = 0; i < Samplers.Length; i++) { device.SamplerStates[i] = Samplers[i]; }
                for (int i = 0; i < VertexSamplers.Length; i++) { device.VertexSamplerStates[i] = VertexSamplers[i]; }
                for (int i = 0; i < Textures.Length; i++) { device.Textures[i] = Textures[i]; }
                for (int i = 0; i < VertexTextures.Length; i++) { device.VertexTextures[i] = VertexTextures[i]; }
            }
        }
    }
}
