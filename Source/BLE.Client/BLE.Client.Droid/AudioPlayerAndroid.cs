using Android.Media;
using BLE.Client;
using Xamarin.Forms;

/// <summary>
/// Based on https://stackoverflow.com/questions/34256176/how-to-play-sounds-on-xamarin-forms
/// </summary>
[assembly: Dependency(typeof(AudioPlayerAndroid))]
public class AudioPlayerAndroid : IAudioPlayer
{
    private MediaPlayer player;
    private AudioFinishedDelegate previousAudioFinishedFunc;

    #region INTERFACE

    public void PlayAudioFile(string fileName, AudioFinishedDelegate audioFinishedFunc = null)
    {
        player = new MediaPlayer();
        player.Prepared += (s, e) => player.Start();
        player.Completion += (s, e) => { audioFinishedFunc?.Invoke(); };

        previousAudioFinishedFunc = audioFinishedFunc;

        var fd = Android.App.Application.Context.Assets.OpenFd(fileName); 
        player.SetDataSource(fd.FileDescriptor, fd.StartOffset, fd.Length); 

        player.PrepareAsync();
    }

    public void StopAudio()
    {
        if (player.IsPlaying)
        {
            player.Stop();

            previousAudioFinishedFunc?.Invoke();
        }
    }

    public bool IsAudioPlaying { get => player?.IsPlaying == true; }

    #endregion

}