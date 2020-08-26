using MvvmCross.Commands;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace BLE.Client.ViewModels
{
    public class DeviceCommunicationViewModel : BaseViewModel
    {
        #region BINDINGS

        #region PROPERTIES

        private string _Command;
        /// <summary>
        /// The command to send to the board.
        /// </summary>
        public string Command
        {
            get => _Command;
            set
            {
                _Command = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region METHODS

        public MvxCommand SendCommand => new MvxCommand(_SendCommand);

        public void _SendCommand()
        {
            Console.WriteLine("Send Command!");
        }

        #endregion

        #endregion


        public DeviceCommunicationViewModel(IAdapter adapter) : base(adapter)
        {
            Command = string.Empty;
        } 
    }
}
