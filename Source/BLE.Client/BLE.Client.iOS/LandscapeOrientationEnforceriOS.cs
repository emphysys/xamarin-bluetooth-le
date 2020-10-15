using System;
using BLE.Client.Helpers;
using BLE.Client.iOS;
using Foundation;
using UIKit;
using Xamarin.Forms;

[assembly: Dependency(typeof(LandscapeOrientationEnforceriOS))]
namespace BLE.Client.iOS
{

    public class LandscapeOrientationEnforceriOS : ILandscapeOrientationEnforcer
    {
        public void ForceCurrentScreenLandscape()
        {
            UIDevice.CurrentDevice.SetValueForKey(new NSNumber((int)UIInterfaceOrientation.LandscapeLeft), new NSString("orientation"));
        }

        public void RevertToPortrait()
        {
            UIDevice.CurrentDevice.SetValueForKey(new NSNumber((int)UIInterfaceOrientation.Portrait), new NSString("orientation"));
        }
    }
}