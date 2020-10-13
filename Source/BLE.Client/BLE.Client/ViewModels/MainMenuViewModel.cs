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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms.Internals;

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


        public MainMenuViewModel(IBluetoothLE bluetoothLe, IAdapter adapter, IUserDialogs userDialogs, ISettings _, IPermissions permissions) : base(adapter)
        {
            this.bluetoothLe = bluetoothLe;
            this.userDialogs = userDialogs;
            this.permissions = permissions;

            Adapter.DeviceDiscovered += Adapter_DeviceDiscovered;
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
            if (!await userDialogs.ConfirmAsync($"Connect to Device?"))
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

                    await Adapter.StartScanningForDevicesAsync(deviceFilter: DeviceConnectionPredicate);
                    var wasDeviceFound = AwaitDeviceDiscoveredOrTimeout();

                    if (!wasDeviceFound || Adapter.DiscoveredDevices.Count == 0)
                    {
                        await userDialogs.AlertAsync("Device not found! Reset the board and try again. (If you're on Android, make sure that your " +
                            "location is on!)");
                        return;
                    }

                    if (Adapter.DiscoveredDevices.Count > 1)
                    {
                        await userDialogs.AlertAsync("Two or more devices found... uh oh");
                        return;
                    }

                    device = Adapter.DiscoveredDevices[0];
                    await Adapter.ConnectToDeviceAsync(device, cancellationToken: tokenSource.Token);
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

        private bool DeviceConnectionPredicate(IDevice dev)
        { 
            return (dev.Name?.Equals("EMPBM")).GetValueOrDefault(false);
            //Console.WriteLine($">>> {dev.Id}"); 
            //if (dev.Id.Equals(Guid.Parse("00000000-0000-0000-0000-80e126082948")))
            //{
            //    //var asdf = Task.Run(() => Adapter.ConnectToDeviceAsync(dev));
            //    //asdf.Wait();
            //    var na = dev.Name;
            //}
             
            ////var task = dev.GetServicesAsync();
            ////Task.WaitAll(task);
            //var task = Task.Run(() => dev.GetServicesAsync());
            //task.Wait(); 

            //var services = task.Result; //services.ForEach(s => Console.WriteLine($">>> {s.Id}"));
            //return services.Any((s) => s.Id.Equals(a));
            //(IDevice d) => d.Id.ToString().Split('-').Last().Contains("80e12608")
            //return (await dev.GetServicesAsync()).Any((s) => s.Id.Equals(a));
        }

        /// <summary>
        /// Condition variable for device discovery
        /// </summary>
        private readonly ManualResetEvent deviceDiscoveryCV = new ManualResetEvent(false);

        private void Adapter_DeviceDiscovered(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
        {
            // Awaken the work thread
            deviceDiscoveryCV.Set();
        }

        private bool AwaitDeviceDiscoveredOrTimeout()
        {
            // Await device discovery or adapter scan timeout
            return deviceDiscoveryCV.WaitOne(Adapter.ScanTimeout);
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
