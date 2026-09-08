// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Rendering.Vertices;
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

        public Color4 BackbufferClearColour { get; private set; } = Color4.Black;

        public RectangleI BrowserViewport { get; private set; }

        public float[] FrameVertices => frameVertices.ToArray();

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
        }

        protected override void ClearImplementation(ClearInfo clearInfo)
        {
            BackbufferClearColour = clearInfo.Colour;
        }

        protected override void SetViewportImplementation(RectangleI viewport)
        {
            BrowserViewport = viewport;
        }
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
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering.Dummy;
using osuTK.Graphics;

namespace osu.Framework.Graphics.Rendering.Browser
{
    /// <summary>
    /// First stage of the browser renderer. Captures framework backbuffer state so the web
    /// host can apply it to WebGL while native batches, textures and shaders are ported.
    /// </summary>
    public sealed class BrowserRenderer : DummyRenderer
    {
        public Color4 BackbufferClearColour { get; private set; } = Color4.Black;

        public RectangleI BrowserViewport { get; private set; }

        protected override void ClearImplementation(ClearInfo clearInfo)
        {
            BackbufferClearColour = clearInfo.Colour;
        }

        protected override void SetViewportImplementation(RectangleI viewport)
        {
            BrowserViewport = viewport;
        }
    }
}
