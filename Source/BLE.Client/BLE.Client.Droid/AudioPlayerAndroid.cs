using Android.Media;
using BLE.Client;
using System.Threading;
using System.Threading.Tasks;
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

    public void PlayAudioFile(string fileName, CancellationToken token, AudioFinishedDelegate audioFinishedFunc = null)
    {
        player = new MediaPlayer();
        player.Prepared += (s, e) => player.Start(); 
        player.Completion += (s, e) => { audioFinishedFunc?.Invoke(); };

        previousAudioFinishedFunc = audioFinishedFunc;

        var fd = Android.App.Application.Context.Assets.OpenFd(fileName); 
        player.SetDataSource(fd.FileDescriptor, fd.StartOffset, fd.Length);
         
        token.Register(StopAudio);

        if (!token.IsCancellationRequested)
        {
            player.PrepareAsync();
        }
    } 

    public void StopAudio()
    {
        if (player?.IsPlaying == true)
        {
            player.Stop();

            previousAudioFinishedFunc?.Invoke();
        }
    }

    public void PauseAudio()
    {
        if (player?.IsPlaying == true)
        {
            player.Pause();
            IsAudioPaused = true;
        }
    }

    public void ResumePausedAudio()
    {
        if (player != null && IsAudioPaused)
        {
            player.Start();
            IsAudioPaused = false;
        }
    }

    public bool IsAudioPlaying { get => player?.IsPlaying == true; }

    public bool IsAudioPaused { get; private set; }

    #endregion

}