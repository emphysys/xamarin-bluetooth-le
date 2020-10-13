using BLE.Client.Droid;
using BLE.Client.Helpers;
using Plugin.CurrentActivity;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

[assembly: Dependency(typeof(LandscapeOrientationEnforcerAndroid))]
namespace BLE.Client.Droid
{
    class LandscapeOrientationEnforcerAndroid : ILandscapeOrientationEnforcer
    {
        public void ForceCurrentScreenLandscape()
        {
            CrossCurrentActivity.Current.Activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Landscape;
        }

        public void RevertToPortrait()
        {
            CrossCurrentActivity.Current.Activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
        }
    }
}