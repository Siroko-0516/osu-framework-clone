// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using ManagedBass;

namespace osu.Framework.Audio.Mixing
{
    /// <summary>
    /// A no-output mixer used while the browser Web Audio backend is being implemented.
    /// </summary>
    internal sealed class BrowserAudioMixer : AudioMixer
    {
        public BrowserAudioMixer(AudioMixer? fallbackMixer, string identifier)
            : base(fallbackMixer, identifier)
        {
        }

        public override void AddEffect(IEffectParameter effect, int priority = 0)
        {
        }

        public override void RemoveEffect(IEffectParameter effect)
        {
        }

        public override void UpdateEffect(IEffectParameter effect)
        {
        }

        protected override void AddInternal(IAudioChannel channel)
        {
        }

        protected override void RemoveInternal(IAudioChannel channel)
        {
        }
    }
}
