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
using Windows.System.Threading;

using ZXing;
using ZXing.Mobile;
using ZXing.Common;

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
        IPropertySet _localSettings = null;
        List<Scan> _scans = new List<Scan>();
        InMemoryRandomAccessStream _stream = null;
        ImageEncodingProperties _imgprop = null;
        VideoFrame _videoframe = null;
        WriteableBitmap _writablebm = null;
        MediaCapture _mediaCapture = null;
        BarcodeReader _reader = null;
        bool _started = false;
        string _rotation = "";

        //SemaphoreSlim _sem = new SemaphoreSlim(1);

        private void vlog(string x)
        {
            try
            {
                listLog.Items.Insert(0, DateTime.Now.ToString("ss.ff") + "| " + x);
            }
            catch
            {
            }
        }

        private void vex(string title, Exception ex)
        {
            try
            {
                StackTrace stacktrace = new StackTrace(ex, true);
                StackFrame frame = stacktrace.GetFrames().LastOrDefault();
                vlog("[EX:"+title+"] @" + frame.GetFileName() + ":" + frame.GetFileLineNumber() + "\n" + ex.ToString());
            }
            catch
            {
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;

            listLog.Items.Clear();

            vlog("mainPage()");

            _reader = new BarcodeReader();
            _reader.Options.PossibleFormats = new List<BarcodeFormat>();
            _reader.Options.PossibleFormats.Add(BarcodeFormat.QR_CODE);

            Window.Current.VisibilityChanged += Current_VisibilityChanged;
            Window.Current.Activated += Current_Activated;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // ** Always
            toggleHiRes.Visibility = Visibility.Collapsed;

            commandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Minimal;

            // ** Settings
            _localSettings = ApplicationData.Current.LocalSettings.Values;

            //Windows.System.Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);

            // ** Settings: toggleLog
            if (!_localSettings.ContainsKey("toggleLog"))
                _localSettings["toggleLog"] = false;
            toggleLog.IsChecked = (bool)_localSettings["toggleLog"];
            listLog.Visibility = toggleLog.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;

            // ** Settings: toggleResults
            if (!_localSettings.ContainsKey("toggleResults"))
                _localSettings["toggleResults"] = true;
            toggleResults.IsChecked = (bool)_localSettings["toggleResults"];
            listResults.Visibility = toggleResults.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;

            // ** Settings: toggleTorch
            if (!_localSettings.ContainsKey("toggleTorch"))
                _localSettings["toggleTorch"] = false;
            toggleTorch.IsChecked = (bool)_localSettings["toggleTorch"];
            doTorch();

            // ** Settings: toggleBrowser
            if (!_localSettings.ContainsKey("toggleBrowser"))
                _localSettings["toggleBrowser"] = true;
            toggleBrowser.IsChecked = (bool)_localSettings["toggleBrowser"];

            // ** Settings: toggleClipboard
            if (!_localSettings.ContainsKey("toggleClipboard"))
                _localSettings["toggleClipboard"] = true;
            toggleClipboard.IsChecked = (bool)_localSettings["toggleClipboard"];

            // ** Settings: toggleHiRes
            if (!_localSettings.ContainsKey("toggleHiRes"))
                _localSettings["toggleHiRes"] = false;
            toggleHiRes.IsChecked = (bool)_localSettings["toggleHiRes"];

            // ** Results
            jsonLoad();
        }

        private async Task cameraStart()
        {
            if (_started)
                return;

            vlog("cameraStart()");
            try
            {
                DeviceInformationCollection webcamList = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                DeviceInformation webcamDev = (from webcam in webcamList where webcam.IsEnabled select webcam).FirstOrDefault();

                vlog("DevID: " + webcamDev.Id);
                vlog("DevName: " + webcamDev.Name);

                _mediaCapture = new MediaCapture();

                await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = webcamDev.Id,
                    AudioDeviceId = string.Empty,
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    PhotoCaptureSource = PhotoCaptureSource.VideoPreview,
                });

                double _width = 640;
                double _height = 480;

                // ** HiRes
                /*
                try
                {
                    if (toggleHiRes.IsChecked ?? false)
                    {
                        uint pixels = 0;
                        IReadOnlyList<IMediaEncodingProperties> m_SelectedResolutions = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);

                        for (int i = 0; i < m_SelectedResolutions.Count; i++)
                        {
                            VideoEncodingProperties vp = (VideoEncodingProperties)m_SelectedResolutions[i];
                            if (vp.Width * vp.Height > pixels)
                            {
                                pixels = vp.Width * vp.Height;
                                await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, vp);
                                vlog(string.Format("DevRes: {0}x{1} fps:{2} bitrate:{3}", vp.Width, vp.Height, vp.FrameRate.Numerator, vp.Bitrate));
                                _width = vp.Width;
                                _height = vp.Height;
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    vex("HiRes", ex);
                }
                */
                
                _videoframe = new VideoFrame(BitmapPixelFormat.Bgra8, (int)_width, (int)_height);
                _imgprop = new ImageEncodingProperties() { Subtype = "BMP" };
                _stream = new InMemoryRandomAccessStream();
                _writablebm = new WriteableBitmap((int)_width, (int)_height);

                captureElement.Source = _mediaCapture;
                await _mediaCapture.StartPreviewAsync();

                _started = true;
                cameraWorker();

                // ** Rotate
                doRotate();

                // ** Torch
                FlashControl flashControl = _mediaCapture.VideoDeviceController.FlashControl;
                vlog("Support Torch: " + (flashControl.Supported ? "Yes" : "No"));
                toggleTorch.IsEnabled = _mediaCapture.VideoDeviceController.FlashControl.Supported;
                doTorch();

                // ** Focus
                try
                {
                    FocusControl focusControl = _mediaCapture.VideoDeviceController.FocusControl;
                    vlog("Support Focus: " + (focusControl.Supported ? "Yes" : "No"));
                    if (focusControl.Supported)
                    {
                        //await mediaCapture.StartPreviewAsync();
                        await focusControl.UnlockAsync();

                        focusControl.Configure(new FocusSettings
                        {
                            Mode = FocusMode.Continuous,
                            AutoFocusRange = AutoFocusRange.FullRange
                        });
                    }
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                vex("DevInit", ex);
            }
        }

        async Task cameraStop()
        {
            if (_started)
            {
                _started = false;
                vlog("cameraStop()");
                await _mediaCapture.StopPreviewAsync();
            }
        }

        async void cameraRestart()
        {
            await cameraStop();
            await cameraStart();
        }

        private async Task cameraWorker()
        {
            if (!_started)
            {
                vlog("cameraWorker(): stop");
                return;
            }

            try
            {
                //note(2017/03/31 06:47:16): CapturePhotoToStreamAsync() causes clicking sound

                VideoFrame currentFrame = await _mediaCapture.GetPreviewFrameAsync(_videoframe);

                //color (ZXing)
                //currentFrame.SoftwareBitmap.CopyToBuffer(_wbm.PixelBuffer);
                //Result result = _ZXingReader.Decode(_wbm);

                //grayscale (ZXing.Mobile)
                SoftwareBitmapLuminanceSource luminanceSource = new SoftwareBitmapLuminanceSource(_videoframe.SoftwareBitmap);
                Result result = _reader.Decode(luminanceSource);

                if (result != null)
                {
                    vlog("Scan Result: " + result.Text);

                    //delete
                    for (int i = _scans.Count - 1; i >= 0; i--)
                    {
                        if (result.Text == _scans[i].Text)
                        {
                            _scans.RemoveAt(i);
                        }
                    }

                    //insert
                    var item = new Scan() { Text = result.Text, Date = DateTime.Now.ToString("MM/dd/yyyy hh:mm") };
                    _scans.Insert(0, item);
                    listResults.ItemsSource = null;
                    listResults.ItemsSource = _scans;
                    listResults.ScrollIntoView(item);

                    doClipboard(result.Text);
                    doBrowser(result.Text);
                    await doUpload(result, currentFrame);

                    //save
                    await jsonWrite();

                    //msgbox
                    var msgbox = new MessageDialog(result.Text);
                    await msgbox.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                vex("cameraWorker", ex);
            }

            cameraWorker();
        }

        private void Current_VisibilityChanged(object sender, Windows.UI.Core.VisibilityChangedEventArgs e)
        {
            cameraStop();
        }

        private void Current_Activated(object sender, WindowActivatedEventArgs e)
        {
            cameraStart();
        }
        
        private void listView1_ItemClick(object sender, ItemClickEventArgs e)
        {
            vlog("itemClick");
            Scan item = (e.ClickedItem as Scan);
            doClipboard(item.Text);
            doBrowser(item.Text);
        }

        private void captureElement_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            doRotate();
        }

        private async Task jsonWrite()
        {
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<Scan>));
                using (Stream stream = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync("data.json", CreationCollisionOption.ReplaceExisting))
                {
                    serializer.WriteObject(stream, _scans);
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
                DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Scan>));
                Stream myStream = await ApplicationData.Current.LocalFolder.OpenStreamForReadAsync("data.json");

                _scans = (List<Scan>)jsonSerializer.ReadObject(myStream);
            }
            catch
            {
            }

            if (_scans.Count <= 0)
            {
                _scans.Add(new Scan() { Text = "http://apps.midtask.com/" });
            }

            listResults.ItemsSource = _scans;
        }

        private void doClipboard(string text)
        {
            try
            {
                DataPackage dataPackage = new DataPackage();
                dataPackage.SetText(text);
                Clipboard.SetContent(dataPackage);
            }
            catch
            {
            }
        }

        private void doBrowser(string url)
        {
            try
            {
                if (toggleBrowser.IsChecked ?? false)
                {
                    Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                }
            }
            catch
            {
            }
        }

        private void doRotate()
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
                    vlog("Rotation: " + currentorientation);
                    _rotation = currentorientation;
                }
            }
            catch (Exception ex)
            {
                vlog(ex.Message);
            }
        }

        private async Task doUpload(Result result, VideoFrame currentFrame)
        {
            try
            {
                //note: raw
                //note: client_max_body_size 200M;
                //MemoryStream memoryStream = new MemoryStream();
                //wbm.PixelBuffer.AsStream().CopyTo(memoryStream);
                //byte[] wbmBytes = memoryStream.ToArray();

                //note: jpeg
                IRandomAccessStream enc_stream = new InMemoryRandomAccessStream();
                BitmapEncoder enc_encoder = await BitmapEncoder.CreateAsync(
                    BitmapEncoder.PngEncoderId/*BitmapEncoder.JpegEncoderId*/,
                    enc_stream/*dst*/
                    );
                enc_encoder.SetSoftwareBitmap(
                    currentFrame.SoftwareBitmap/*src*/
                    );
                //enc_encoder.BitmapTransform.ScaledWidth = 640;
                //enc_encoder.BitmapTransform.ScaledHeight = 480;
                //enc_encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees;
                //enc_encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

                await enc_encoder.FlushAsync();
                var wbmBytes = new byte[enc_stream.Size];
                await enc_stream.ReadAsync(wbmBytes.AsBuffer(), (uint)enc_stream.Size, Windows.Storage.Streams.InputStreamOptions.None);

                // http://stackoverflow.com/a/33368251
                MultipartFormDataContent form = new MultipartFormDataContent();
                form.Add(new StringContent(result.Text), "result[text]");
                form.Add(new StringContent(result.BarcodeFormat.ToString()), "result[format]");
                form.Add(new ByteArrayContent(wbmBytes, 0, wbmBytes.Count()), "image", "image.png");

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
                catch (Exception ex)
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
                catch (Exception ex)
                {
                }

                try
                {
                    form.Add(new StringContent(Package.Current.Id.Architecture.ToString()), "Package[Current][Id][Architecture]");
                    PackageVersion version = Package.Current.Id.Version;
                    form.Add(new StringContent(string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision)), "Package[Current][Id][Version]");
                }
                catch (Exception ex)
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
                catch (Exception ex)
                {
                }

                Uri uri = new Uri("http://apps.midtask.com/qr_saga/result.php");
                HttpClient client = new HttpClient();
                ////HttpResponseMessage response = await client.PostAsync(uri, form);
                client.PostAsync(uri, form);
            }
            catch (Exception ex)
            {
                vex("doUpload", ex);
            }
        }

        private void doTorch()
        {
            try
            {
                if (_mediaCapture.VideoDeviceController.FlashControl.Supported)
                {
                    _mediaCapture.VideoDeviceController.FlashControl.Enabled = toggleTorch.IsChecked ?? false;
                    _mediaCapture.VideoDeviceController.FlashControl.Auto = false;
                }
            }
            catch
            {
            }
        }

        private void toggleClipboard_Click(object sender, RoutedEventArgs e)
        {
            AppBarToggleButton b = (AppBarToggleButton)sender;
            vlog("toggleClipboard: " + b.IsChecked);
            _localSettings["toggleClipboard"] = b.IsChecked;
        }

        private void toggleTorch_Click(object sender, RoutedEventArgs e)
        {
            AppBarToggleButton b = (AppBarToggleButton)sender;
            vlog("toggleTorch: " + b.IsChecked);
            _localSettings["toggleTorch"] = b.IsChecked;
            doTorch();
        }

        private void toggleHiRes_Click(object sender, RoutedEventArgs e)
        {
            AppBarToggleButton b = (AppBarToggleButton)sender;
            vlog("toggleHiRes: " + b.IsChecked);
            _localSettings["toggleHiRes"] = b.IsChecked;
            cameraRestart();
        }

        private void toggleBrowser_Click(object sender, RoutedEventArgs e)
        {
            AppBarToggleButton b = (AppBarToggleButton)sender;
            vlog("toggleBrowser: " + b.IsChecked);
            _localSettings["toggleBrowser"] = b.IsChecked;
        }

        private void toggleLog_Click(object sender, RoutedEventArgs e)
        {
            listLog.Visibility = toggleLog.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;
            _localSettings["toggleLog"] = toggleLog.IsChecked;
        }

        private void toggleResults_Click(object sender, RoutedEventArgs e)
        {
            listResults.Visibility = toggleResults.IsChecked ?? false ? Visibility.Visible : Visibility.Collapsed;
            _localSettings["toggleResults"] = toggleResults.IsChecked;
        }

        /*
        private async void CommandBar_Opening(object sender, object e)
        {
            CommandBar c = (CommandBar)sender;

            // ** Remove
            c.SecondaryCommands.Clear();

            // ** Devices
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            foreach (var device in devices)
            {
                var bDev = new AppBarToggleButton()
                {
                    Label = device.Name,
                    Content = device.Id,
                    IsChecked = (device.Id == m_SelectedDevice)
                };
                bDev.Click += BDev_Click;
                c.SecondaryCommands.Add(bDev);
            }

            return;

            // ** Resolutions
            if (c.SecondaryCommands.Count > 0 && m_SelectedDevice != "")
            {
                c.SecondaryCommands.Add(new AppBarSeparator());
                try
                {
                    for (int i = 0; i < m_SelectedResolutions.Count; i++)
                    {
                        VideoEncodingProperties vp = (VideoEncodingProperties)m_SelectedResolutions[i];
                        string resfmt = string.Format("{0}x{1} {2}fps {3}br", vp.Width, vp.Height, vp.FrameRate.Numerator, vp.Bitrate);
                        var bRes = new AppBarToggleButton()
                        {
                            Label = resfmt,
                            Content = resfmt,
                            IsChecked = (resfmt == m_SelectedResolution)
                        };
                        bRes.Click += BRes_Click; ;
                        c.SecondaryCommands.Add(bRes);
                    }
                }
                catch
                {
                }
            }
        }

        private void BDev_Click(object sender, RoutedEventArgs e)
        {
            //AppBarToggleButton b = (AppBarToggleButton)sender;
            //m_SelectedDevice = (string)b.Content;
            //CameraStop();
            //CameraStart();
        }

        private void BRes_Click(object sender, RoutedEventArgs e)
        {
            //AppBarToggleButton b = (AppBarToggleButton)sender;
            //m_SelectedResolution = (string)b.Content;
            //CameraStop();
            //CameraStart();
        }
        */
    }
}

