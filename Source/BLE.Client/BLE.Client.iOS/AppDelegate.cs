using System;
using Foundation;
using MvvmCross.Core;
using MvvmCross.Forms.Platforms.Ios.Core;
using Octane.Xamarin.Forms.VideoPlayer.iOS;
using UIKit;

namespace BLE.Client.iOS
{
    [Register("AppDelegate")]
    public partial class AppDelegate : MvxFormsApplicationDelegate
    {
        protected override void RegisterSetup()
        {
            this.RegisterSetupType<Setup>();
        }

        public override void FinishedLaunching(UIApplication application)
        {
            base.FinishedLaunching(application);

            FormsVideoPlayer.Init();
        }
    }
}
