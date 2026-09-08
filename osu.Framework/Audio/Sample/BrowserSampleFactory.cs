// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio.Mixing;
using osu.Framework.Bindables;

namespace osu.Framework.Audio.Sample
{
    /// <summary>Owns a decoded browser sample shared by independently adjustable playbacks.</summary>
    public abstract class BrowserSampleFactory : AudioCollectionManager<AdjustableAudioComponent>
    {
        public static Func<byte[], string, AudioMixer, BrowserSampleFactory>? CreateFactory { get; set; }
        public string Name { get; }
        public Bindable<int> PlaybackConcurrency { get; } = new Bindable<int>(Sample.DEFAULT_CONCURRENCY);
        public abstract double Length { get; }

        protected BrowserSampleFactory(string name) => Name = name;

        public Sample CreateSample()
        {
            var sample = CreateSampleCore();
            sample.OnPlay = AddItem;
            return sample;
        }

        protected abstract Sample CreateSampleCore();
    }
}
