using BLE.Client.ViewModels;
using System;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Xamarin.Forms;

namespace BLE.Client
{ 
    public enum AudioInstruction
    {
        p01_calm_911,
        p02_chest_cut_clothing,
        p03_foil_package,
        p04_remove_pads
    }

    public struct AudioPlayerFSMBinder
    {
        public Func<AudioInstruction> currentAudioInstructionGet;
        public Action<AudioInstruction> currentAudioInstructionSet;

        public Func<string> imageSourceGet;
        public Action<string> imageSourceSet;

        public Func<bool> isFastForwardEnabledGet;
        public Action<bool> isFastForwardEnabledSet;
    }

    public static class AudioInstructionExtensions
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

    public class AudioPlayerFSM
    {
        #region ENUMS

        private enum State
        {
            Entry,
            AwaitingBoardInput,
            PlayingClip,
            ParsingUserInput,
            Exit
        }

        internal enum AudioCompleteCycleOption
        { 
            ReplayCurrent,
            GoToPrevious,
            GoToNext
        }

        #endregion

        #region AUDIO LOOP THREAD VARIABLES

        private readonly Thread audioLoopThread;

        private readonly CancellationTokenSource audioLoopTokenSource;

        #endregion

        #region VIEWMODEL-BOUND PROPERTIES

        private readonly AudioPlayerFSMBinder viewmodelBinder;

        private AudioInstruction CurrentAudioInstruction
        {
            get => viewmodelBinder.currentAudioInstructionGet();
            set => viewmodelBinder.currentAudioInstructionSet(value);
        }

        #endregion

        private State currentState;

        private AudioClipFSM clipFSM = null;

        
        public AudioPlayerFSM(AudioPlayerFSMBinder bindingFuncs)
        {
            viewmodelBinder = bindingFuncs; 
            CurrentAudioInstruction = AudioInstruction.p01_calm_911;
            currentState = State.Entry;

            audioLoopTokenSource = new CancellationTokenSource();
            audioLoopThread = new Thread(AudioLoopThread_Entry)
            {
                Name = "Audio Loop Thread"
            };
        }

        public void StartAudioLoopThread()
        {
            audioLoopThread.Start();
        }

        public void StopAudioLoopThread()
        { 
            audioLoopTokenSource?.Cancel(); 
        }

        private void AudioLoopThread_Entry()
        {
            while (true)
            {
                var token = audioLoopTokenSource.Token;
                AudioLoopThread_CycleFSM(token);

                if (token.IsCancellationRequested || currentState == State.Exit)
                {
                    break;
                }
            }
        }

        private void AudioLoopThread_CycleFSM(CancellationToken token)
        {
            switch (currentState)
            {
                case State.Entry:
                    // Anything to do here? 
                    currentState = State.AwaitingBoardInput;
                    break;
                case State.AwaitingBoardInput:
                    CurrentAudioInstruction = AudioLoopThread_AwaitBoardInput(token);
                    currentState = State.PlayingClip;
                    break;
                case State.PlayingClip:
                    var cycleOption = AudioLoopThread_PlayAudio(token);
                    CurrentAudioInstruction = AudioLoopThread_ParseCycleOption(cycleOption, CurrentAudioInstruction);
                    currentState = State.AwaitingBoardInput;
                    break;
                case State.Exit:
                    break;
                default:
                    throw new NotImplementedException($"Unimplemented state: {currentState}!");
            }
        }

        private AudioInstruction AudioLoopThread_AwaitBoardInput(CancellationToken token)
        {
            // Mock board behavior for now
            Thread.Sleep(TimeSpan.FromSeconds(1));

            if (token.IsCancellationRequested) return default;

            return CurrentAudioInstruction;
        }

        private AudioCompleteCycleOption AudioLoopThread_PlayAudio(CancellationToken token)
        {
            clipFSM = new AudioClipFSM(CurrentAudioInstruction, viewmodelBinder);
            var option = clipFSM.PlayAudio(token);

            clipFSM = null;
            return option;
        }

        private AudioInstruction AudioLoopThread_ParseCycleOption(AudioCompleteCycleOption option, AudioInstruction instruction)
        {
            switch (option)
            {
                case AudioCompleteCycleOption.ReplayCurrent:
                    return instruction;
                case AudioCompleteCycleOption.GoToPrevious:
                    return instruction.GetPreviousClipOrCurrent();
                case AudioCompleteCycleOption.GoToNext:
                    return instruction.GetNextClipOrCurrent();
                default:
                    throw new NotImplementedException($"Cycle option not implemented: {option}!");
            }
        }

        #region CLIPFSM FORWARDS

        public void UserPressedBacktrack() => clipFSM?.UserPressedBacktrack();
        public void UserPressedRewind() => clipFSM?.UserPressedRewind();
        public void UserPressedPlayPause() => clipFSM?.UserPressedPlayPause();
        public void UserPressedFastForward() => clipFSM?.UserPressedFastForward();

        #endregion
    }

    class AudioClipFSM
    {
        private enum State
        {
            Entry,
            AudioPlaying,
            AudioPaused,
            AudioComplete
        }

        private enum UserInput
        {
            ButtonRewind, 
            ButtonBacktrack,
            ButtonPlayPause,
            ButtonFastForward
        }
         
        private readonly IAudioPlayer audioPlayer;

        private State currentState;

        private readonly ManualResetEventSlim inputCV;

