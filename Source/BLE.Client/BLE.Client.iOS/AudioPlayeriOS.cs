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
        #region INTERFACE

        public void PlayAudioFile(string fileName)
        {
            var filePath = NSBundle.MainBundle.PathForResource(Path.GetFileNameWithoutExtension(fileName), Path.GetExtension(fileName));
            var url = NSUrl.FromString(filePath);

            var player = AVAudioPlayer.FromUrl(url);
            player.FinishedPlaying += (a, b) => { player = null; };
            player.Play();
        } 

        #endregion
    }
}