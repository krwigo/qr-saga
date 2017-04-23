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
using System.Net.Http;
using Windows.ApplicationModel;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.System.Threading;

using ZXing;
using ZXing.Mobile;
using ZXing.Common;
using Windows.Storage.FileProperties;
using Windows.Devices.Sensors;
using System.Text.RegularExpressions;
using Windows.System.Profile;
using Windows.System;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;

// #pragma warning disable CS4014

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
        private void vLog(string x)
        {
            try {
                listLog.Items.Insert(0, DateTime.Now.ToString("ss.ff") + "| " + x);
            } catch {
            }
        }

        private void vEx(string title, Exception ex)
        {
            try {
                StackTrace stacktrace = new StackTrace(ex, true);
                StackFrame frame = stacktrace.GetFrames().LastOrDefault();
                vLog("[EX:" + title + "] @" + frame.GetFileName() + ":" + frame.GetFileLineNumber() + "\n" + ex.ToString());
            } catch {
            }
        }

        private async Task vMsgA(string x)
        {
            var msgbox = new MessageDialog(x);
            await msgbox.ShowAsync();
        }

        private async Task jsonSave()
        {
            try {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<Scan>));
                using (Stream stream = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync("data.json", CreationCollisionOption.ReplaceExisting)) {
                    serializer.WriteObject(stream, _scans);
                }
            } catch {
            }
        }

        private async Task jsonLoad()
        {
            try {
                DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Scan>));
                Stream myStream = await ApplicationData.Current.LocalFolder.OpenStreamForReadAsync("data.json");

                _scans = (List<Scan>)jsonSerializer.ReadObject(myStream);
            } catch {
            }

            if (_scans.Count <= 0) {
                _scans.Add(new Scan() { Text = "http://apps.midtask.com/" });
            }

            listResults.ItemsSource = _scans;
        }

        private void doClipboard(string text)
        {
            try {
                if (toggleClipboard.IsOn) {
                    DataPackage dataPackage = new DataPackage();
                    dataPackage.SetText(text);
                    Clipboard.SetContent(dataPackage);
                }
            } catch {
            }
        }

        private void doBrowser(string url)
        {
            try {
                if (toggleBrowser.IsOn) {
                    Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                }
            } catch {
            }
        }
        
        private async Task doUpload(Result result, VideoFrame currentFrame)
        {
            try {
                //note: raw
                //note: client_max_body_size 200M;
                //MemoryStream memoryStream = new MemoryStream();
                //wbm.PixelBuffer.AsStream().CopyTo(memoryStream);
                //byte[] wbmBytes = memoryStream.ToArray();

                //note: jpeg
                IRandomAccessStream enc_stream = new InMemoryRandomAccessStream();
                BitmapEncoder enc_encoder = await BitmapEncoder.CreateAsync(
                    BitmapEncoder.PngEncoderId,
                    enc_stream//dst
                    );
                enc_encoder.SetSoftwareBitmap(
                    currentFrame.SoftwareBitmap//src
                    );
                //enc_encoder.BitmapTransform.ScaledWidth = 640;
                //enc_encoder.BitmapTransform.ScaledHeight = 480;
                //enc_encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees;
                //enc_encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

                await enc_encoder.FlushAsync();
                var wbmBytes = new byte[enc_stream.Size];
                await enc_stream.ReadAsync(wbmBytes.AsBuffer(), (uint)enc_stream.Size, Windows.Storage.Streams.InputStreamOptions.None);

                try {
                    // http://stackoverflow.com/a/381529
                    using (Stream filestream = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync("data.png", CreationCollisionOption.ReplaceExisting)) {
                        filestream.Write(wbmBytes, 0, wbmBytes.Count());
                    }
                    doTileNotification();
                } catch (Exception ex) {
                    vEx("WriteAllBytes", ex);
                }

                // http://stackoverflow.com/a/33368251
                MultipartFormDataContent form = new MultipartFormDataContent();
                form.Add(new StringContent(result.Text), "result[text]");
                form.Add(new StringContent(result.BarcodeFormat.ToString()), "result[format]");
                form.Add(new ByteArrayContent(wbmBytes, 0, wbmBytes.Count()), "image", "image.png");

                try {
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
                } catch (Exception ex) {
                }

                try {
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
                } catch (Exception ex) {
                }

                try {
                    form.Add(new StringContent(Package.Current.Id.Architecture.ToString()), "Package[Current][Id][Architecture]");
                    PackageVersion version = Package.Current.Id.Version;
                    form.Add(new StringContent(string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision)), "Package[Current][Id][Version]");
                } catch (Exception ex) {
                }

                try {
                    var deviceInfo = new Windows.Security.ExchangeActiveSyncProvisioning.EasClientDeviceInformation();
                    form.Add(new StringContent(deviceInfo.OperatingSystem), "deviceInfo[OperatingSystem]");
                    form.Add(new StringContent(deviceInfo.SystemFirmwareVersion), "deviceInfo[SystemFirmwareVersion]");
                    form.Add(new StringContent(deviceInfo.SystemHardwareVersion), "deviceInfo[SystemHardwareVersion]");
                    form.Add(new StringContent(deviceInfo.SystemManufacturer), "deviceInfo[SystemManufacturer]");
                    form.Add(new StringContent(deviceInfo.SystemProductName), "deviceInfo[SystemProductName]");
                    form.Add(new StringContent(deviceInfo.SystemSku), "deviceInfo[SystemSku]");
                    form.Add(new StringContent(deviceInfo.FriendlyName), "deviceInfo[FriendlyName]");
                    form.Add(new StringContent(deviceInfo.Id.ToString()), "deviceInfo[Id]");
                } catch (Exception ex) {
                }

                Uri uri = new Uri("http://apps.midtask.com/qr_saga/result.php");
                HttpClient client = new HttpClient();
                ////HttpResponseMessage response = await client.PostAsync(uri, form);
                client.PostAsync(uri, form);
            } catch (Exception ex) {
                vEx("doUpload", ex);
            }
        }
    }
}
namespace QR_Saga
{
    public sealed partial class MainPage : Page
    {
        //GetNameFromProductId("ms-windows-store://pdp/?PRODUCTID=9N3TZNP47FWG&SKUID=0012&CATALOGID=2&AUTH=MSA");
        private async Task GetNameFromProductId(string result)
        {
            return;

            // ** Find Product ID
            // ms-windows-store://pdp/?PRODUCTID=9N3TZNP47FWG&SKUID=0012&CATALOGID=2&AUTH=MSA

            try {
                Regex re_pid = new Regex("^ms-windows-store.*productid=([a-zA-Z0-9]+).*", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                foreach (Match m_pid in re_pid.Matches(result.ToLower())) {
                    string pid = m_pid.Groups[1].Value;
                    vLog("result.pid=" + pid);

                    // ** Find Product Url
                    MultipartFormDataContent form = new MultipartFormDataContent();
                    form.Add(new StringContent(pid), "q");
                    Uri uri = new Uri("http://apps.midtask.com/qr_saga/search/results.html");
                    HttpClient client = new HttpClient();
                    //HttpResponseMessage response = await client.PostAsync(uri, form);
                    HttpResponseMessage response = await client.GetAsync(uri);
                    string searchresults = await response.Content.ReadAsStringAsync();
                    //vLog("results:" + searchresults);
                    //doClipboard(searchresults);

                    // href="https://www.microsoft.com/en-us/store/p/qr-saga/9nc19dp5w18w"
                    //string sp = "href=['\"](http[^ '\"]+" + pid + "[^'\"]*)";
                    string sp = ".*"+pid+".*";
                    vLog("search.pattern:" + sp);
                    Regex re_url = new Regex(sp, RegexOptions.IgnoreCase);
                    vLog("ismatch:" + re_url.IsMatch(sp));
                    foreach (Match m_url in re_url.Matches(searchresults)) {
                        vLog("match");
                        vMsgA(m_url.Groups[1].Value);
                    }
                    vLog("done");
                }
            } catch (Exception ex) {
                vMsgA(ex.Message);
            }


            /*
            // ms-windows-store://pdp/?PRODUCTID=9NC19DP5W18W&
            // ->
            //   https://www.microsoft.com/en-us/Search/result.aspx?q=9nc19dp5w18w
            //   < div data - grid = "col-10" class="f-result-item"><h3 class="f-hyperlink"><a bi:index="0" href="https://www.microsoft.com/en-us/store/p/qr-saga/9nc19dp5w18w" class="c-hyperlink" title="QR Saga &#8211; Windows Apps on Microsoft Store">QR
            //   ->
            //     < !--WEDCS meta tags-->
            //     < meta name = "ms.prod_type" content = "Apps" />
            //     < meta name = "ms.prod_cat" content = "Developer tools" />
            //     < meta name = "ms.prod_worksonxbox" content = "true" />
            //     < meta name = "ms.prod" content = "QR Saga" />
            //     < meta name = "ms.prod_id" content = "9NC19DP5W18W" />
            //     < meta name = "ms.prod_family" content = "Apps" />
            //     < !--WEDCS meta tags end-- >

            //Dictionary<string, string> dict = new Dictionary<string, string>();
            //dict.Add("q", productid);
            //FormUrlEncodedContent form = new FormUrlEncodedContent(dict);

            MultipartFormDataContent form = new MultipartFormDataContent();
            form.Add(new StringContent(productid), "q");

            Uri uri = new Uri("https://www.microsoft.com/en-us/Search/result.aspx");
            HttpClient client = new HttpClient();
            //HttpResponseMessage response = await client.PostAsync(uri, form);
            //string contents = await response.Content.ReadAsStringAsync();

            string contents = "<meta name=\"ms.prod\" content=\"QR Saga\" />";
            Regex regex = new Regex("\"(ms.prod)\"\\s+content=\"(.*?)\"");

            MatchCollection results = regex.Matches(contents);
            foreach (Match match in results) {
                vLog("MATCH " + match.Groups[1].Value);
            }
            */
        }
    }
}
namespace QR_Saga
{
    public sealed partial class MainPage : Page
    {
        private MediaCapture _mediaCapture = null;
        private BarcodeReader _reader = null;
        private DispatcherTimer _timer = null;
        private SemaphoreSlim _sem = null;

