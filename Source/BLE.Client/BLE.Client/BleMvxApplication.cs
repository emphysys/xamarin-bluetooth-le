using System;
using Acr.UserDialogs;
using BLE.Client.ViewModels;
using MvvmCross;
using MvvmCross.Forms.Core;
using MvvmCross.IoC;
using MvvmCross.Localization;
using MvvmCross.ViewModels;
using Xamarin.Forms;
using Plugin.Settings.Abstractions;

namespace BLE.Client
{
    public class BleMvxApplication : MvxApplication
    {
        public override void Initialize()
        {
            CreatableTypes()
                .EndingWith("Service")
                .AsInterfaces()
                .RegisterAsLazySingleton();


            Mvx.IoCProvider.RegisterSingleton(() => Plugin.Settings.CrossSettings.Current);
            Mvx.IoCProvider.RegisterSingleton(() => Plugin.Permissions.CrossPermissions.Current);
            RegisterAppStart<MainMenuViewModel>();
        }
    }
}
