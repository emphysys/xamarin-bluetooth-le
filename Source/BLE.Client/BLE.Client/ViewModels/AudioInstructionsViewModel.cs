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

        public static AudioPlaybackLanguage PlaybackLanguage { get; set; }

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

        #endregion

        #endregion
         

        private readonly AudioPlayerFSM fsm;

        public AudioInstructionsViewModel(IAdapter adapter) : base(adapter)
        {
            CurrentAudioInstructionFile = AudioInstruction.test1; 

            AudioPlayerFSMBinder binder = new AudioPlayerFSMBinder
            {
                currentAudioInstructionGet = () => CurrentAudioInstructionFile,
                currentAudioInstructionSet = (i) => CurrentAudioInstructionFile = i,
                imageSourceGet = () => ImageButtonPlayImageSource,
                imageSourceSet = (s) => ImageButtonPlayImageSource = s
            };

            fsm = new AudioPlayerFSM(binder);
            fsm.StartAudioLoopThread();
        }

        //private AudioInstruction _tempCurrentInstruction;

        //private readonly ManualResetEventSlim playCV = new ManualResetEventSlim(false);

        //private int rewind = 0;

        //private void AudioLoopThreadEntry()
        //{  
        //    // Check token after each step and quick-quit if canceled
        //    while (true)
        //    {
        //        if (rewind > 0)
        //        {  
        //            InformBoardOfRewind_AudioLoopThread();
        //            rewind = 0;

        //            // Reset the token 
        //            audioLoopTokenSource = new CancellationTokenSource();
        //        }

        //        var token = audioLoopTokenSource.Token;
        //        AudioLoopIteration_AudioLoopThread(token);

        //        if (token.IsCancellationRequested && rewind == 0)
        //        {
        //            return;
        //        }
        //    }
        //}

        //private void InformBoardOfRewind_AudioLoopThread()
        //{
        //    // Do nothing for now
        //    Thread.Sleep(TimeSpan.FromMilliseconds(500));
        //    _tempCurrentInstruction -= rewind;

        //    if (_tempCurrentInstruction < 0)
        //    {
        //        _tempCurrentInstruction = 0;
        //    }
        //}

        //private void AudioLoopIteration_AudioLoopThread(CancellationToken token)
        //{
        //    var instruction = AwaitResponseFromBoard_AudioLoopThread(token); 

        //    if (token.IsCancellationRequested)
        //    {
        //        return;
        //    }

        //    playCV.Reset();
        //    PlayClip_AudioLoopThread(instruction, token);

        //    if (token.IsCancellationRequested)
        //    {
        //        return;
        //    }

        //    playCV.Reset();
        //    WaitForUserAcknowledgement_AudioLoopThread(token);
        //}

        //private AudioInstruction AwaitResponseFromBoard_AudioLoopThread(CancellationToken token)
        //{
        //    // Mock board behavior for now 
        //    Thread.Sleep(TimeSpan.FromSeconds(1));

        //    if (token.IsCancellationRequested) return default;

        //    AudioInstruction toReturn = _tempCurrentInstruction;
        //    ++_tempCurrentInstruction;
        //    Console.WriteLine("<<< ++");
        //    return toReturn;
        //}

        //private void PlayClip_AudioLoopThread(AudioInstruction instruction, CancellationToken token)
        //{
        //    ImageButtonPlayImageSource = IMAGESOURCE_STOP;

        //    audioPlayer.PlayAudioFile(instruction.GetClipFileName(), token, PlayClip_ClipFinished_AudioLoopThread);

        //    if (!token.IsCancellationRequested)
        //    {
        //        try
        //        {
        //            playCV.Wait(token);
        //        }
        //        catch (OperationCanceledException e)
        //        {
        //            // If the same token threw, then the task was canceled
        //            if (!e.CancellationToken.Equals(token))
        //            {
        //                throw e;
        //            }
        //        }
        //    }
        //}

        //private void PlayClip_ClipFinished_AudioLoopThread()
        //{
        //    ImageButtonPlayImageSource = IMAGESOURCE_NEXT;

        //    playCV.Set();
        //}

        //private void WaitForUserAcknowledgement_AudioLoopThread(CancellationToken token)
        //{
        //    if (!token.IsCancellationRequested)
        //    {
        //        try
        //        {
        //            playCV.Wait(token);
        //        }
        //        catch (OperationCanceledException e)
        //        {
        //            // If the same token threw, then the task was canceled
        //            if (!e.CancellationToken.Equals(token))
        //            {
        //                throw e;
        //            }
        //        }
        //    }
        //}

        //private void StopOrForwardButtonPressed()
        //{
        //    if (IsAudioPlaying)
        //    {
        //        PauseAudioFromUserInput();
        //    }
        //    else if (IsAudioPaused)
        //    {
        //        ResumePausedAudioFromUserInput();
        //    }
        //    else
        //    {
        //        MoveToNextClipFromUserInput();
        //    }
        //}

        //private void PauseAudioFromUserInput()
        //{
        //    audioPlayer.PauseAudio();
        //    ImageButtonPlayImageSource = IMAGESOURCE_PLAY;
        //}

        //private void ResumePausedAudioFromUserInput()
        //{
        //    audioPlayer.ResumePausedAudio();
        //    ImageButtonPlayImageSource = IMAGESOURCE_STOP;
        //}

        //private void MoveToNextClipFromUserInput()
        //{
        //    playCV.Set();
        //} 

        //private void PlayPreviousClip()
        //{
        //    ++rewind;
        //    audioLoopTokenSource.Cancel();
        //    playCV.Set();
        //    Console.WriteLine("<<< |<l");
        //}

        private async void SelectLanguage()
        {
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<SelectLanguageViewModel, MvxBundle>(new MvxBundle(new Dictionary<string, string>()));
        }

        public override void ViewDisappearing()
        {
            base.ViewDisappearing();

            fsm.StopAudioLoopThread();
        }

    }
}