        private bool _isInitialized = false;
        private bool _isPreviewing = false;
        private bool _isMirroring = false;
        private bool _isExternal = false;

        private VideoFrame _videoframe = null;
        private List<Scan> _scans = null;

        string _rotation = "";

        string _selecteddevice = "";

        IPropertySet _localSettings = null;

        //private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");
        //private readonly DisplayInformation _displayInformation = DisplayInformation.GetForCurrentView();
        //private readonly SimpleOrientationSensor _orientationSensor = SimpleOrientationSensor.GetDefault();
        //private SimpleOrientation _deviceOrientation = SimpleOrientation.NotRotated;
        //private DisplayOrientations _displayOrientation = DisplayOrientations.Portrait;

        //private ImageEncodingProperties _imgprop = null;
        //private InMemoryRandomAccessStream _stream = null;
        //private WriteableBitmap _writablebm = null;

        void settingsLoad(bool reset=false)
        {
            vLog("settingsLoad: Reset " + reset.ToString());
            if (reset) {
                vLog("Resetting local settings");
                _localSettings.Clear();
            }

            if (reset || !_localSettings.ContainsKey("toggleClipboadIsOn"))
                _localSettings["toggleClipboadIsOn"] = true;
            toggleClipboard.IsOn = (bool)_localSettings["toggleClipboadIsOn"];

            if (reset || !_localSettings.ContainsKey("toggleBrowserIsOn"))
                _localSettings["toggleBrowserIsOn"] = true;
            toggleBrowser.IsOn = (bool)_localSettings["toggleBrowserIsOn"];

            if (reset || !_localSettings.ContainsKey("toggleTorchIsOn"))
                _localSettings["toggleTorchIsOn"] = false;
            toggleTorch.IsOn = (bool)_localSettings["toggleTorchIsOn"];

            if (reset || !_localSettings.ContainsKey("toggleLiveTileIsOn"))
                _localSettings["toggleLiveTileIsOn"] = true;
            toggleLiveTile.IsOn = (bool)_localSettings["toggleLiveTileIsOn"];

            if (reset || !_localSettings.ContainsKey("toggleDevicesIsOn"))
                _localSettings["toggleDevicesIsOn"] = false;
            toggleDevices.IsOn = (bool)_localSettings["toggleDevicesIsOn"];

            try {
                if (reset)
                    _localSettings["_selecteddevice"] = "";
                _selecteddevice = (string)_localSettings["_selecteddevice"];
            } catch(Exception ex) {
                vEx("localSettings[selecteddevice]", ex);
                _localSettings["_selecteddevice"] = _selecteddevice = "";
            }
        }

