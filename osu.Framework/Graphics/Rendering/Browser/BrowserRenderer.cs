// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;

namespace osu.Framework.Graphics.Rendering.Browser
{
    /// <summary>
    /// First stage of the browser renderer. Captures framework backbuffer state so the web
    /// host can apply it to WebGL while native batches, textures and shaders are ported.
    /// </summary>
    public sealed class BrowserRenderer : DummyRenderer
    {
        private const int max_frame_floats = 240000;
        private readonly float[] frameVertices = new float[max_frame_floats];
        private int frameVertexCount;
        private readonly Queue<BrowserTextureUpload> textureUploads = new Queue<BrowserTextureUpload>();
        private int nextTextureId;
        private int currentTextureId;

        public bool CaptureEnabled { get; set; } = true;

        public Color4 BackbufferClearColour { get; private set; } = Color4.Black;

        public RectangleI BrowserViewport { get; private set; }

        public byte[] CreateFrameState()
        {
            byte[] state = new byte[(8 + frameVertexCount) * sizeof(float)];
            Span<float> values = MemoryMarshal.Cast<byte, float>(state);
            values[0] = BackbufferClearColour.R;
            values[1] = BackbufferClearColour.G;
            values[2] = BackbufferClearColour.B;
            values[3] = BackbufferClearColour.A;
            values[4] = BrowserViewport.X;
            values[5] = BrowserViewport.Y;
            values[6] = BrowserViewport.Width;
            values[7] = BrowserViewport.Height;
            frameVertices.AsSpan(0, frameVertexCount).CopyTo(values[8..]);
            return state;
        }

        public BrowserTextureUpload[] TakeTextureUploads()
        {
            BrowserTextureUpload[] uploads = textureUploads.ToArray();
            textureUploads.Clear();
            return uploads;
        }

        protected internal override void BeginFrame(Vector2 windowSize)
        {
            frameVertexCount = 0;
            base.BeginFrame(windowSize);
        }

        protected override IVertexBatch<TVertex> CreateLinearBatch<TVertex>(int size, int maxBuffers, PrimitiveTopology topology)
            => new BrowserVertexBatch<TVertex>(this, size, false);

        protected override IVertexBatch<TVertex> CreateQuadBatch<TVertex>(int size, int maxBuffers)
            => new BrowserVertexBatch<TVertex>(this, size, true);

        internal void CaptureVertex<TVertex>(TVertex vertex)
            where TVertex : unmanaged, IEquatable<TVertex>, IVertex
        {
            if (!CaptureEnabled || vertex is not TexturedVertex2D textured || frameVertexCount + 9 > max_frame_floats)
                return;

            appendVertex(textured, currentTextureId);
        }

        internal void CaptureQuad(ReadOnlySpan<TexturedVertex2D> vertices, int textureId)
        {
            if (!CaptureEnabled || vertices.Length != 4 || frameVertexCount + 36 > max_frame_floats)
                return;

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxAlpha = 0;

            foreach (TexturedVertex2D vertex in vertices)
            {
                minX = Math.Min(minX, vertex.Position.X);
                minY = Math.Min(minY, vertex.Position.Y);
                maxX = Math.Max(maxX, vertex.Position.X);
                maxY = Math.Max(maxY, vertex.Position.Y);
                maxAlpha = Math.Max(maxAlpha, vertex.Colour.A);
            }

            // Keep gameplay and judgement data intact; only omit geometry which
            // cannot contribute a pixel to the current browser backbuffer.
            if (maxAlpha <= 0.001f
                || maxX < BrowserViewport.X
                || maxY < BrowserViewport.Y
                || minX > BrowserViewport.X + BrowserViewport.Width
                || minY > BrowserViewport.Y + BrowserViewport.Height)
                return;

            foreach (TexturedVertex2D vertex in vertices)
            {
                appendVertex(vertex, textureId);
            }
        }

        private void appendVertex(TexturedVertex2D vertex, int textureId)
        {
            frameVertices[frameVertexCount++] = vertex.Position.X;
            frameVertices[frameVertexCount++] = vertex.Position.Y;
            frameVertices[frameVertexCount++] = vertex.Colour.R;
            frameVertices[frameVertexCount++] = vertex.Colour.G;
            frameVertices[frameVertexCount++] = vertex.Colour.B;
            frameVertices[frameVertexCount++] = vertex.Colour.A;
            frameVertices[frameVertexCount++] = vertex.TexturePosition.X;
            frameVertices[frameVertexCount++] = vertex.TexturePosition.Y;
            frameVertices[frameVertexCount++] = textureId;
        }

        internal int CurrentTextureId => currentTextureId;

        protected override INativeTexture CreateNativeTexture(int width, int height, bool manualMipmaps = false, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,
                                                              Color4? initialisationColour = null)
            => new BrowserNativeTexture(this, ++nextTextureId, width, height);

        protected override bool SetTextureImplementation(INativeTexture? texture, int unit)
        {
            if (unit == 0)
                currentTextureId = (texture as BrowserNativeTexture)?.TextureId ?? 0;

            return true;
        }

        internal void QueueTextureUpload(BrowserTextureUpload upload) => textureUploads.Enqueue(upload);

        internal void QueueTextureDeletion(int textureId) => textureUploads.Enqueue(new BrowserTextureUpload
        {
            TextureId = textureId,
            Deleted = true,
        });

        protected override void ClearImplementation(ClearInfo clearInfo)
        {
            BackbufferClearColour = clearInfo.Colour;
        }

        protected override void SetViewportImplementation(RectangleI viewport)
        {
            BrowserViewport = viewport;
        }
    }

    public sealed class BrowserTextureUpload
    {
        public int TextureId { get; init; }
        public bool Deleted { get; init; }
        public int TextureWidth { get; init; }
        public int TextureHeight { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }

    internal sealed class BrowserVertexBatch<TVertex> : IVertexBatch<TVertex>
        where TVertex : unmanaged, IEquatable<TVertex>, IVertex
    {
        private readonly BrowserRenderer renderer;
        private readonly bool cullQuads;
        private readonly TexturedVertex2D[] quad = new TexturedVertex2D[4];
        private int quadCount;
        private int quadTextureId;
        private int count;

        public BrowserVertexBatch(BrowserRenderer renderer, int size, bool cullQuads)
        {
            this.renderer = renderer;
            this.cullQuads = cullQuads;
            Size = size;
            AddAction = Add;
        }

        public int Size { get; }

        public Action<TVertex> AddAction { get; }

        public void Add(TVertex vertex)
        {
            renderer.SetActiveBatch(this);
            if (cullQuads && vertex is TexturedVertex2D textured)
            {
                if (quadCount == 0)
                    quadTextureId = renderer.CurrentTextureId;

                quad[quadCount++] = textured;
                if (quadCount == quad.Length)
                {
                    renderer.CaptureQuad(quad, quadTextureId);
                    quadCount = 0;
                }
            }
            else
            {
                renderer.CaptureVertex(vertex);
            }
            count++;
        }

        public int Draw()
        {
            int drawn = count;
            count = 0;
            quadCount = 0;
            return drawn;
        }

        void IVertexBatch.ResetCounters()
        {
            count = 0;
            quadCount = 0;
        }

        public void Dispose()
        {
        }
    }
}
