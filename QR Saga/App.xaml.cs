using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace QR_Saga
{
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();

                try
                {
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
                    }
                }
                catch
                {
                }

                try
                {
                    ApplicationView.PreferredLaunchViewSize = new Size(420, 600);
                    ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
                    ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(420, 600));
                }
                catch
                {
                }

                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();
            }
        }
    }
}
