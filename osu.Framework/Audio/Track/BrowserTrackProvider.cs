// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;

namespace osu.Framework.Audio.Track
{
    /// <summary>Browser host's audio implementation. The returned track owns the stream.</summary>
    public static class BrowserTrackProvider
    {
        public static Func<Stream, string, Track>? CreateTrack { get; set; }
    }
}