/*
        private async Task CameraStart_old()
        {
            vlog("CameraStart()");

            // ** INIT
            try
            {
                // ** Device
                    DeviceInformationCollection webcamList = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                DeviceInformation webcamDev = (from webcam in webcamList where webcam.IsEnabled select webcam).FirstOrDefault();

                vlog("Dev ID: " + webcamDev.Id);
                vlog("Dev Name: " + webcamDev.Name);

                // ** MediaCapture
                _mediaCapture = new MediaCapture();
                await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = webcamDev.Id,
                    AudioDeviceId = string.Empty,
                    //StreamingCaptureMode = StreamingCaptureMode.Video,
                    //PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                });

                /*
                _external = false;
                _mirrored = false;
                if (webcamDev.EnclosureLocation == null || webcamDev.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                {
                    _external = true;
                }
                else
                {
                    // Only mirror the preview if the camera is on the front panel
                    _mirrored = (webcamDev.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                }
                vlog(string.Format("Dev Panel: {0}, {1}", (_external ? "External" : "Internal"), (_mirrored ? "Front" : "Rear")));
                /

                vlog("CameraStart(): Success");
            }
            catch (Exception ex)
            {
                //vlog("EX1: " + ex.ToString());
                vex("DevInit", ex);

var msgbox = new MessageDialog("Unable to access camera. Connect a camera and try again!");
await msgbox.ShowAsync();

                return;
            }

            /*
            // ** Camera Resolution
            try
            {
                if (ToggleHiRes.IsChecked ?? false)
                {
                    _best = null;
                    m_SelectedResolutions = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);
                    for (int i = 0; i < m_SelectedResolutions.Count; i++)
                    {
                        VideoEncodingProperties vp = (VideoEncodingProperties)m_SelectedResolutions[i];
                        if (
                            (_best == null) ||
                            (
                                (vp.FrameRate.Numerator >= _best.FrameRate.Numerator) &&
                                (vp.Width + vp.Height >= _best.Width + _best.Height) &&
                                (vp.Bitrate >= _best.Bitrate)
                            )
                        )
                        {
                            _best = vp;
                        }
                    }

                    await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, _best);
                    vlog(string.Format("Dev Res: {0}x{1} fps:{2} bitrate:{3}", _best.Width, _best.Height, _best.FrameRate.Numerator, _best.Bitrate));
                    _width = _best.Width;
                    _height = _best.Height;
                }
            }
            catch (Exception ex)
            {
                vlog("CameraStart()[res1]: " + ex.ToString());
            }
            */

            // ** Camera Rotation
            //tryRotatePreview();

            // ** Camera Flash
            /*
            try
            {
                vlog("Support Flash: " + (_mediaCapture.VideoDeviceController.FlashControl.Supported ? "Yes" : "No"));
                if (_mediaCapture.VideoDeviceController.FlashControl.Supported)
                {
                    _mediaCapture.VideoDeviceController.FlashControl.Auto = false;
                    _mediaCapture.VideoDeviceController.FlashControl.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                vlog("CameraStart()[res2]: " + ex.ToString());
            }
            /

            // ** Camera Start
            captureElement.Source = _mediaCapture;
            //captureElement.Stretch = Stretch.UniformToFill;
            await _mediaCapture.StartPreviewAsync();

// ** Camera Worker
//_sem.Release();
//CameraWorker();
//_timer.Start();

// ** Camera Focus
var focusControl = _mediaCapture.VideoDeviceController.FocusControl;
            vlog("Support Focus: " + (focusControl.Supported? "Yes" : "No"));
            if (focusControl.Supported)
            {
                //await mediaCapture.StartPreviewAsync();
                await focusControl.UnlockAsync();

focusControl.Configure(new FocusSettings
                {
                    Mode = FocusMode.Continuous,
                    AutoFocusRange = AutoFocusRange.FullRange
                });

                /*
                focusControl.Configure(new FocusSettings
                {
                    Mode = FocusMode.Auto
                });
                /
            }
        }


 */