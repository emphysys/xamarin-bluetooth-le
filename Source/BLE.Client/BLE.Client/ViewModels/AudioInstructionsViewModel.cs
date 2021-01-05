using MvvmCross;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace BLE.Client.ViewModels
{ 
    public class AudioInstructionsViewModel : BaseViewModel
    {
        public static string IMAGESOURCE_PLAY = "audioplayer_play.png";
        public static string IMAGESOURCE_STOP = "audioplayer_stop.png";

        public static AudioPlaybackLanguage PlaybackLanguage => SelectLanguageViewModel.SelectedLanguage;

        #region BINDINGS

        #region COMMANDS

        public MvxCommand AudioBacktrackCommand => new MvxCommand(AudioBacktrack);
        public MvxCommand AudioRewindCommand => new MvxCommand(AudioRewind);
        public MvxCommand AudioPlayPauseCommand => new MvxCommand(AudioPlayPause);
        public MvxCommand AudioFastForwardCommand => new MvxCommand(AudioFastForward);

        public MvxCommand SelectLanguageCommand => new MvxCommand(SelectLanguage);

        private void AudioBacktrack()
        {
            fsm.UserPressedBacktrack();
        }

        private void AudioRewind()
        {
            fsm.UserPressedRewind();
        }

        private void AudioPlayPause()
        {
            fsm.UserPressedPlayPause();
        }

        private void AudioFastForward()
        {
            fsm.UserPressedFastForward();
        }

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

        private bool _IsFastForwardEnabled;
        public bool IsFastForwardEnabled
        {
            get => _IsFastForwardEnabled;
            set
            {
                _IsFastForwardEnabled = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #endregion
         

        private readonly AudioPlayerFSM fsm;

        public AudioInstructionsViewModel(IAdapter adapter) : base(adapter)
        {
            CurrentAudioInstructionFile = AudioInstruction.p01_calm_911;

            fsm = InitAndBeginAudioPlayerFSM();
        }

        private AudioPlayerFSM InitAndBeginAudioPlayerFSM()
        {
            AudioPlayerFSMBinder binder = new AudioPlayerFSMBinder
            {
                currentAudioInstructionGet = () => CurrentAudioInstructionFile,
                currentAudioInstructionSet = (i) => CurrentAudioInstructionFile = i,
                imageSourceGet = () => ImageButtonPlayImageSource,
                imageSourceSet = (s) => ImageButtonPlayImageSource = s,
                isFastForwardEnabledGet = () => IsFastForwardEnabled,
                isFastForwardEnabledSet = (e) => IsFastForwardEnabled = e
            };

            var fsm = new AudioPlayerFSM(binder);
            fsm.StartAudioLoopThread();

            return fsm;
        }

        private async void SelectLanguage()
        {
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<SelectLanguageViewModel, MvxBundle>(new MvxBundle(new Dictionary<string, string>()));
        }

        public override void ViewAppeared()
        {
            base.ViewAppeared();

            fsm?.Restart();
        }

        public override void ViewDisappearing()
        {
            base.ViewDisappearing();

            fsm?.StopAudioLoopThread();
        }

    }
}
