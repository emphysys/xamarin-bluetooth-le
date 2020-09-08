using BLE.Client.ViewModels;
using MvvmCross.Forms.Presenters.Attributes;
using MvvmCross.Forms.Views;
using OxyPlot.Xamarin.Forms;
using System.Threading;
using System.Threading.Tasks;

namespace BLE.Client.Pages
{
    [MvxContentPagePresentation(WrapInNavigationPage = true, NoHistory = false)]
    public partial class DeviceCommunicationPage : MvxTabbedPage<DeviceCommunicationViewModel>
    {
        private double oldWidth, oldHeight;

        public DeviceCommunicationPage()
        {
            InitializeComponent();
            oldWidth = Width;
            oldHeight = Height;
            SizeChanged += DeviceCommunicationPage_SizeChanged; 
        }

        private void DeviceCommunicationPage_SizeChanged(object sender, System.EventArgs e)
        {
            ViewModel.PlotModel.InvalidatePlot(true); 
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (oldWidth == width && oldHeight == height || ViewModel == null) return;

            oldWidth = width; oldHeight = height;
             
            if (width < height)
            {
                // Portrait 
                ViewModel.PlotViewScaleX = 1;
                ViewModel.PlotViewScaleY = 1;
            }
            else
            {
                // Landscape
                ViewModel.PlotViewScaleX = 1;
                ViewModel.PlotViewScaleY = 0.65;
            }
        }


    }
}