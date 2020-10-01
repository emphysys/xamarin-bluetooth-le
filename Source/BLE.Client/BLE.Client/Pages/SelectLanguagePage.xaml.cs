using BLE.Client.ViewModels;
using MvvmCross.Forms.Presenters.Attributes;
using MvvmCross.Forms.Views;
using Plugin.BLE.Abstractions.Contracts;

namespace BLE.Client.Pages
{
    [MvxContentPagePresentation(WrapInNavigationPage = true, NoHistory = false)]

    public partial class SelectLanguagePage : MvxContentPage<SelectLanguageViewModel>
    {
        public SelectLanguagePage()
        {
            InitializeComponent();
        }

        private void MvxContentPage_Disappearing(object sender, System.EventArgs e)
        {
            var language = ViewModel.SelectedLanguage;
            AudioInstructionsViewModel.PlaybackLanguage = language;
        }
    }
}