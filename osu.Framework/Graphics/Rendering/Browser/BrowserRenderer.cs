// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
        private readonly List<float> frameVertices = new List<float>();
        private readonly Queue<BrowserTextureUpload> textureUploads = new Queue<BrowserTextureUpload>();
        private int nextTextureId;
        private int currentTextureId;

        public Color4 BackbufferClearColour { get; private set; } = Color4.Black;

        public RectangleI BrowserViewport { get; private set; }

        public float[] FrameVertices => frameVertices.ToArray();

        public BrowserTextureUpload[] TakeTextureUploads()
        {
            BrowserTextureUpload[] uploads = textureUploads.ToArray();
            textureUploads.Clear();
            return uploads;
        }

        protected internal override void BeginFrame(Vector2 windowSize)
        {
            frameVertices.Clear();
            base.BeginFrame(windowSize);
        }

        protected override IVertexBatch<TVertex> CreateLinearBatch<TVertex>(int size, int maxBuffers, PrimitiveTopology topology)
            => new BrowserVertexBatch<TVertex>(this, size);

        protected override IVertexBatch<TVertex> CreateQuadBatch<TVertex>(int size, int maxBuffers)
            => new BrowserVertexBatch<TVertex>(this, size);

        internal void CaptureVertex<TVertex>(TVertex vertex)
            where TVertex : unmanaged, IEquatable<TVertex>, IVertex
        {
            if (vertex is not TexturedVertex2D textured || frameVertices.Count >= 240000)
                return;

            frameVertices.Add(textured.Position.X);
            frameVertices.Add(textured.Position.Y);
            frameVertices.Add(textured.Colour.R);
            frameVertices.Add(textured.Colour.G);
            frameVertices.Add(textured.Colour.B);
            frameVertices.Add(textured.Colour.A);
            frameVertices.Add(textured.TexturePosition.X);
            frameVertices.Add(textured.TexturePosition.Y);
            frameVertices.Add(currentTextureId);
        }

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
        private int count;

        public BrowserVertexBatch(BrowserRenderer renderer, int size)
        {
            this.renderer = renderer;
            Size = size;
            AddAction = Add;
        }

        public int Size { get; }

        public Action<TVertex> AddAction { get; }

        public void Add(TVertex vertex)
        {
            renderer.SetActiveBatch(this);
            renderer.CaptureVertex(vertex);
            count++;
        }

        public int Draw()
        {
            int drawn = count;
            count = 0;
            return drawn;
        }

        void IVertexBatch.ResetCounters() => count = 0;

        public void Dispose()
        {
        }
    }
}