        private UserInput lastUserInput;

        private readonly object lastUserInputLock = new object();

        private readonly AudioInstruction instruction;

        private readonly AudioPlayerFSMBinder viewmodelBinder; 

        private string ImageButtonPlaySource
        { 
            set => viewmodelBinder.imageSourceSet(value);
        }

        public bool IsFastForwardEnabled
        {
            get => viewmodelBinder.isFastForwardEnabledGet();
            set => viewmodelBinder.isFastForwardEnabledSet(value);
        }


        internal AudioClipFSM(AudioInstruction instruction, AudioPlayerFSMBinder binder)
        {
            this.instruction = instruction;
            viewmodelBinder = binder;

            audioPlayer = DependencyService.Get<IAudioPlayer>();
            currentState = State.Entry;
            inputCV = new ManualResetEventSlim(false);

            IsFastForwardEnabled = false;
        }

        internal AudioPlayerFSM.AudioCompleteCycleOption PlayAudio(CancellationToken token)
        {
            if (token.IsCancellationRequested) return default;

            AudioPlayerFSM.AudioCompleteCycleOption option;
            while (true)
            {
                inputCV.Reset(); 

                if (CycleFSM(token, out option))
                {
                    break;
                }
            }

            return option;
        }

        private bool CycleFSM(CancellationToken token, out AudioPlayerFSM.AudioCompleteCycleOption maybeOption)
        { 
            maybeOption = default;

            if (token.IsCancellationRequested) return true;

            switch (currentState)
            {
                case State.Entry:
                    // Start audio and cycle to AudioPlaying
                    StartAudio(token);
                    currentState = State.AudioPlaying;
                    return false;
                case State.AudioPlaying:
                    ImageButtonPlaySource = AudioInstructionsViewModel.IMAGESOURCE_STOP;
                    // Wait for something to cycle out of this state
                    if (AudioStartedAwaitChange(token, out maybeOption))
                    {
                        return true;
                    }
                    else
                    {
                        currentState = State.AudioPaused;
                        audioPlayer.PauseAudio();

                        return false;
                    }
                case State.AudioPaused:
                    // Wait for the user to press a button 
                    ImageButtonPlaySource = AudioInstructionsViewModel.IMAGESOURCE_PLAY;
                    if (AudioStartedAwaitChange(token, out maybeOption))
                    {
                        return true;
                    }
                    else
                    {
                        currentState = State.AudioPlaying;
                        audioPlayer.ResumePausedAudio();

                        return false;
                    } 
                case State.AudioComplete:
                    // Direct cycle to next clip
                    maybeOption = AudioPlayerFSM.AudioCompleteCycleOption.GoToNext;
                    return true;
                default:
                    throw new NotImplementedException($"Unimplemented state: {currentState}!");
            }
        }

        private void StartAudio(CancellationToken token)
        {
            // When the audio finishes, inject a mock fast-forward request to notify the condition variable
            audioPlayer.PlayAudioFile(instruction.GetClipFileName(), token, AudioClipEnded);
        }

        private void AudioClipEnded()
        {
            ImageButtonPlaySource = AudioInstructionsViewModel.IMAGESOURCE_PLAY;
            IsFastForwardEnabled = true;
        }

        private bool AudioStartedAwaitChange(CancellationToken token, out AudioPlayerFSM.AudioCompleteCycleOption maybeOption)
        {
            maybeOption = default;

            // Await input
            var input = AwaitUserInput(token);

            if (token.IsCancellationRequested) return true;

            switch (input)
            {
                case UserInput.ButtonRewind:
                    // Restart the current track
                    maybeOption = AudioPlayerFSM.AudioCompleteCycleOption.ReplayCurrent;
                    return true;
                case UserInput.ButtonBacktrack:
                    // Play the previous track
                    maybeOption = AudioPlayerFSM.AudioCompleteCycleOption.GoToPrevious;
                    return true;
                case UserInput.ButtonPlayPause:
                    // Do not cycle!
                    return false;
                case UserInput.ButtonFastForward:
                    // Play the next track
                    maybeOption = AudioPlayerFSM.AudioCompleteCycleOption.GoToNext;
                    return true;
                default:
                    throw new NotImplementedException($"Unimplemented user input: {input}!");
            }
        } 

        private UserInput AwaitUserInput(CancellationToken token)
        {
            // Await the CV, then copy the input (while locked) and return it
            try
            {
                inputCV.Wait(token);
            }
            catch (OperationCanceledException e)
            {
                if (e.CancellationToken.Equals(token))
                {
                    return default;
                }
            }

            UserInput inputCopy; 
            lock(lastUserInputLock)
            {
                inputCopy = lastUserInput;
            }

            return inputCopy;
        }


        private void HandleUserInput(UserInput input)
        {
            lock (lastUserInputLock)
            {
                lastUserInput = input;
            } 

            if (input != UserInput.ButtonPlayPause)
            {
                audioPlayer.StopAudio();
            }

            inputCV.Set();
        }

        internal void UserPressedBacktrack() => HandleUserInput(UserInput.ButtonBacktrack);

        internal void UserPressedRewind() => HandleUserInput(UserInput.ButtonRewind);

        internal void UserPressedPlayPause() => HandleUserInput(UserInput.ButtonPlayPause);

        internal void UserPressedFastForward() => HandleUserInput(UserInput.ButtonFastForward);

    }
}
