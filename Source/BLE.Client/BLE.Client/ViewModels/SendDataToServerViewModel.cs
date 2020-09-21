using BLE.Client.ViewModels;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using MvvmCross.Commands;
using MvvmCross.ViewModels;
using Newtonsoft.Json.Linq;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Forms
{
    public class SendDataToServerViewModel : BaseViewModel
    {
        #region BINDINGS

        #region PROPERTIES

        private string _FileName;
        public string FileName
        {
            get => _FileName;
            set
            {
                _FileName = value;
                RaisePropertyChanged();
            }
        }

        private bool _IsUploading;
        public bool IsUploading
        {
            get => _IsUploading;
            set
            {
                _IsUploading = value;
                RaisePropertyChanged();
            }
        }

        private bool _IsUploadComplete;
        public bool IsUploadComplete
        {
            get => _IsUploadComplete;
            set
            {
                _IsUploadComplete = value;
                RaisePropertyChanged();
            }
        }

        private string _ErrorMessage;
        public string ErrorMessage
        {
            get => _ErrorMessage;
            set
            {
                _ErrorMessage = $"ERROR:\n{value}";
                RaisePropertyChanged();
            }
        }

        private string _SendButtonText;
        public string SendButtonText
        {
            get => _SendButtonText;
            set
            {
                _SendButtonText = value;
                RaisePropertyChanged();
            }
        }

        private double _FileSize;
        public double FileSize
        {
            get => _FileSize;
            set
            {
                _FileSize = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region COMMANDS

        public IMvxCommand SendDataCommand => new MvxCommand(SendData);

        public IMvxCommand ExitCommand => new MvxCommand(Exit);

        #endregion

        #endregion

        public SendDataToServerViewModel(IAdapter adapter) : base(adapter)
        {
            // Default the file name to the current datetime
            var now = DateTime.Now;
            FileName = now.ToString("dd MMM yyyy HH:mm:ss");
            FileName = "DEBUG: Data Integrity (1 minute) ";

            FileSize = DeviceCommunicationViewModel.CSVDataSizeInBytes / 1042d;

            SendButtonText = "Send";
        }

        /// <summary>
        /// Sends the CSV data to the server.
        /// </summary>
        private async void SendData()
        {
            IsUploading = true;

            try
            {
                var azureKey = GetContainerKey();
                var blobContainer = await GetOrCreateBlobContainer(azureKey);
                var csvData = DeviceCommunicationViewModel.PlotCSVData;

                if (csvData == null)
                {
                    throw new ArgumentException("testing: this should never happen");
                }
                 
                var fileBlob = blobContainer.GetBlockBlobReference($"{FileName}.csv");

                await fileBlob.UploadTextAsync(csvData);

                IsUploading = false;
                IsUploadComplete = true;
                SendButtonText = "Done!"; 
            }
            catch (Exception e)  // <-- This error reporting should be further fleshed out per-type
            { 
                IsUploading = false;
                ErrorMessage = e.Message;
            } 
        }


        /// <summary>
        /// The connection string for the azure file database. Pulled from portal.azure.com in the emphysys1042appecgdata | Access keys page.
        /// IMPORTANT: must use string.Format() and provide the container key as the first argument!
        /// </summary>
        private const string AZURE_CONNECTIONSTR =
                "DefaultEndpointsProtocol=https;AccountName=emphysys1042appecgdata;" +
                "AccountKey={0};" +
                "EndpointSuffix=core.windows.net";

        /// <summary>
        /// The name of the container into which to put the formatted data.
        /// </summary>
        private const string AZURE_CONTAINERNAME = "ecg-csv";

        /// <summary>
        /// The name of the key file.
        /// </summary>
        private const string KEYFILE_FILENAME = "azure_keys.json";

        /// <summary>
        /// The key for the azure container access key in the key file.
        /// </summary>
        private const string KEYFILE_CONTAINER_KEY = "container_key";

        /// <summary>
        /// To parse the azure container key from the embedded JSON key file. 
        /// </summary>
        /// <returns>The key.</returns>
        private string GetContainerKey()
        {
            // As specified at https://docs.microsoft.com/en-us/xamarin/xamarin-forms/data-cloud/data/files?tabs=windows
            var assembly = IntrospectionExtensions.GetTypeInfo(typeof(DeviceCommunicationViewModel)).Assembly;
            var assemblyName = assembly.GetName().Name;
            var resourcePath = $"{assemblyName}.{KEYFILE_FILENAME}";

            var stream = assembly.GetManifestResourceStream(resourcePath);

            string text;
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            var obj = JObject.Parse(text);
            return obj.Value<string>(KEYFILE_CONTAINER_KEY);
        }

        /// <summary>
        /// Either retrieves or creates the azure container to which the plot data will be delivered.
        /// </summary>
        /// <param name="key">The access key for the container.</param>
        /// <returns>The container.</returns>
        private async Task<CloudBlobContainer> GetOrCreateBlobContainer(string key)
        {
            var str = string.Format(AZURE_CONNECTIONSTR, key);
            var client = CloudStorageAccount.Parse(str).CreateCloudBlobClient();
            var container = client.GetContainerReference(AZURE_CONTAINERNAME);

            await container.CreateIfNotExistsAsync();

            return container;
        }

        /// <summary>
        /// Exits the page.
        /// </summary>
        private void Exit()
        {
            Application.Current.MainPage.Navigation.PopAsync();
        }

    }
}
