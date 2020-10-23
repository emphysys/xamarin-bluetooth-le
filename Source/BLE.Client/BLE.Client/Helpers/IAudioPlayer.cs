using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BLE.Client
{
    public delegate void AudioFinishedDelegate();

    /// <summary>
    /// Based on https://stackoverflow.com/questions/34256176/how-to-play-sounds-on-xamarin-forms
    /// </summary>
    public interface IAudioPlayer
    {
        /// <summary>
        /// Plays the audio file at the specified path.
        /// </summary>
        /// <param name="fileName">The file to play.</param>
        /// <param name="token">The token upon which to cancel.</param>
        /// <param name="audioFinishedFunc">An optional function to call when audio is finished playing.</param>
        void PlayAudioFile(string fileName, CancellationToken token, AudioFinishedDelegate audioFinishedFunc = null);

        void PauseAudio();

        void ResumePausedAudio();

        void StopAudio();

        bool IsAudioPlaying { get; }

        bool IsAudioPaused { get; }
    }
}
