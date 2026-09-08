// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Drawing;

namespace osu.Framework.Platform
{
    /// <summary>
    /// Describes the WebGL surface owned by the browser host.
    /// </summary>
    internal sealed class BrowserGraphicsSurface : IGraphicsSurface, IOpenGLGraphicsSurface
    {
        public IntPtr WindowHandle => IntPtr.Zero;

        public GraphicsSurfaceType Type => GraphicsSurfaceType.OpenGL;

        public bool VerticalSync { get; set; } = true;

        public IntPtr WindowContext => IntPtr.Zero;

        public IntPtr CurrentContext => IntPtr.Zero;

        public int? BackbufferFramebuffer => null;

        public void Initialise()
        {
        }

        public Size GetDrawableSize() => Size.Empty;

        public void SwapBuffers()
        {
        }

        public void CreateContext()
        {
        }

        public void MakeCurrent(IntPtr context)
        {
        }

        public void ClearCurrent()
        {
        }

        public void DeleteContext(IntPtr context)
        {
        }

        public IntPtr GetProcAddress(string symbol) => IntPtr.Zero;
    }
}
