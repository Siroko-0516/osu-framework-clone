// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Browser;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Input.Handlers;
using osu.Framework.Logging;
using osu.Framework.Threading;

namespace osu.Framework.Platform
{
    /// <summary>
    /// Initial browser host used while the WebGL renderer and browser input stack are ported.
    /// </summary>
    /// <remarks>
    /// This intentionally avoids SDL and all desktop shell APIs. The dummy renderer makes it
    /// possible to bring up and validate the original game dependency graph in WebAssembly
    /// before rendering is connected to an HTML canvas.
    /// </remarks>
    public class BrowserGameHost : GameHost
    {
        private BrowserRenderer? browserRenderer;

        public BrowserGameHost(string gameName, HostOptions? options = null)
            : base(gameName, options)
        {
        }

        protected override bool RequireWindowExists => false;

        protected override bool UsesExternalMainLoop => true;

        public override bool CanExit => false;

        public override IEnumerable<string> UserStoragePaths => new[] { "/osu-data" };

        public override bool OpenFileExternally(string filename)
        {
            Logger.Log($"Browser host cannot open local file \"{filename}\" externally yet.");
            return false;
        }

        public override bool PresentFileExternally(string filename)
        {
            Logger.Log($"Browser host cannot present local file \"{filename}\" externally yet.");
            return false;
        }

        public override void OpenUrlExternally(string url) =>
            Logger.Log($"Browser host requested URL \"{url}\".");

        public override Storage GetStorage(string path) => new NativeStorage(path, this);

        protected override IWindow CreateWindow(GraphicsSurfaceType preferredSurface) => null!;

        protected override Clipboard CreateClipboard() => new HeadlessClipboard();

        protected override IEnumerable<InputHandler> CreateAvailableInputHandlers() =>
            Array.Empty<InputHandler>();

        protected override void ChooseAndSetupRenderer()
        {
            SetupRendererAndWindow(browserRenderer = new BrowserRenderer(), GraphicsSurfaceType.OpenGL);

            // A browser has a WebGL surface owned by JavaScript rather than an IWindow. The
            // normal setup path returns early for windowless hosts, so complete the renderer's
            // common initialisation here (default batches, disposal queues and frame state).
            ((IRenderer)browserRenderer).Initialise(new BrowserGraphicsSurface());
        }

        /// <summary>
        /// Returns the latest backbuffer state produced by the original framework scene graph.
        /// </summary>
        public float[] GetBrowserFrameState()
        {
            var colour = browserRenderer?.BackbufferClearColour ?? osuTK.Graphics.Color4.Black;
            var viewport = browserRenderer?.BrowserViewport ?? default;

            float[] header = new[]
            {
                colour.R, colour.G, colour.B, colour.A,
                viewport.X, viewport.Y, viewport.Width, viewport.Height
            };

            return header.Concat(browserRenderer?.FrameVertices ?? Array.Empty<float>()).ToArray();
        }

        protected override void SetupConfig(IDictionary<FrameworkSetting, object> defaultOverrides)
        {
            defaultOverrides[FrameworkSetting.AudioDevice] = "No sound";
            defaultOverrides[FrameworkSetting.ExecutionMode] = ExecutionMode.SingleThread;
            base.SetupConfig(defaultOverrides);
        }

        /// <summary>
        /// Advances the original framework input, update, audio and draw threads by one frame.
        /// The browser requestAnimationFrame callback is responsible for invoking this method.
        /// </summary>
        /// <returns>Whether the host accepted the frame.</returns>
        public bool PumpFrame()
        {
            if (ExecutionState != ExecutionState.Running)
                return false;

            RunMainLoopFrame();
            return true;
        }

        protected override void DrawFrame()
        {
            base.DrawFrame();
        }
    }
}
