using System;
using System.Collections.Generic;
using System.Text;

namespace BLE.Client.Helpers
{
    public interface ILandscapeOrientationEnforcer
    {

        void ForceCurrentScreenLandscape();

        void RevertToPortrait();
    }
}
