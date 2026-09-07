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