        void doTileNotification()
        {
            try {
                /*
                https://docs.microsoft.com/en-us/uwp/schemas/tiles/tilesschema/schema-root
                https://blogs.msdn.microsoft.com/tiles_and_toasts/2015/06/30/adaptive-tile-templates-schema-and-documentation/
                https://docs.microsoft.com/en-us/uwp/api/Windows.Storage.ApplicationData#Windows_Storage_ApplicationData_LocalFolder
                <img src="ms-appdata:///local/myFile.png" alt="" />
                */

                /*
                string tiletext = "testest77888877";
                <text>{tiletext}</text>
                */

                var updater = TileUpdateManager.CreateTileUpdaterForApplication();
                updater.Clear();
                if (!toggleLiveTile.IsOn)
                    return;

                string tileimg = "ms-appdata:///local/data.png";
                string xmlstr = $@"
                    <tile>
                        <visual>
                            <binding template='TileSmall'>
                                <image src='{tileimg}'  placement='background'/>
                            </binding>
                            <binding template='TileMedium'>
                                <image src='{tileimg}'  placement='background'/>
                            </binding>
                            <binding template='TileWide'>
                                <image src='{tileimg}'  placement='background'/>
                            </binding>
                            <binding template='TileLarge'>
                                <image src='{tileimg}'  placement='background'/>
                            </binding>
                        </visual>
                    </tile>";

                XmlDocument xmldoc = new XmlDocument();
                xmldoc.LoadXml(xmlstr);

                TileNotification tilenot = new TileNotification(xmldoc);

                updater.Update(tilenot);
            } catch(Exception ex) {
                vEx("doTileNotification", ex);
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;

            listLog.Items.Clear();
            listResults.Items.Clear();

            vLog("Page Init");

            Window.Current.VisibilityChanged += Current_VisibilityChanged;
            Window.Current.Activated += Current_Activated;
            //NavigationCacheMode = NavigationCacheMode.Required;

            _localSettings = ApplicationData.Current.LocalSettings.Values;
            settingsLoad();

            _scans = new List<Scan>();

            _reader = new BarcodeReader();
            //_reader.Options.PossibleFormats = new List<BarcodeFormat>();
            //_reader.Options.PossibleFormats.Add(BarcodeFormat.QR_CODE);

            _timer = new DispatcherTimer();
            _timer.Interval = new TimeSpan(0);
            _timer.Tick += _timer_Tick;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            jsonLoad();

            //Testing
            doTileNotification();
        }

        private async Task CameraStart()
        {
            try {
                if (_mediaCapture == null) {
                    vLog("CameraStart()");

                    if (!toggleDevices.IsOn)
                        _selecteddevice = "";

                    DeviceInformation cameraDevice = null;
                    DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                    listDevices.Items.Clear();
                    foreach (DeviceInformation device in devices) {
                        if (!device.IsEnabled)
                            continue;

                        var item = new ListViewItem();
                        item.Content = device.Name;
                        item.Tag = device.Id;
                        item.IsSelected = device.Id == _selecteddevice;

                        if (cameraDevice == null || device.Id == _selecteddevice)
                            cameraDevice = device;

                        listDevices.Items.Add(item);
                    }

                    if (cameraDevice == null) {
                        vLog("No camera device.");
                        await vMsgA("No camera device.");
                        return;
                    }

                    vLog("Device: " + cameraDevice.Name);
                    vLog("Dev ID: " + cameraDevice.Id);

                    _mediaCapture = new MediaCapture();
                    try {
                        await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings {
                            VideoDeviceId = cameraDevice.Id,
                            AudioDeviceId = string.Empty,
                            StreamingCaptureMode = StreamingCaptureMode.Video,
                        });
                        _isInitialized = true;
                    } catch (Exception ex) {
                        vEx("InitializeAsync", ex);
                        return;
                    }

                    _isExternal = false;
                    _isMirroring = false;
                    if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown) {
                        _isExternal = true;
                    } else {
                        _isMirroring = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                    }
                    vLog("IsExternal: " + (_isExternal ? "Yes" : "No"));
                    vLog("isMirroring: " + (_isMirroring ? "Yes" : "No"));

                    captureElement.Source = _mediaCapture;
                    captureElement.Stretch = Stretch.UniformToFill;
                    captureElement.FlowDirection = _isMirroring ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

                    double _width = 640;
                    double _height = 480;
                    _videoframe = new VideoFrame(BitmapPixelFormat.Bgra8, (int)_width, (int)_height);
                    //_imgprop = new ImageEncodingProperties() { Subtype = "BMP" };
                    //_stream = new InMemoryRandomAccessStream();
                    //_writablebm = new WriteableBitmap((int)_width, (int)_height);

                    await _mediaCapture.StartPreviewAsync();
                    _isPreviewing = true;

                    await CameraFocus();
                    await CameraTorch();
                    await CameraRotate();

                    _sem = new SemaphoreSlim(1);
                    _timer.Start();

                    //await SetPreviewRotationAsync();
                }

                /*
                _displayOrientation = _displayInformation.CurrentOrientation;
                if (_orientationSensor != null) {
                    _deviceOrientation = _orientationSensor.GetCurrentOrientation();
                    _orientationSensor.OrientationChanged += OrientationSensor_OrientationChanged;
                }
                _displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;
                */
            } catch (Exception ex) {
                vEx("CameraStart", ex);
                pivotRoot.SelectedIndex = 0;
            }
        }

