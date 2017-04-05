﻿using System;
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
using Windows.Graphics.Imaging;
using System.Text;
using Windows.UI.Core;
using Windows.Media;
using System.Threading;
using System.Net;
//using Windows.Web.Http;
using System.Net.Http;
using Windows.ApplicationModel;
using Windows.ApplicationModel.ExtendedExecution;

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
        private List<Scan> m_Scans = new List<Scan>();

        string _rotation;
        bool _started = false;
        bool _focusing = false;
        double _width = 640;
        double _height = 480;

        MediaCapture _mediaCapture;
        BarcodeReader _ZXingReader = new BarcodeReader();
        DispatcherTimer _timerFocus = new DispatcherTimer();
        //SemaphoreSlim _semRender = new SemaphoreSlim(1);
        //SemaphoreSlim _semScan = new SemaphoreSlim(1);

        private void vlog(string x)
        {
            try
            {
                vlog1.Items.Insert(0, DateTime.Now.ToString("ss.ff") + "| " + x);
            }
            catch
            {
            }
        }

        async Task CameraStart()
        {
            if (_started)
                return;
            vlog("CameraStart()");
            _started = true;

            // ** Devices List
            /*
            try
            {
                var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                //DeviceInformation frontCamera = null;
                //DeviceInformation rearCamera = null;

                deviceList1.Items.Clear();
                foreach (var device in devices)
                {
                    deviceList1.Items.Add(device.Name);
                    switch (device.EnclosureLocation.Panel)
                    {
                        case Windows.Devices.Enumeration.Panel.Front:
                            //frontCamera = device;
                            vlog("DeviceList(Front): " + device.Name);
                            break;
                        case Windows.Devices.Enumeration.Panel.Back:
                            //rearCamera = device;
                            vlog("DeviceList(Rear): " + device.Name);
                            break;
                    }
                }
                if (deviceList1.Items.Count > 0)
                {
                    deviceList1.SelectedIndex = 0;
                }
            }
            catch(Exception ex)
            {
                vlog(ex.Message);
            }
            */

            // ** INIT
            try
            {
                // ** Devices
                DeviceInformationCollection webcamList = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                DeviceInformation webcamDev = (from webcam in webcamList where webcam.IsEnabled select webcam).FirstOrDefault();

                vlog("Device ID: " + webcamDev.Id);
                vlog("Device Name: " + webcamDev.Name);

                // ** MediaCapture
                _mediaCapture = new MediaCapture();

                await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = webcamDev.Id,
                    AudioDeviceId = "",
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                });

                _mediaCapture.FocusChanged += _mediaCapture_FocusChanged;
            }
            catch (Exception ex)
            {
                vlog(ex.StackTrace.ToString());
                vlog(ex.InnerException.ToString());
                vlog(ex.Message);

                var msgbox = new MessageDialog("Unable to access camera. Connect a camera and try again!");
                await msgbox.ShowAsync();

                _started = false;
                return;
            }

            if (ToggleHighResolution.IsChecked ?? false)
            {
                vlog("High Resolution Is Checked");

                try
                {
                    // ** Camera Resolution
                    var res = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);
                    uint maxResolution = 0;
                    int indexMaxResolution = 0;
                    _width = 640;
                    _height = 480;
                    if (res.Count >= 1)
                    {
                        for (int i = 0; i < res.Count; i++)
                        {
                            VideoEncodingProperties vp = (VideoEncodingProperties)res[i];
                            if (vp.Width > maxResolution)
                            {
                                indexMaxResolution = i;
                                maxResolution = vp.Width;
                                _width = vp.Width;
                                _height = vp.Height;
                                vlog("Camera Resolution " + i + ": " + vp.Width + "x" + vp.Height);
                            }
                        }
                        vlog("Using Camera Resolution " + indexMaxResolution);
                        await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, res[indexMaxResolution]);
                    }
                }
                catch (Exception ex)
                {
                    vlog(ex.Message);
                }
            }

            // ** Camera Flash
            try
            {
                if (_mediaCapture.VideoDeviceController.FlashControl.Supported)
                {
                    _mediaCapture.VideoDeviceController.FlashControl.Auto = false;
                    _mediaCapture.VideoDeviceController.FlashControl.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                vlog(ex.Message);
            }

            // ** Camera Rotation
            tryRotatePreview();

            // ** Camera Start
            captureElement.Source = _mediaCapture;
            captureElement.Stretch = Stretch.UniformToFill;
            await _mediaCapture.StartPreviewAsync();

            // ** Camera Worker
            CameraWorker();

            // ** Camera Focus
            var focusControl = _mediaCapture.VideoDeviceController.FocusControl;
            if (focusControl.FocusChangedSupported)
            {
                vlog("Camera Auto Focus");

                //await mediaCapture.StartPreviewAsync();
                await focusControl.UnlockAsync();

                focusControl.Configure(new FocusSettings {
                    Mode = FocusMode.Continuous,
                    AutoFocusRange = AutoFocusRange.FullRange
                });

                await focusControl.FocusAsync();
            }
            else if (focusControl.Supported)
            {
                vlog("Camera Manual Focus");

                //await mediaCapture.StartPreviewAsync();
                await focusControl.UnlockAsync();

                focusControl.Configure(new FocusSettings {
                    Mode = FocusMode.Auto
                });

                _timerFocus.Interval = new TimeSpan(0, 0, 3);
                _timerFocus.Start();
            }
            else
            {
                vlog("Camera Cannot Focus");
            }
        }

        async Task CameraWorker()
        {
            while (true)
            {
                if (!_started)
                    return;

                var stream = new InMemoryRandomAccessStream();
                var imgProp = new ImageEncodingProperties {
                    Subtype = "BMP",
                    Width = (uint)_width,
                    Height = (uint)_height
                };

                VideoFrame videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, (int)_width, (int)_height);
                //await _mediaCapture.GetPreviewFrameAsync(videoFrame);
                //await _mediaCapture.CapturePhotoToStreamAsync(imgProp, stream);
                //stream.Seek(0);
                //var wbm = new WriteableBitmap((int)_width, (int)_height); //600, 800
                //await wbm.SetSourceAsync(stream);
                //NOTE(2017/03/31 06:47:16): this causes clicking sound

                var currentFrame = await _mediaCapture.GetPreviewFrameAsync(videoFrame);
                WriteableBitmap wbm = new WriteableBitmap((int)_width, (int)_height);
                currentFrame.SoftwareBitmap.CopyToBuffer(wbm.PixelBuffer);

                Result result = _ZXingReader.Decode(wbm);
                if (result != null)
                {
                    vlog("Scan Result: " + result.Text);

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

                    trySetClipboard(result.Text);
                    tryOpenUrl(result.Text);

                    //upload
                    try
                    {
                        //note: raw
                        //note: client_max_body_size 200M;
                        //MemoryStream memoryStream = new MemoryStream();
                        //wbm.PixelBuffer.AsStream().CopyTo(memoryStream);
                        //byte[] wbmBytes = memoryStream.ToArray();

                        //note: jpeg
                        IRandomAccessStream jpegstream = new InMemoryRandomAccessStream();
                        BitmapEncoder jpegencoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, jpegstream/*DESTINATION*/);
                        jpegencoder.SetSoftwareBitmap(currentFrame.SoftwareBitmap/*SOURCE*/);
                        //jpegencoder.BitmapTransform.ScaledWidth = 640;
                        //jpegencoder.BitmapTransform.ScaledHeight = 480;
                        //encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees;
                        //jpegencoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                        //encoder.IsThumbnailGenerated = true;
                        await jpegencoder.FlushAsync();
                        var wbmBytes = new byte[jpegstream.Size];
                        await jpegstream.ReadAsync(wbmBytes.AsBuffer(), (uint)jpegstream.Size, Windows.Storage.Streams.InputStreamOptions.None);

                        // http://stackoverflow.com/a/33368251
                        MultipartFormDataContent form = new MultipartFormDataContent();
                        form.Add(new StringContent(result.Text), "result[text]");
                        form.Add(new StringContent(result.BarcodeFormat.ToString()), "result[format]");
                        form.Add(new StringContent(_width.ToString()), "_width");
                        form.Add(new StringContent(_height.ToString()), "_height");
                        form.Add(new ByteArrayContent(wbmBytes, 0, wbmBytes.Count()), "image", "image.jpg");

                        try
                        {
                            var foc = _mediaCapture.VideoDeviceController.FocusControl;
                            form.Add(new StringContent(foc.Supported.ToString()), "FocusControl[Supported]");
                            form.Add(new StringContent(foc.Max.ToString()), "FocusControl[Max]");
                            form.Add(new StringContent(foc.Min.ToString()), "FocusControl[Min]");
                            //form.Add(new StringContent(foc.Preset.ToString()), "FocusControl[Preset]");
                            form.Add(new StringContent(foc.Step.ToString()), "FocusControl[Step]");
                            //form.Add(new StringContent(foc.SupportedFocusDistances.ToString()), "FocusControl[SupportedFocusDistances]");
                            //form.Add(new StringContent(foc.SupportedFocusModes.ToString()), "FocusControl[SupportedFocusModes]");
                            //form.Add(new StringContent(foc.SupportedFocusRanges.ToString()), "FocusControl[SupportedFocusRanges]");
                            //form.Add(new StringContent(foc.SupportedPresets.ToString()), "FocusControl[SupportedPresets]");
                            form.Add(new StringContent(foc.Value.ToString()), "FocusControl[Value]");
                            form.Add(new StringContent(foc.WaitForFocusSupported.ToString()), "FocusControl[WaitForFocusSupported]");
                            form.Add(new StringContent(foc.Mode.ToString()), "FocusControl[Mode]");
                            form.Add(new StringContent(foc.FocusChangedSupported.ToString()), "FocusControl[FocusChangedSupported]");
                            form.Add(new StringContent(foc.FocusState.ToString()), "FocusControl[FocusState]");
                        }
                        catch
                        {
                        }

                        try
                        {
                            var flsh = _mediaCapture.VideoDeviceController.FlashControl;
                            form.Add(new StringContent(flsh.Supported.ToString()), "FlashControl[Supported]");
                            form.Add(new StringContent(flsh.AssistantLightEnabled.ToString()), "FlashControl[AssistantLightEnabled]");
                            form.Add(new StringContent(flsh.AssistantLightSupported.ToString()), "FlashControl[AssistantLightSupported]");
                            form.Add(new StringContent(flsh.Auto.ToString()), "FlashControl[Auto]");
                            form.Add(new StringContent(flsh.Enabled.ToString()), "FlashControl[Enabled]");
                            form.Add(new StringContent(flsh.PowerPercent.ToString()), "FlashControl[PowerPercent]");
                            form.Add(new StringContent(flsh.PowerSupported.ToString()), "FlashControl[PowerSupported]");
                            form.Add(new StringContent(flsh.RedEyeReduction.ToString()), "FlashControl[RedEyeReduction]");
                            form.Add(new StringContent(flsh.RedEyeReductionSupported.ToString()), "FlashControl[RedEyeReductionSupported]");
                        }
                        catch
                        {
                        }

                        try
                        {
                            form.Add(new StringContent(Package.Current.Id.Architecture.ToString()), "Package[Current][Id][Architecture]");
                            PackageVersion version = Package.Current.Id.Version;
                            form.Add(new StringContent(string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision)), "Package[Current][Id][Version]");
                        }
                        catch
                        {
                        }

                        try
                        {
                            var deviceInfo = new Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation();
                            form.Add(new StringContent(deviceInfo.OperatingSystem), "deviceInfo[OperatingSystem]");
                            form.Add(new StringContent(deviceInfo.SystemFirmwareVersion), "deviceInfo[SystemFirmwareVersion]");
                            form.Add(new StringContent(deviceInfo.SystemHardwareVersion), "deviceInfo[SystemHardwareVersion]");
                            form.Add(new StringContent(deviceInfo.SystemManufacturer), "deviceInfo[SystemManufacturer]");
                            form.Add(new StringContent(deviceInfo.SystemProductName), "deviceInfo[SystemProductName]");
                            form.Add(new StringContent(deviceInfo.SystemSku), "deviceInfo[SystemSku]");
                            form.Add(new StringContent(deviceInfo.FriendlyName), "deviceInfo[FriendlyName]");
                            form.Add(new StringContent(deviceInfo.Id.ToString()), "deviceInfo[Id]");
                        }
                        catch
                        {
                        }

                        Uri uri = new Uri("http://apps.midtask.com/qr_saga/result.php");
                        HttpClient client = new HttpClient();
                        //HttpResponseMessage response = await client.PostAsync(uri, form);
                        client.PostAsync(uri, form);
                        vlog("Shuttle away.");
                    }
                    catch (Exception ex)
                    {
                        vlog("Shuttle died: " + ex.Message);
                    }

                    //save
                    await jsonWrite();

                    //msgbox
                    var msgbox = new MessageDialog(result.Text);
                    await msgbox.ShowAsync();
                }
            }
        }
        
        private void _mediaCapture_FocusChanged(MediaCapture sender, MediaCaptureFocusChangedEventArgs args)
        {
            vlog("FocusChanged: "+ args.FocusState);
        }

        async Task CameraStop()
        {
            if (!_started)
                return;
            vlog("CameraStop()");
            _started = false;

            _timerFocus.Stop();

            await _mediaCapture.StopPreviewAsync();

            _mediaCapture.FocusChanged -= _mediaCapture_FocusChanged;
            _mediaCapture.Dispose();
            _mediaCapture = null;
        }

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;

            vlog("MainPage()");

            Window.Current.VisibilityChanged += Current_VisibilityChanged;
            Window.Current.Activated += Current_Activated;

            _timerFocus.Tick += timerFocus_Tick;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // ** User Settings
            /*
            ApplicationDataContainer AppSettings = ApplicationData.Current.LocalSettings;

            if (AppSettings.Values.ContainsKey("musicV"))
            {
                musicVolume.Value = (double)AppSettings.Values["musicV"];
            }
            */

            jsonLoad();

            /*
            try
            {
                var session = new ExtendedExecutionSession();
                session.Description = "Location Tracker";
                session.Reason = ExtendedExecutionReason.Unspecified;
                session.Revoked += Session_Revoked;

                var result = await session.RequestExtensionAsync();
                if (result == ExtendedExecutionResult.Allowed)
                {
                    vlog("ExtendedExecutionResult.Allowed");
                }
                else if (result == ExtendedExecutionResult.Denied)
                {
                    vlog("ExtendedExecutionResult.Denied");
                }
            }
            catch (Exception ex)
            {
                vlog(ex.Message);
            }
            */
        }

        private void Session_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            vlog("Session_Revoked");
        }

        private void Current_VisibilityChanged(object sender, Windows.UI.Core.VisibilityChangedEventArgs e)
        {
            CameraStop();
        }

        private void Current_Activated(object sender, WindowActivatedEventArgs e)
        {
            CameraStart();
        }

        private async void timerFocus_Tick(object sender, object e)
        {
            if (!_started)
                return;
            if (_focusing)
                return;
            vlog("Tick");
            _focusing = true;

            try
            {
                await _mediaCapture.VideoDeviceController.FocusControl.FocusAsync();
            }
            catch
            {
            }

            _focusing = false;
        }

        private void listView1_ItemClick(object sender, ItemClickEventArgs e)
        {
            vlog("ItemClick");
            var item = (e.ClickedItem as Scan);
            trySetClipboard(item.Text);
            tryOpenUrl(item.Text);
        }
        
        private void captureElement_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            tryRotatePreview();
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
                Clipboard.SetContent(dataPackage);
            }
            catch
            {
            }
        }

        private void tryOpenUrl(string url)
        {
            try
            {
                if (ToggleOpenUrl.IsChecked ?? false)
                {
                    Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                }
            }
            catch
            {
            }
        }

        private void tryRotatePreview()
        {
            if (!_started)
                return;

            try
            {
                // http://stackoverflow.com/a/31431740
                string currentorientation = DisplayInformation.GetForCurrentView().CurrentOrientation.ToString();
                switch (currentorientation)
                {
                    case "Landscape":
                        _mediaCapture.SetRecordRotation(VideoRotation.None);
                        _mediaCapture.SetPreviewRotation(VideoRotation.None);
                        break;
                    case "Portrait":
                        _mediaCapture.SetRecordRotation(VideoRotation.Clockwise90Degrees);
                        _mediaCapture.SetPreviewRotation(VideoRotation.Clockwise90Degrees);
                        break;
                    case "LandscapeFlipped":
                        _mediaCapture.SetRecordRotation(VideoRotation.Clockwise180Degrees);
                        _mediaCapture.SetPreviewRotation(VideoRotation.Clockwise180Degrees);
                        break;
                    case "PortraitFlipped":
                        _mediaCapture.SetRecordRotation(VideoRotation.Clockwise270Degrees);
                        _mediaCapture.SetPreviewRotation(VideoRotation.Clockwise270Degrees);
                        break;
                    default:
                        _mediaCapture.SetRecordRotation(VideoRotation.None);
                        _mediaCapture.SetPreviewRotation(VideoRotation.None);
                        break;
                }

                if (_rotation != currentorientation)
                {
                    vlog("Camera Rotation: " + currentorientation);
                    _rotation = currentorientation;
                }
            }
            catch (Exception ex)
            {
                vlog(ex.Message);
            }
        }

        private void TogglePanel_Click(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = !splitView.IsPaneOpen;   
        }

        private void ToggleFlash_Click(object sender, RoutedEventArgs e)
        {
            var value = (sender as AppBarToggleButton).IsChecked;
            vlog("ToggleFlash: " + value);

            CameraStop();
            CameraStart();
        }

        private void ToggleHighResolution_Click(object sender, RoutedEventArgs e)
        {
            var value = (sender as AppBarToggleButton).IsChecked;
            vlog("ToggleHiRes: " + value);

            CameraStop();
            CameraStart();
        }

        private void ToggleOpenUrl_Click(object sender, RoutedEventArgs e)
        {
            var value = (sender as AppBarToggleButton).IsChecked;
            vlog("ToggleOpenUrl: " + value);
        }
    }
}
