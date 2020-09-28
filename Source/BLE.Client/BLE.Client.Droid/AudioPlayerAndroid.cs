using Android.Media;
using BLE.Client;
using BLE.Client.Pages;
using System.Reflection;
using Xamarin.Forms;

/// <summary>
/// Based on https://stackoverflow.com/questions/34256176/how-to-play-sounds-on-xamarin-forms
/// </summary>
[assembly: Dependency(typeof(AudioPlayerAndroid))]
public class AudioPlayerAndroid : IAudioPlayer
{
    #region INTERFACE

    public void PlayAudioFile(string fileName)
    {
        var a = IntrospectionExtensions.GetTypeInfo(typeof(DeviceCommunicationPage)).Assembly;
        var ee = a.GetManifestResourceNames();
        var player = new MediaPlayer();

        using (var fd = Android.App.Application.Context.Assets.OpenFd(fileName))
        {
            player.Prepared += (s, e) => { player.Start(); };
            player.SetDataSource(fd.FileDescriptor, fd.StartOffset, fd.Length);
        }

        player.Prepare();
    }

    #endregion

}