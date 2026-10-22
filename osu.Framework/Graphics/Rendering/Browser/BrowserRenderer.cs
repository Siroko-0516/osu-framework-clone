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
        public const int QuadStride = 76;
        private const int max_frame_floats = QuadStride * 6000;
        private int pathSequence;
        private readonly float[] frameVertices = new float[max_frame_floats];
        private int frameVertexCount;
        private byte[] frameState = new byte[8 * sizeof(float)];
        private readonly Queue<BrowserTextureUpload> textureUploads = new Queue<BrowserTextureUpload>();
        private int nextTextureId;
        private int currentTextureId;

        public bool CaptureEnabled { get; set; } = true;

        public Color4 BackbufferClearColour { get; private set; } = Color4.Black;

        public RectangleI BrowserViewport { get; private set; }

        public byte[] CreateFrameState()
        {
            int requiredBytes = (8 + frameVertexCount) * sizeof(float);

            // JS copies the live portion before this call completes, so the managed
            // transport buffer can be reused. Growing geometrically avoids allocating
            // and collecting a large byte array on every gameplay frame.
            if (frameState.Length < requiredBytes)
            {
                int capacity = frameState.Length;
                while (capacity < requiredBytes)
                    capacity = Math.Min((8 + max_frame_floats) * sizeof(float), capacity * 2);

                frameState = new byte[capacity];
            }

            if (frameState.Length > 65536 && requiredBytes < frameState.Length / 4)
                frameState = new byte[Math.Max(65536, requiredBytes * 2)];

            Span<float> values = MemoryMarshal.Cast<byte, float>(frameState);
            values[0] = BackbufferClearColour.R;
            values[1] = BackbufferClearColour.G;
            values[2] = BackbufferClearColour.B;
            values[3] = BackbufferClearColour.A;
            values[4] = frameVertexCount;
            values[5] = 1;
            values[6] = BrowserViewport.Width;
            values[7] = BrowserViewport.Height;
            frameVertices.AsSpan(0, frameVertexCount).CopyTo(values[8..]);
            return frameState;
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
            pathSequence = 0;
            base.BeginFrame(windowSize);
        }

        protected override IVertexBatch<TVertex> CreateLinearBatch<TVertex>(int size, int maxBuffers, PrimitiveTopology topology)
            => new BrowserVertexBatch<TVertex>(this, size, false, topology);

        protected override IVertexBatch<TVertex> CreateQuadBatch<TVertex>(int size, int maxBuffers)
            => new BrowserVertexBatch<TVertex>(this, size, true);

        internal void CaptureQuad(ReadOnlySpan<TexturedVertex2D> vertices, int textureId, int pathId = 0, float capsuleLength = 0, Vector4 gradientRect = default)
        {
            if (!CaptureEnabled || vertices.Length != 4 || frameVertexCount + QuadStride > max_frame_floats)
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
            var m = CurrentMaskingInfo;
            var b = CurrentBlendingParameters;
            var r = m.MaskingRect;
            var c = m.BorderColour.TopLeft.Linear;
            var t = m.ToMaskingSpace;
            var scissor = ScissorState ? Scissor : BrowserViewport;
            ReadOnlySpan<float> metadata = stackalloc float[] { scissor.X, scissor.Y, scissor.Width, scissor.Height,
                t.Row0.X, t.Row1.X, t.Row2.X, t.Row0.Y, t.Row1.Y, t.Row2.Y,
                r.X, r.Y, r.Width, r.Height, m.CornerRadius, m.BorderThickness,
                c.R, c.G, c.B, c.A, IsMaskingActive ? 1 : 0, pathId, capsuleLength,
                gradientRect.X, gradientRect.Y, gradientRect.Z, gradientRect.W,
                (int)b.Source, (int)b.Destination, (int)b.SourceAlpha, (int)b.DestinationAlpha,
                (int)b.RGBEquation, (int)b.AlphaEquation, m.Hollow ? 1 : 0,
                m.CornerExponent, m.BlendRange, m.AlphaExponent, m.HollowCornerRadius, m.EdgeOffset.X, m.EdgeOffset.Y };
            metadata.CopyTo(frameVertices.AsSpan(frameVertexCount));
            frameVertexCount += metadata.Length;
        }

        public void CapturePath(IReadOnlyList<Line> segments, float radius, Matrix3 matrix, Texture texture, Color4 colour)
        {
            if (radius <= 0 || segments.Count == 0 || !texture.Bind()) return;
            int id = ++pathSequence;
            int textureId = currentTextureId;
            var rect = texture.GetTextureRect();
            var gradient = new Vector4(rect.X, rect.Y, rect.Width, rect.Height);
            Span<TexturedVertex2D> vertices = stackalloc TexturedVertex2D[4];
            foreach (var line in segments)
            {
                Vector2 delta = line.EndPoint - line.StartPoint;
                float length = delta.Length;
                Vector2 direction = length > 0.0001f ? delta / length : Vector2.UnitX;
                Vector2 normal = new Vector2(-direction.Y, direction.X);
                for (int i = 0; i < 4; i++)
                {
                    float x = i == 0 || i == 3 ? -1 : length / radius + 1;
                    float y = i < 2 ? -1 : 1;
                    Vector2 p = line.StartPoint + radius * (direction * x + normal * y);
                    vertices[i] = new TexturedVertex2D(this)
                    {
                        Position = new Vector2(p.X * matrix.Row0.X + p.Y * matrix.Row1.X + matrix.Row2.X,
                            p.X * matrix.Row0.Y + p.Y * matrix.Row1.Y + matrix.Row2.Y),
                        Colour = colour,
                        TexturePosition = new Vector2(x, y),
                    };
                }
                CaptureQuad(vertices, textureId, id, length / radius, gradient);
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
        private readonly PrimitiveTopology topology;
        private readonly TexturedVertex2D[] quad = new TexturedVertex2D[4];
        private int quadCount;
        private int quadTextureId;
        private int count;

        public BrowserVertexBatch(BrowserRenderer renderer, int size, bool cullQuads, PrimitiveTopology topology = PrimitiveTopology.Triangles)
        {
            this.renderer = renderer;
            this.cullQuads = cullQuads;
            this.topology = topology;
            Size = size;
            AddAction = Add;
        }

        public int Size { get; }

        public Action<TVertex> AddAction { get; }

        public void Add(TVertex vertex)
        {
            renderer.SetActiveBatch(this);
            if (vertex is TexturedVertex2D textured && (cullQuads || topology == PrimitiveTopology.Triangles || topology == PrimitiveTopology.TriangleStrip))
            {
                if (quadCount == 0)
                    quadTextureId = renderer.CurrentTextureId;

                quad[quadCount++] = textured;
                if (quadCount == (cullQuads ? 4 : 3))
                {
                    if (!cullQuads) quad[3] = quad[2];
                    renderer.CaptureQuad(quad, quadTextureId);
                    if (!cullQuads && topology == PrimitiveTopology.TriangleStrip)
                    {
                        quad[0] = quad[1]; quad[1] = quad[2]; quadCount = 2;
                    }
                    else quadCount = 0;
                }
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
