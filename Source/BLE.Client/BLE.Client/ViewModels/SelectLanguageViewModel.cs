using MvvmCross.Commands;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace BLE.Client.ViewModels
{
    public class SelectLanguageViewModel : BaseViewModel
    {
        private string _SelectedLanguage = AudioPlaybackLanguage.English.ToString();
        public string SelectedLanguageStr
        {
            get => _SelectedLanguage;
            set
            {
                _SelectedLanguage = value;
                RaisePropertyChanged();
            }
        }

        public AudioPlaybackLanguage SelectedLanguage { get => (AudioPlaybackLanguage)Enum.Parse(typeof(AudioPlaybackLanguage), SelectedLanguageStr); }

        public SelectLanguageViewModel(IAdapter adapter) : base(adapter)
        {
        } 
    }

    public enum AudioPlaybackLanguage
    {
        English, Italian
    }
}
