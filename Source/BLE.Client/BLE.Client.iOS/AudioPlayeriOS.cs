using System.IO;
using System.Threading;
using AVFoundation;
using BLE.Client.iOS;
using Foundation;
using Xamarin.Forms; 

[assembly: Dependency(typeof(AudioPlayeriOS))]
namespace BLE.Client.iOS
{

    public class AudioPlayeriOS : IAudioPlayer
    {
        private AVAudioPlayer player;

        #region INTERFACE
          
        public void StopAudio()
        {
            player?.Stop();
        }

        public void PlayAudioFile(string fileName, CancellationToken token, AudioFinishedDelegate audioFinishedFunc = null)
        {
            var filePath = NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(fileName), Path.GetExtension(fileName));
            var url = NSUrl.FromString(filePath);

            player = AVAudioPlayer.FromUrl(url);
            player.FinishedPlaying += (a, b) => { player = null; audioFinishedFunc(); };

            token.Register(StopAudio);

            if (!token.IsCancellationRequested)
            {
                player.Play();
            }
        }

        public void PauseAudio()
        {
            player?.Pause();
        }

        public void ResumePausedAudio()
        {
            player?.Play();
        }

        public bool IsAudioPlaying => player?.Playing == true;

        public bool IsAudioPaused => player?.CurrentTime > 0;
        #endregion
    }
}