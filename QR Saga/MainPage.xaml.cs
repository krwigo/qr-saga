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
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.Foundation.Metadata;
using Windows.Media.Devices;

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

            /* note(2017/03/28): moved to App.xaml.cs
            // desktop titlebar
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.ApplicationView"))
            {
                var titleBar = ApplicationView.GetForCurrentView().TitleBar;
                if (titleBar != null)
                {
                    titleBar.ButtonBackgroundColor = Colors.Black;
                    titleBar.ButtonForegroundColor = Colors.White;
                    titleBar.BackgroundColor = Colors.Black;
                    titleBar.ForegroundColor = Colors.White;
                }
                //CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
                //Window.Current.SetTitleBar(BackgroundElement);
            }
            */

            //portrait only
            /*
            try
            {
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
            }
            catch
            {
            }
            */

            //desktop size
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
            //possible exception on Xbox?
            try
            {
                jsonLoad();
            }
            catch
            {
            }

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
            try
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
                    var focusSettings = new FocusSettings();
                    focusSettings.AutoFocusRange = AutoFocusRange.FullRange;
                    focusSettings.Mode = FocusMode.Auto;
                    focusSettings.WaitForFocus = true;
                    focusSettings.DisableDriverFallback = false;
                    _mediaCapture.VideoDeviceController.FocusControl.Configure(focusSettings);
                }
                catch
                {
                    //var msgbox = new MessageDialog("Auto focus is not available on this device.");
                    //await msgbox.ShowAsync();
                    buttonFocusAuto1.IsChecked = false;
                }

                captureElement.Source = _mediaCapture;
                await _mediaCapture.StartPreviewAsync();
            }
            catch
            {
                var msgbox = new MessageDialog("qrInit is not available on this device. Try another!");
                await msgbox.ShowAsync();
            }
        }

        private async void qrScan()
        {
            try
            {
                await qrInit();

                var imgProp = new ImageEncodingProperties { Subtype = "BMP", Width = 1200, Height = 1600 };
                var bcReader = new BarcodeReader();

                while (true)
                {
                    var stream = new InMemoryRandomAccessStream();
                    await _mediaCapture.CapturePhotoToStreamAsync(imgProp, stream);

                    stream.Seek(0);
                    var wbm = new WriteableBitmap(1200, 1600); //600, 800
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
            catch
            {
                var msgbox = new MessageDialog("qrScan is not available on this device. Try another!");
                await msgbox.ShowAsync();
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

            if (m_Scans.Count <= 0)
            {
                m_Scans.Add(new Scan() { Text = "http://apps.midtask.com/" });
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
        
        private void captureElement_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            /* http://stackoverflow.com/a/31431740 */
            try
            {
                string currentorientation = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().CurrentOrientation.ToString();
                switch (currentorientation)
                {
                    case "Landscape":
                        _mediaCapture.SetPreviewRotation(VideoRotation.None);
                        break;
                    case "Portrait":
                        _mediaCapture.SetPreviewRotation(VideoRotation.Clockwise90Degrees);
                        break;
                    case "LandscapeFlipped":
                        _mediaCapture.SetPreviewRotation(VideoRotation.Clockwise180Degrees);
                        break;
                    case "PortraitFlipped":
                        _mediaCapture.SetPreviewRotation(VideoRotation.Clockwise270Degrees);
                        break;
                    default:
                        _mediaCapture.SetPreviewRotation(VideoRotation.None);
                        break;
                }
                //captureElement.Width = Math.Floor(Window.Current.Bounds.Width);
                //captureElement.Height = Math.Floor(Window.Current.Bounds.Height);
            }
            catch
            {
            }
        }

        private void buttonFocusAuto1_Checked(object sender, RoutedEventArgs e)
        {
            focusSlider1.Visibility = Visibility.Collapsed;
            try
            {
                _mediaCapture.VideoDeviceController.FocusControl.SetPresetAsync(Windows.Media.Devices.FocusPreset.AutoInfinity);
            }
            catch
            {
            }
        }

        private void buttonFocusAuto1_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                focusSlider1.Visibility = Visibility.Visible;
                focusSlider1.Value = 100;
            }
            catch
            {
            }
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            try
            {
                var value = (sender as Slider).Value;
                _mediaCapture.VideoDeviceController.FocusControl.SetPresetAsync(Windows.Media.Devices.FocusPreset.Manual);
                _mediaCapture.VideoDeviceController.FocusControl.SetValueAsync((uint)value);
                _mediaCapture.VideoDeviceController.FocusControl.Configure(new FocusSettings { Mode = FocusMode.Manual, Value = (uint)value, DisableDriverFallback = true });
                _mediaCapture.VideoDeviceController.FocusControl.FocusAsync();
            }
            catch
            {
            }
        }

        private void buttonFlash1_Checked(object sender, RoutedEventArgs e)
        {
            var flashControl = _mediaCapture.VideoDeviceController.FlashControl;
            if (!flashControl.Supported)
            {
                var msgbox = new MessageDialog("Flash is not available on this device. Try another!");
                msgbox.ShowAsync();
                buttonFlash1.IsChecked = false;
            }

            try
            {
                _mediaCapture.VideoDeviceController.FlashControl.Enabled = true;
                _mediaCapture.VideoDeviceController.FlashControl.Auto = false;
            }
            catch
            {
            }
        }

        private void buttonFlash1_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                _mediaCapture.VideoDeviceController.FlashControl.Enabled = false;
            }
            catch
            {
            }
        }
    }
}
