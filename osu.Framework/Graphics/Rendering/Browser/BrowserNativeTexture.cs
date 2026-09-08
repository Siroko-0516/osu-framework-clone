// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using osu.Framework.Graphics.Textures;

namespace osu.Framework.Graphics.Rendering.Browser
{
    internal sealed class BrowserNativeTexture : INativeTexture
    {
        private readonly BrowserRenderer renderer;
        private readonly Queue<ITextureUpload> uploadQueue = new Queue<ITextureUpload>();

        public int TextureId { get; }
        public IRenderer Renderer => renderer;
        public string Identifier => TextureId.ToString();
        public int MaxSize => 4096;
        public int Width { get; set; }
        public int Height { get; set; }
        public int? MipLevel { get; set; }
        public bool Available { get; private set; } = true;
        public bool BypassTextureUploadQueueing { get; set; }
        public bool UploadComplete => uploadQueue.Count == 0;
        ulong INativeTexture.TotalBindCount { get; set; }

        public BrowserNativeTexture(BrowserRenderer renderer, int textureId, int width, int height)
        {
            this.renderer = renderer;
            TextureId = textureId;
            Width = width;
            Height = height;
        }

        public void SetData(ITextureUpload upload)
        {
            bool enqueue = uploadQueue.Count == 0;
            uploadQueue.Enqueue(upload);

            if (enqueue && !BypassTextureUploadQueueing)
                renderer.EnqueueTextureUpload(this);
        }

        public bool Upload()
        {
            bool uploaded = false;

            while (uploadQueue.TryDequeue(out ITextureUpload? upload))
            {
                using (upload)
                {
                    renderer.QueueTextureUpload(new BrowserTextureUpload
                    {
                        TextureId = TextureId,
                        TextureWidth = Width,
                        TextureHeight = Height,
                        X = upload.Bounds.X,
                        Y = upload.Bounds.Y,
                        Width = upload.Bounds.Width,
                        Height = upload.Bounds.Height,
                        Data = MemoryMarshal.AsBytes(upload.Data).ToArray(),
                    });
                    uploaded = true;
                }
            }

            return uploaded;
        }

        public void FlushUploads()
        {
            while (uploadQueue.TryDequeue(out ITextureUpload? upload))
                upload.Dispose();
        }

        public int GetByteSize() => Width * Height * 4;

        public void Dispose()
        {
            FlushUploads();
            Available = false;
        }
    }
}
