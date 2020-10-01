using MvvmCross;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace BLE.Client.ViewModels
{
    public class MainMenuViewModel : BaseViewModel
    {
        #region BINDINGS

        #region COMMANDS

        public IMvxCommand AudioInstructionsCommand => new MvxCommand(AudioInstructions);

        public IMvxCommand SelectLanguageCommand => new MvxCommand(SelectLanguage);

        public IMvxCommand VideoTrainingCommand => new MvxCommand(VideoTraining);

        public IMvxCommand ConnectCommand => new MvxCommand(Connect);

        #endregion

        #endregion

        public MainMenuViewModel(IAdapter adapter) : base(adapter)
        {
        }


        private async void AudioInstructions()
        {
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<AudioInstructionsViewModel, MvxBundle>(new MvxBundle(new Dictionary<string, string>()));
        }

        private async void SelectLanguage()
        { 
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<SelectLanguageViewModel, MvxBundle>(new MvxBundle(new Dictionary<string, string>()));  
        }

        private async void VideoTraining()
        {
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<VideoTutorialViewModel, MvxBundle>(null);
        }

        private void Connect()
        {

        }
    }
}