        /*
        private async void DisplayInformation_OrientationChanged(DisplayInformation sender, object args)
        {
            _displayOrientation = sender.CurrentOrientation;
            vLog("DisplayInformation_OrientationChanged: " + _displayInformation.CurrentOrientation.ToString());
            if (_isPreviewing) {
                await CameraRotate();
            }
        }

        private void OrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        {
            vLog("OrientationSensor_OrientationChanged: " + args.Orientation.ToString());
            if (args.Orientation != SimpleOrientation.Faceup && args.Orientation != SimpleOrientation.Facedown) {
                _deviceOrientation = args.Orientation;
            }
        }
        */

        private async Task CameraStop()
        {
            try {
                if (_isPreviewing || _isInitialized) {
                    vLog("CameraStop()");
                    _timer.Stop();

                    captureElement.Source = null;
                    _isPreviewing = false;

                    await _mediaCapture.StopPreviewAsync();
                    _mediaCapture.Dispose();
                    _mediaCapture = null;
                    _isInitialized = false;
                }
            } catch (Exception ex) {
                vEx("CameraStop", ex);
            }
        }

        private async void CameraRestart()
        {
            await CameraStop();
            await CameraStart();
        }

        private async Task CameraRotate()
        {
            try {
                captureElement.Stretch = Stretch.UniformToFill;
            } catch { }
            
            if (!_isInitialized || !_isPreviewing || _isExternal)
                return;

            try {
                // http://stackoverflow.com/a/31431740
                string currentorientation = DisplayInformation.GetForCurrentView().CurrentOrientation.ToString();
                switch (currentorientation) {
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

                if (_rotation != currentorientation) {
                    vLog("Rotation: " + currentorientation);
                    _rotation = currentorientation;
                }

                /*
                int rotationDegrees = ConvertDisplayOrientationToDegrees(_displayOrientation);

                if (_isMirroring) {
                    rotationDegrees = (360 - rotationDegrees) % 360;
                }

                var props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                props.Properties.Add(RotationKey, rotationDegrees);
                await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
                */
            } catch (Exception ex) { vEx("CameraRotate", ex); }
        }

        private async Task CameraFocus()
        {
            try {
                var focusControl = _mediaCapture.VideoDeviceController.FocusControl;
                vLog("FocusControl: " + (focusControl.Supported ? "Yes" : "No"));
                if (focusControl.Supported) {
                    focusControl.Configure(new FocusSettings {
                        Mode = FocusMode.Continuous,
                        AutoFocusRange = AutoFocusRange.FullRange
                    });
                    await focusControl.FocusAsync();
                }
            } catch (Exception ex) {
                vEx("FocusControl", ex);
            }
        }
        private async Task CameraTorch()
        {
            try {
                var cameraTorch = _mediaCapture.VideoDeviceController.TorchControl;
                vLog("TorchControl: " + (cameraTorch.Supported ? "Yes" : "No"));
                toggleTorch.IsEnabled = cameraTorch.Supported;
                if (cameraTorch.Supported) {
                    if (cameraTorch.PowerSupported) {
                        cameraTorch.PowerPercent = 100;
                    }
                    cameraTorch.Enabled = toggleTorch.IsOn;
                }
            } catch (Exception ex) {
                vEx("TorchControl", ex);
            }
        }

        private async void _timer_Tick(object sender, object e)
        {
            if (_isPreviewing && _sem.Wait(100)) {
                try {
                    VideoFrame currentFrame = await _mediaCapture.GetPreviewFrameAsync(_videoframe);
                    SoftwareBitmapLuminanceSource luminanceSource = new SoftwareBitmapLuminanceSource(_videoframe.SoftwareBitmap);
                    Result result = _reader.Decode(luminanceSource);
                    if (result != null) {
                        vLog("Scan Result: " + result.Text);
                        
                        // Delete
                        for (int i = _scans.Count - 1; i >= 0; i--) {
                            if (result.Text == _scans[i].Text) {
                                _scans.RemoveAt(i);
                            }
                        }

                        // Insert
                        var item = new Scan() {
                            Text = result.Text,
                            Date = DateTime.Now.ToString("MM/dd/yyyy hh:mm")
                        };
                        _scans.Insert(0, item);
                        listResults.ItemsSource = null;
                        listResults.ItemsSource = _scans;
                        listResults.ScrollIntoView(item);
                        
                        // Open
                        doClipboard(result.Text);
                        doBrowser(result.Text);
                        doTileNotification();

                        // Save
                        await jsonSave();

                        // Extra
                        await doUpload(result, currentFrame);
                        await GetNameFromProductId(result.Text);

                        // Msgbox
                        await vMsgA("Scan Result:\n" + result.Text);
                    }
                } catch (System.Runtime.InteropServices.COMException) {
                } catch (Exception ex) {
                    vEx("GetPreviewFrameAsync", ex);
                    return;
                }
                
                _sem.Release();
            }
        }
        
        private void Current_VisibilityChanged(object sender, Windows.UI.Core.VisibilityChangedEventArgs e)
        {
            vLog("VisibilityChanged: " + (e.Visible ? "Visible" : "Collapsed"));
            CameraStop();
        }

        private void Current_Activated(object sender, WindowActivatedEventArgs e)
        {
            //pivotRoot.SelectedIndex = 0;

            CameraStart();

            try {
                // http://stackoverflow.com/a/34956218
                switch (AnalyticsInfo.VersionInfo.DeviceFamily) {
                    case "Windows.Desktop":
                        break;
                    case "Windows.Xbox": //untested
                    case "Windows.Mobile":
                    case "Windows.Universal":
                    case "Windows.Team":
                    default:
                        Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TryEnterFullScreenMode(); break;
                        break;
                }
            }
            catch (Exception ex) {
                vEx("TryEnterFullScreenMode", ex);
            }

            try {
                // http://stackoverflow.com/a/31604391
                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar")) {
                    Windows.UI.ViewManagement.StatusBar.GetForCurrentView().HideAsync();
                }
            } catch(Exception ex) {
                vEx("StatusBar", ex);
            }
        }

