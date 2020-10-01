using Octane.Xamarin.Forms.VideoPlayer;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace BLE.Client.ViewModels
{
    public class VideoTutorialViewModel : BaseViewModel
    {
        
        public VideoSource VideoSource => VideoSource.FromResource("test.mp4");

        public VideoTutorialViewModel(IAdapter adapter) : base(adapter) { }
    }
}
