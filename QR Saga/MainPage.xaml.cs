using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using Windows.UI.Popups;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.Devices.Enumeration;
using Windows.Storage;
using ZXing;
using Windows.Graphics.Display;

#if DEBUG
#pragma warning disable CS4014
#endif

namespace QR_Saga
{
    [DataContract]
    class Scan
    {
        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public string Date { get; set; }

        public Scan() { }
    }
    
    public sealed partial class MainPage : Page
    {
        private MediaCapture _mediaCapture;
        private List<Scan> m_Scans = new List<Scan>();

        public MainPage()
        {
            this.InitializeComponent();

            //portrait
            try
            {
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
            }
            catch
            {
            }

            //size
            try
            {
                ApplicationView.PreferredLaunchViewSize = new Size(320, 600);
                ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
                ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(320, 600));
            }
            catch
            {
            }

            //listview
            jsonLoad();

            //begin
            try
            {
                qrScan();
            }
            catch
            {
                var msgbox = new MessageDialog("No supported cameras found.");
                msgbox.ShowAsync();
            }
        }
        
        private async Task qrInit()
        {
            DeviceInformationCollection webcamList = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            DeviceInformation backWebcam = (from webcam in webcamList where webcam.IsEnabled select webcam).FirstOrDefault();

            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                VideoDeviceId = backWebcam.Id,
                AudioDeviceId = "",
                StreamingCaptureMode = StreamingCaptureMode.Video,
                PhotoCaptureSource = PhotoCaptureSource.VideoPreview
            });

            try
            {
                _mediaCapture.VideoDeviceController.FocusControl.SetPresetAsync(Windows.Media.Devices.FocusPreset.AutoInfinity);
            }
            catch
            {
                var msgbox = new MessageDialog("Unable to enable Auto focus. Does your device support it?");
                msgbox.ShowAsync();
            }
            //.Configure(FocusSettings);
            /*
            if (cam.FocusControl.Supported){
                await cam.FocusControl.SetPresetAsync(Windows.Media.Devices.FocusPreset.Manual);
                await cam.FocusControl.SetValueAsync(100);
            */

            //_mediaCapture.SetPreviewRotation(VideoRotation.Clockwise90Degrees);
            //_mediaCapture.SetRecordRotation(VideoRotation.Clockwise90Degrees);

            captureElement.Source = _mediaCapture;
            await _mediaCapture.StartPreviewAsync();
        }

        private async void qrScan()
        {
            await qrInit();

            var imgProp = new ImageEncodingProperties { Subtype = "BMP", Width = 600, Height = 800 };
            var bcReader = new BarcodeReader();

            while (true)
            {
                var stream = new InMemoryRandomAccessStream();
                await _mediaCapture.CapturePhotoToStreamAsync(imgProp, stream);

                stream.Seek(0);
                var wbm = new WriteableBitmap(600, 800);
                await wbm.SetSourceAsync(stream);

                var result = bcReader.Decode(wbm);
                if (result != null)
                {
                    //delete
                    for (int i = m_Scans.Count - 1; i >= 0; i--)
                    {
                        if (result.Text == m_Scans[i].Text)
                        {
                            m_Scans.RemoveAt(i);
                        }
                    }

                    //insert
                    var item = new Scan() { Text = result.Text, Date = DateTime.Now.ToString("MM/dd/yyyy hh:mm") };
                    m_Scans.Insert(0, item);
                    listView1.ItemsSource = null;
                    listView1.ItemsSource = m_Scans;
                    listView1.ScrollIntoView(item);

                    //clipboard
                    //var dataPackage = new DataPackage();
                    //dataPackage.SetText(result.Text);
                    //Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                    trySetClipboard(result.Text);

                    //browser
                    //await Windows.System.Launcher.LaunchUriAsync(new Uri(result.Text));
                    tryOpenUrl(result.Text);

                    //save
                    await jsonWrite();
                    
                    //msgbox
                    var msgbox = new MessageDialog(result.Text);
                    await msgbox.ShowAsync();
                }
            }
        }

        private async Task jsonWrite()
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<Scan>));
                using (var stream = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync("data.json", CreationCollisionOption.ReplaceExisting))
                {
                    serializer.WriteObject(stream, m_Scans);
                }
            }
            catch
            {
            }
        }

        private async Task jsonLoad()
        {
            try
            {
                var jsonSerializer = new DataContractJsonSerializer(typeof(List<Scan>));
                var myStream = await ApplicationData.Current.LocalFolder.OpenStreamForReadAsync("data.json");

                m_Scans = (List<Scan>)jsonSerializer.ReadObject(myStream);
            }
            catch
            {
            }

            listView1.ItemsSource = m_Scans;
        }

        private void trySetClipboard(string text)
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
            catch
            {
            }
        }

        private void tryOpenUrl(string url)
        {
            try
            {
                Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
            catch
            {
            }
        }

        private void listView1_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = (e.ClickedItem as Scan);
            tryOpenUrl(item.Text);
        }

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _mediaCapture.VideoDeviceController.FocusControl.SetPresetAsync(Windows.Media.Devices.FocusPreset.AutoInfinity);
                var msgbox = new MessageDialog("Success");
                msgbox.ShowAsync();
            }
            catch
            {
                var msgbox = new MessageDialog("Failure");
                msgbox.ShowAsync();
            }
        }

        private void AppBarButton_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                _mediaCapture.VideoDeviceController.FocusControl.SetPresetAsync(Windows.Media.Devices.FocusPreset.Manual);
                _mediaCapture.VideoDeviceController.FocusControl.SetValueAsync(50);
                var msgbox = new MessageDialog("Success");
                msgbox.ShowAsync();
            }
            catch
            {
                var msgbox = new MessageDialog("Failure");
                msgbox.ShowAsync();
            }
        }

        private void AppBarButton_Click_2(object sender, RoutedEventArgs e)
        {
            try
            {
                _mediaCapture.VideoDeviceController.FocusControl.SetPresetAsync(Windows.Media.Devices.FocusPreset.Manual);
                _mediaCapture.VideoDeviceController.FocusControl.SetValueAsync(100);
                var msgbox = new MessageDialog("Success");
                msgbox.ShowAsync();
            }
            catch
            {
                var msgbox = new MessageDialog("Failure");
                msgbox.ShowAsync();
            }
        }

        private void AppBarButton_Click_3(object sender, RoutedEventArgs e)
        {
            try
            {
                _mediaCapture.VideoDeviceController.FocusControl.SetPresetAsync(Windows.Media.Devices.FocusPreset.Manual);
                _mediaCapture.VideoDeviceController.FocusControl.SetValueAsync(150);
                var msgbox = new MessageDialog("Success");
                msgbox.ShowAsync();
            }
            catch
            {
                var msgbox = new MessageDialog("Failure");
                msgbox.ShowAsync();
            }
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            try
            {
                var value = (sender as Slider).Value;
                uint u = Convert.ToUInt32(value);
                _mediaCapture.VideoDeviceController.FocusControl.SetPresetAsync(Windows.Media.Devices.FocusPreset.Manual);
                _mediaCapture.VideoDeviceController.FocusControl.SetValueAsync(u);
            }
            catch
            {
            }
        }
    }
}
