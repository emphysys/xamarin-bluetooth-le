using MvvmCross.Commands;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace BLE.Client.ViewModels
{
    public class SelectLanguageViewModel : BaseViewModel
    {
        private string _SelectedLanguage = SelectedLanguage.ToString();
        public string SelectedLanguageStr
        {
            get => _SelectedLanguage;
            set
            {
                _SelectedLanguage = value;
                SelectedLanguage = (AudioPlaybackLanguage)Enum.Parse(typeof(AudioPlaybackLanguage), SelectedLanguageStr);
                RaisePropertyChanged();
            }
        }

        public static AudioPlaybackLanguage SelectedLanguage { get; set; } = AudioPlaybackLanguage.English; 

        public SelectLanguageViewModel(IAdapter adapter) : base(adapter)
        {
        } 
    }

    public enum AudioPlaybackLanguage
    {
        English, Italian
    }
}
