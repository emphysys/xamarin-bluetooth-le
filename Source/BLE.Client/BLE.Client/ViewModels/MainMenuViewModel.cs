using Acr.UserDialogs;
using MvvmCross;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Exceptions;
using Plugin.Permissions.Abstractions;
using Plugin.Settings.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        private readonly IBluetoothLE bluetoothLe;
        private readonly IUserDialogs userDialogs; 
        private readonly IPermissions permissions;
         
        private readonly Guid BOARD_UUID = Guid.Parse("00000000-0000-0000-0000-80e126082948");

        public MainMenuViewModel(IBluetoothLE bluetoothLe, IAdapter adapter, IUserDialogs userDialogs, ISettings _, IPermissions permissions) : base(adapter)
        {
            this.bluetoothLe = bluetoothLe;
            this.userDialogs = userDialogs; 
            this.permissions = permissions; 
        }
         
        private async void AudioInstructions()
        {
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<AudioInstructionsViewModel, MvxBundle>(null);
        }

        private async void SelectLanguage()
        { 
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<SelectLanguageViewModel, MvxBundle>(null);  
        }

        private async void VideoTraining()
        {
            await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<VideoTutorialViewModel, MvxBundle>(null);
        }

        private async void Connect()
        {
            if (!bluetoothLe.IsOn || bluetoothLe.State != BluetoothState.On)
            {
                await userDialogs.AlertAsync("Bluetooth is not on! Enable Bluetooth on your phone to proceed.");
                return;
            }
            if (!await CheckOrRequestConnectPermissions())
            {
                return;
            }
            if (!await userDialogs.ConfirmAsync($"Connect to Device {BOARD_UUID}?"))
            {
                return;
            }

            Adapter.ScanMode = ScanMode.LowLatency;
            var connectParams = (Xamarin.Forms.Device.RuntimePlatform == Xamarin.Forms.Device.Android) ? new ConnectParameters(false, false) : default;
            IDevice device;
            try
            {
                var tokenSource = new CancellationTokenSource();
                var config = new ProgressDialogConfig()
                {
                    Title = "Connecting...",
                    CancelText = "Cancel",
                    IsDeterministic = false,
                    OnCancel = tokenSource.Cancel
                };

                using (var progress = userDialogs.Progress(config))
                {
                    progress.Show();

                    device = await Adapter.ConnectToKnownDeviceAsync(BOARD_UUID, connectParams, tokenSource.Token);
                }

                await userDialogs.AlertAsync("Connection complete!");

                await Mvx.IoCProvider.Resolve<IMvxNavigationService>().Navigate<DeviceCommunicationViewModel, MvxBundle>(new MvxBundle(new Dictionary<string, string> { { DeviceIdKey, device.Id.ToString() } }));
            }
            catch (DeviceConnectionException e)
            {
                // GATT 133: device was not found
                if (e.Message.Contains("133"))
                {
                    await userDialogs.AlertAsync("Device not found! Reset the board and try again.");
                }
                else
                {
                    await userDialogs.AlertAsync($"Unknown error occurred: {e.Message}.");
                }
            }
            catch (Exception e)
            {
                await userDialogs.AlertAsync($"Unknown error occurred: {e.Message}.");
            }
        }

        private async Task<bool> CheckOrRequestConnectPermissions()
        {
            if (Xamarin.Forms.Device.RuntimePlatform == Xamarin.Forms.Device.Android)
            {
                var status = await permissions.CheckPermissionStatusAsync(Permission.Location);
                if (status != PermissionStatus.Granted)
                {
                    var permissionResult = await permissions.RequestPermissionsAsync(Permission.Location);
                    if (permissionResult[Permission.Location] != PermissionStatus.Granted)
                    {
                        await userDialogs.AlertAsync("Location Permission Denied! Cannot Connect!");
                        permissions.OpenAppSettings();
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
