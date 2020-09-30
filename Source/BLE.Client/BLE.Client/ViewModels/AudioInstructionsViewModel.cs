using MvvmCross.Commands;
using MvvmCross.ViewModels;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Xamarin.Forms;

namespace BLE.Client.ViewModels
{
    #region AUDIOFILES ENUM STUFF

    public enum AudioInstruction
    {
        test1,
        test2,
        test3,
        test4,
    }

    public static class AudioFilesExtensions
    {
        private static readonly AudioInstruction[] allFiles = (AudioInstruction[])Enum.GetValues(typeof(AudioInstruction));

        public static string GetClipTitle(this AudioInstruction file)
        {
            return $"Current File: {file}";
        }

        public static string GetClipFileName(this AudioInstruction file)
        {
            return $"{file}.ogg";
        }

        public static bool TryGetPreviousClip(this AudioInstruction file, out AudioInstruction maybePreviousFile)
        {
            if (((int)file) > 0)
            {
                maybePreviousFile = allFiles[(int)file - 1];
                return true;
            }
            else
            {
                maybePreviousFile = file;
                return false;
            }
        }

        public static bool TryGetNextClip(this AudioInstruction file, out AudioInstruction maybeNextFile)
        {
            if (((int)file) < allFiles.Length - 2)
            {
                maybeNextFile = allFiles[(int)file + 1];
                return true;
            }
            else
            {
                maybeNextFile = file;
                return false;
            }
        }

        public static AudioInstruction GetPreviousClipOrCurrent(this AudioInstruction file)
        {
            return allFiles[Math.Max(0, (int)file - 1)];
        }

        public static AudioInstruction GetNextClipOrCurrent(this AudioInstruction file)
        {
            return allFiles[Math.Min(allFiles.Length - 1, (int)file + 1)];
        }
    }

    #endregion

    public class AudioInstructionsViewModel : BaseViewModel
    {
        private const string IMAGESOURCE_PLAY = "audioplayer_play.png";
        private const string IMAGESOURCE_STOP = "audioplayer_stop.png";

        #region BINDINGS

        #region COMMANDS

        public MvxCommand AudioPreviousCommand => new MvxCommand(AudioPrevious);
        public MvxCommand AudioPlayCommand => new MvxCommand(AudioPlay);
        public MvxCommand AudioNextCommand => new MvxCommand(AudioNext);

        #endregion

        #region PROPERTIES

        public string AudioClipTitle
        {
            get => CurrentAudioInstructionFile.GetClipTitle();
        }

        private string _AudioClipFileName;

        public string AudioClipFileName
        {
            get => _AudioClipFileName;
            set
            {
                _AudioClipFileName = value;
                RaisePropertyChanged();
            }
        }

        public string _ImageButtonPlayImageSource = IMAGESOURCE_PLAY;
        public string ImageButtonPlayImageSource
        {
            get => _ImageButtonPlayImageSource;
            set
            {
                _ImageButtonPlayImageSource = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #endregion

        private AudioInstruction _CurrentAudioFile;
        private AudioInstruction CurrentAudioInstructionFile
        {
            get => _CurrentAudioFile;
            set
            {
                _CurrentAudioFile = value;
                RaisePropertyChanged(nameof(AudioClipTitle));
            }
        }

        private bool IsAudioPlaying { get => audioPlayer.IsAudioPlaying; }

        private readonly IAudioPlayer audioPlayer;

        public AudioInstructionsViewModel(IAdapter adapter) : base(adapter)
        {
            CurrentAudioInstructionFile = AudioInstruction.test1;
            audioPlayer = DependencyService.Get<IAudioPlayer>();
        }

        private void AudioPrevious()
        {
            audioPlayer.StopAudio(); 

            CurrentAudioInstructionFile = CurrentAudioInstructionFile.GetPreviousClipOrCurrent();
        }

        private void AudioPlay()
        {
            if (IsAudioPlaying)
            {
                ImageButtonPlayImageSource = IMAGESOURCE_PLAY;
                audioPlayer.StopAudio(); 
            }
            else
            {
                ImageButtonPlayImageSource = IMAGESOURCE_STOP;
                audioPlayer.PlayAudioFile(CurrentAudioInstructionFile.GetClipFileName(), () => ImageButtonPlayImageSource = IMAGESOURCE_PLAY); 
            }
        }

        private void AudioNext()
        {
            audioPlayer.StopAudio(); 

            CurrentAudioInstructionFile = CurrentAudioInstructionFile.GetNextClipOrCurrent();
        }
    }
}
