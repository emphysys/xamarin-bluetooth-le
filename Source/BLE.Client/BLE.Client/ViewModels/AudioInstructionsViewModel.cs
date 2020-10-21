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
            return file.ToString();
        }

        public static string GetClipFileName(this AudioInstruction file)
        {
            return $"{file}_{AudioInstructionsViewModel.PlaybackLanguage.GetAudioFileSuffix()}.wav";
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


        public static string GetAudioFileSuffix(this AudioPlaybackLanguage language)
        {
            return language.ToString().ToLower();
        }
    }

    #endregion

    public class AudioInstructionsViewModel : BaseViewModel
    {
        private const string IMAGESOURCE_PLAY = "audioplayer_play.png";
        private const string IMAGESOURCE_STOP = "audioplayer_stop.png";

        public static AudioPlaybackLanguage PlaybackLanguage { get; set; }

        #region BINDINGS

        #region COMMANDS

        public MvxCommand AudioPreviousCommand => new MvxCommand(AudioPrevious);
        public MvxCommand AudioPlayCommand => new MvxCommand(MoveToNextClipFromUserInput);
        public MvxCommand AudioNextCommand => new MvxCommand(AudioNext);
        public MvxCommand SelectLanguageCommand => new MvxCommand(SelectLanguage);

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

        /// <summary>
        /// For later use, should the board end up directing audio playback
        /// </summary>
        private IDevice BoardBluetoothDevice => MainMenuViewModel.BoardBluetoothDevice;

        private readonly CancellationTokenSource audioLoopTokenSource;
        private readonly Thread audioLoopThread;

        public AudioInstructionsViewModel(IAdapter adapter) : base(adapter)
        {
            CurrentAudioInstructionFile = AudioInstruction.test1;
            audioPlayer = DependencyService.Get<IAudioPlayer>();

            audioLoopTokenSource = new CancellationTokenSource();
            audioLoopThread = new Thread(AudioLoopThreadEntry);
            audioLoopThread.Start(audioLoopTokenSource.Token);
        }



        private readonly Stack<AudioInstruction> previousInstructions = new Stack<AudioInstruction>();

        private AudioInstruction _tempCurrentInstruction;
         
        private readonly ManualResetEventSlim playCV = new ManualResetEventSlim(false);

        private void AudioLoopThreadEntry(object tokenObject)
        {
            var token = (CancellationToken)tokenObject; 

            // Check token after each step and quick-quit if canceled
            while (!token.IsCancellationRequested)
            {
                var instruction = AwaitResponseFromBoard_AudioLoopThread(token);

                if (token.IsCancellationRequested) return;

                playCV.Reset();
                PlayClip_AudioLoopThread(instruction, token);

                if (token.IsCancellationRequested) return;

                playCV.Reset();
                WaitForUserAcknowledgement_AudioLoopThread(token);
            }
        }

        private AudioInstruction AwaitResponseFromBoard_AudioLoopThread(CancellationToken token)
        {
            // Mock board behavior for now 
            if (token.IsCancellationRequested) return default;

            try
            { 
                Task.Delay(TimeSpan.FromSeconds(1), token); 
            }
            catch (TaskCanceledException e)
            {
                // If the same token threw, then the task was canceled
                if (!e.CancellationToken.Equals(token))
                {
                    throw e;
                } 
            }

            AudioInstruction toReturn = _tempCurrentInstruction;
            _tempCurrentInstruction = (AudioInstruction)((int)_tempCurrentInstruction + 1);
            return toReturn;
        }

        private void PlayClip_AudioLoopThread(AudioInstruction instruction, CancellationToken token)
        {
            ImageButtonPlayImageSource = IMAGESOURCE_STOP;
            audioPlayer.PlayAudioFile(instruction.GetClipFileName(), token, PlayClip_ClipFinished_AudioLoopThread);
             
            if (!token.IsCancellationRequested)
            {
                try
                {
                    playCV.Wait(token);
                }
                catch (OperationCanceledException e)
                {
                    // If the same token threw, then the task was canceled
                    if (!e.CancellationToken.Equals(token))
                    {
                        throw e;
                    }
                }
            }
        }

        private void PlayClip_ClipFinished_AudioLoopThread()
        {
            ImageButtonPlayImageSource = IMAGESOURCE_PLAY;
             
            playCV.Set(); 
        }

        private void WaitForUserAcknowledgement_AudioLoopThread(CancellationToken token)
        { 
            if (!token.IsCancellationRequested)
            {
                try
                {
                    playCV.Wait(token);
                }
                catch (OperationCanceledException e)
                {
                    // If the same token threw, then the task was canceled
                    if (!e.CancellationToken.Equals(token))
                    {
                        throw e;
                    }
                }
            }
        }

        private void MoveToNextClipFromUserInput()
        { 
            playCV.Set(); 
        }

        public override void ViewDisappearing()
        {
            base.ViewDisappearing();

            audioLoopTokenSource.Cancel();
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
                //audioPlayer.PlayAudioFile(CurrentAudioInstructionFile.GetClipFileName(), () => ImageButtonPlayImageSource = IMAGESOURCE_PLAY); 
            }
        }

        private void AudioNext()
        {
            audioPlayer.StopAudio(); 

            CurrentAudioInstructionFile = CurrentAudioInstructionFile.GetNextClipOrCurrent();
        }

        private async void SelectLanguage()
        {
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<SelectLanguageViewModel, MvxBundle>(new MvxBundle(new Dictionary<string, string>()));
        }
          
    }


}