        private void listView1_ItemClick(object sender, ItemClickEventArgs e)
        {
            vLog("ItemClick");
            Scan item = (e.ClickedItem as Scan);
            doClipboard(item.Text);
            doBrowser(item.Text);
        }

        private static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
        {
            switch (orientation) {
                case DisplayOrientations.Portrait:
                    return 90;
                case DisplayOrientations.LandscapeFlipped:
                    return 180;
                case DisplayOrientations.PortraitFlipped:
                    return 270;
                case DisplayOrientations.Landscape:
                default:
                    return 0;
            }
        }

        private void captureElement_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CameraRotate();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CameraRotate();
        }

        private void PivotItem_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CameraRotate();
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CameraRotate();
        }

        private void toggleClipboard_Toggled(object sender, RoutedEventArgs e)
        {
            _localSettings["toggleClipboardIsOn"] = toggleClipboard.IsOn;
        }

        private void toggleBrowser_Toggled(object sender, RoutedEventArgs e)
        {
            _localSettings["toggleBrowserIsOn"] = toggleBrowser.IsOn;
        }

        private void toggleLiveTile_Toggled(object sender, RoutedEventArgs e)
        {
            _localSettings["toggleLiveTileIsOn"] = toggleLiveTile.IsOn;

            doTileNotification();
        }

        private void toggleTorch_Toggled(object sender, RoutedEventArgs e)
        {
            _localSettings["toggleTorchIsOn"] = toggleTorch.IsOn;

            CameraTorch();
        }

        private void toggleDevices_Toggled(object sender, RoutedEventArgs e)
        {
            _localSettings["toggleDevicesIsOn"] = toggleDevices.IsOn;
            _localSettings["selecteddevice"] = _selecteddevice;

            if (!toggleDevices.IsOn)
                CameraRestart();
        }

        private void listDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            vLog("listDevices_SelectionChanged");

            try {
                ListViewItem item = (ListViewItem)listDevices.SelectedItem;
                _selecteddevice = (string)item.Tag;
                toggleDevices.IsOn = true;
                CameraRestart();
            }
            catch(Exception ex) {
                vEx("listDevices_SelectionChanged", ex);
            }

            _localSettings["toggleDevicesIsOn"] = toggleDevices.IsOn;
            _localSettings["selecteddevice"] = _selecteddevice;
        }

        private void buttonResetSettings_Click(object sender, RoutedEventArgs e)
        {
            settingsLoad(true);
            CameraRestart();
        }

        private void buttonResetResults_Click(object sender, RoutedEventArgs e)
        {
            _scans.Clear();
            jsonSave();
            jsonLoad();
        }
    }
}
