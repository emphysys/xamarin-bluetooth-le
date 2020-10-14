using System.IO;
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
         
        public void PlayAudioFile(string fileName, AudioFinishedDelegate audioFinishedFunc = null)
        {
            var filePath = NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(fileName), Path.GetExtension(fileName));
            var url = NSUrl.FromString(filePath);

            player = AVAudioPlayer.FromUrl(url);
            player.FinishedPlaying += (a, b) => { player = null; audioFinishedFunc(); };
            player.Play();
        }

        public void StopAudio()
        {
            player?.Stop();
        }

        public bool IsAudioPlaying => player?.Playing == true;
        #endregion
    }
}