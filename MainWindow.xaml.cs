// Copyright (C) Microsoft Corporation. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Runtime.Remoting.Messaging;
using System.Xml;

namespace WebDemoExe
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        CoreWebView2Environment _webViewEnvironment;
        CoreWebView2Environment WebViewEnvironment
        {
            get
            {
                if (_webViewEnvironment == null && webView?.CoreWebView2 != null)
                {
                    _webViewEnvironment = webView.CoreWebView2.Environment;
                }

                return _webViewEnvironment;
            }
        }

        List<CoreWebView2Frame> _webViewFrames = new List<CoreWebView2Frame>();

        IDictionary<(string, CoreWebView2PermissionKind, bool), bool> _cachedPermissions =
            new Dictionary<(string, CoreWebView2PermissionKind, bool), bool>();


        public MainWindow()
        {
            var dlg = new DemoDialog();


            var reader = new XmlTextReader("webdemoexe.xml");
            reader.WhitespaceHandling = WhitespaceHandling.None;

            var currentTag = "";
            var dialogTitle = "webDemoExe";
            var autostart = false;

            try
            {

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            Trace.Write("ele", reader.Name);
                            currentTag = reader.Name;
                            break;

                        case XmlNodeType.Text:
                            if (currentTag.Equals("title")) dialogTitle = reader.Value;
                            if (currentTag.Equals("autostart")) autostart = true;
                            break;

                    }
                }

            }
            catch (Exception e)
            {
                dialogTitle = "xml error";

            }

            if (autostart)
            {
                dlg.Title = dialogTitle;

                dlg.ShowDialog();


                Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--autoplay-policy=no-user-gesture-required");
                Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.GetTempPath());

                if (dlg.DialogResult == true)
                {
                    Trace.WriteLine("text3");
                }
                else
                {
                    Close();
                    return;
                }
            }

            DataContext = this;
            InitializeComponent();
            AttachControlEventHandlers(webView);

            if ((bool)dlg.Fullscreen.IsChecked)
            {
                this.WindowStyle = WindowStyle.None;
                this.Topmost = true;
                this.WindowState = WindowState.Maximized;
            }

            webView.Focus();
        }

        void AttachControlEventHandlers(WebView2 control)
        {
            // <NavigationStarting>
            control.NavigationStarting += WebView_NavigationStarting;
            // </NavigationStarting>
            // <NavigationCompleted>
            control.NavigationCompleted += WebView_NavigationCompleted;
            // </NavigationCompleted>
            control.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            control.KeyDown += WebView_KeyDown;
        }


        private bool _isControlInVisualTree = true;

        void RemoveControlFromVisualTree(WebView2 control)
        {
            Layout.Children.Remove(control);
            _isControlInVisualTree = false;
        }

        void AttachControlToVisualTree(WebView2 control)
        {
            Layout.Children.Add(control);
            _isControlInVisualTree = true;
        }

        WebView2 GetReplacementControl(bool useNewEnvironment)
        {
            WebView2 replacementControl = new WebView2();
            ((System.ComponentModel.ISupportInitialize)(replacementControl)).BeginInit();
            // Setup properties and bindings.
            if (useNewEnvironment)
            {
                // Create a new CoreWebView2CreationProperties instance so the environment
                // is made anew.
                replacementControl.CreationProperties = new CoreWebView2CreationProperties();
                replacementControl.CreationProperties.BrowserExecutableFolder = webView.CreationProperties.BrowserExecutableFolder;
                replacementControl.CreationProperties.AdditionalBrowserArguments = webView.CreationProperties.AdditionalBrowserArguments;
                shouldAttachEnvironmentEventHandlers = true;
            }
            else
            {
                replacementControl.CreationProperties = webView.CreationProperties;
            }

            AttachControlEventHandlers(replacementControl);
            replacementControl.Source = webView.Source ?? new Uri("https://www.bing.com");
            ((System.ComponentModel.ISupportInitialize)(replacementControl)).EndInit();

            return replacementControl;
        }

        void WebView_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            void ReinitIfSelectedByUser(string caption, string message)
            {
                this.Dispatcher.InvokeAsync(() =>
                {
                    var selection = MessageBox.Show(message, caption, MessageBoxButton.YesNo);
                    if (selection == MessageBoxResult.Yes)
                    {
                        // The control cannot be re-initialized so we setup a new instance to replace it.
                        // Note the previous instance of the control is disposed of and removed from the
                        // visual tree before attaching the new one.
                        if (_isControlInVisualTree)
                        {
                            RemoveControlFromVisualTree(webView);
                        }
                        webView.Dispose();
                        webView = GetReplacementControl(false);
                        AttachControlToVisualTree(webView);
                        // Set background transparent
                        webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
                    }
                });
            }

            void ReloadIfSelectedByUser(string caption, string message)
            {
                this.Dispatcher.InvokeAsync(() =>
                {
                    var selection = MessageBox.Show(message, caption, MessageBoxButton.YesNo);
                    if (selection == MessageBoxResult.Yes)
                    {
                        webView.Reload();
                        // Set background transparent
                        webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
                    }
                });
            }

            bool IsAppContentUri(Uri source)
            {
                // Sample virtual host name for the app's content.
                // See CoreWebView2.SetVirtualHostNameToFolderMapping: https://learn.microsoft.com/dotnet/api/microsoft.web.webview2.core.corewebview2.setvirtualhostnametofoldermapping
                return source.Host == "appassets.example";
            }

            if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.FrameRenderProcessExited)
            {
                // A frame-only renderer has exited unexpectedly. Check if reload is needed.
                // In this sample we only reload if the app's content has been impacted.
                foreach (CoreWebView2FrameInfo frameInfo in e.FrameInfosForFailedProcess)
                {
                    if (IsAppContentUri(new System.Uri(frameInfo.Source)))
                    {
                        System.Threading.SynchronizationContext.Current.Post((_) =>
                        {
                            ReloadIfSelectedByUser("App content frame unresponsive",
                                "Browser render process for app frame exited unexpectedly. Reload page?");
                        }, null);
                    }
                }

                return;
            }

            // Show the process failure details. Apps can collect info for their logging purposes.
            this.Dispatcher.InvokeAsync(() =>
            {
                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine($"Process kind: {e.ProcessFailedKind}");
                messageBuilder.AppendLine($"Reason: {e.Reason}");
                messageBuilder.AppendLine($"Exit code: {e.ExitCode}");
                messageBuilder.AppendLine($"Process description: {e.ProcessDescription}");
                MessageBox.Show(messageBuilder.ToString(), "Child process failed", MessageBoxButton.OK);
            });

            if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
            {
                ReinitIfSelectedByUser("Browser process exited",
                    "Browser process exited unexpectedly. Recreate webview?");
            }
            else if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
            {
                ReinitIfSelectedByUser("Web page unresponsive",
                    "Browser render process has stopped responding. Recreate webview?");
            }
            else if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited)
            {
                ReloadIfSelectedByUser("Web page unresponsive",
                    "Browser render process exited unexpectedly. Reload page?");
            }
        }


        void WebView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat) return;

            if (e.KeyboardDevice.IsKeyDown(Key.Escape))
            {
                Close();
            }

            /*            bool ctrl = e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) || e.KeyboardDevice.IsKeyDown(Key.RightCtrl);
                        bool alt = e.KeyboardDevice.IsKeyDown(Key.LeftAlt) || e.KeyboardDevice.IsKeyDown(Key.RightAlt);
                        bool shift = e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift);
            */            /*
                        if (e.Key == Key.N && ctrl && !alt && !shift)
                        {
                            new MainWindow().Show();
                            e.Handled = true;
                        }
                        else if (e.Key == Key.W && ctrl && !alt && !shift)
                        {
                            Close();
                            e.Handled = true;
                        }
                        */
        }

        void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            RequeryCommands();
        }

        void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            RequeryCommands();
        }

        private bool shouldAttachEnvironmentEventHandlers = true;

        private string GetSdkBuildVersion()
        {
            CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();

            // The full version string A.B.C.D
            var targetVersionMajorAndRest = options.TargetCompatibleBrowserVersion;
            var versionList = targetVersionMajorAndRest.Split('.');
            if (versionList.Length != 4)
            {
                return "Invalid SDK build version";
            }
            // Keep C.D
            return versionList[2] + "." + versionList[3];
        }

        private string GetRuntimeVersion(CoreWebView2 webView2)
        {
            return webView2.Environment.BrowserVersionString;
        }

        private string GetAppPath()
        {
            return System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        }

        private string GetRuntimePath(CoreWebView2 webView2)
        {
            int processId = (int)webView2.BrowserProcessId;
            try
            {
                Process process = System.Diagnostics.Process.GetProcessById(processId);
                var fileName = process.MainModule.FileName;
                return System.IO.Path.GetDirectoryName(fileName);
            }
            catch (ArgumentException e)
            {
                return e.Message;
            }
            catch (InvalidOperationException e)
            {
                return e.Message;
            }
            // Occurred when a 32-bit process wants to access the modules of a 64-bit process.
            catch (Win32Exception e)
            {
                return e.Message;
            }
        }

        private string GetStartPageUri(CoreWebView2 webView2)
        {
            string uri = "https://appassets.example/index.html";
            if (webView2 == null)
            {
                return uri;
            }
            string sdkBuildVersion = GetSdkBuildVersion(),
                   runtimeVersion = GetRuntimeVersion(webView2),
                   appPath = GetAppPath(),
                   runtimePath = GetRuntimePath(webView2);
            string newUri = $"{uri}?sdkBuild={sdkBuildVersion}&runtimeVersion={runtimeVersion}" +
                $"&appPath={appPath}&runtimePath={runtimePath}";
            return newUri;
        }

        void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                // Setup host resource mapping for local files
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets.example", "demo", CoreWebView2HostResourceAccessKind.DenyCors);
                // Set StartPage Uri
                webView.Source = new Uri(GetStartPageUri(webView.CoreWebView2));

                // <ProcessFailed>
                webView.CoreWebView2.ProcessFailed += WebView_ProcessFailed;
                // </ProcessFailed>
                // <DocumentTitleChanged>
                webView.CoreWebView2.DocumentTitleChanged += WebView_DocumentTitleChanged;
                // </DocumentTitleChanged>
                // <IsDocumentPlayingAudioChanged>
                //webView.CoreWebView2.IsDocumentPlayingAudioChanged += WebView_IsDocumentPlayingAudioChanged;
                // </IsDocumentPlayingAudioChanged>
                // <IsMutedChanged>
                //webView.CoreWebView2.IsMutedChanged += WebView_IsMutedChanged;
                // </IsMutedChanged>
                // <PermissionRequested>
                webView.CoreWebView2.PermissionRequested += WebView_PermissionRequested;
                // </PermissionRequested>
                //webView.CoreWebView2.DOMContentLoaded += WebView_PermissionManager_DOMContentLoaded;
                //webView.CoreWebView2.WebMessageReceived += WebView_PermissionManager_WebMessageReceived;

                // The CoreWebView2Environment instance is reused when re-assigning CoreWebView2CreationProperties
                // to the replacement control. We don't need to re-attach the event handlers unless the environment
                // instance has changed.
                if (shouldAttachEnvironmentEventHandlers)
                {
                    try
                    {
                        // <SubscribeToBrowserProcessExited>
                        WebViewEnvironment.BrowserProcessExited += Environment_BrowserProcessExited;
                        // </SubscribeToBrowserProcessExited>
                        // <SubscribeToNewBrowserVersionAvailable>
                        WebViewEnvironment.NewBrowserVersionAvailable += Environment_NewBrowserVersionAvailable;
                        // </SubscribeToNewBrowserVersionAvailable>
                        // <ProcessInfosChanged>
                        //WebViewEnvironment.ProcessInfosChanged += WebView_ProcessInfosChanged;
                        // </ProcessInfosChanged>
                    }
                    catch (NotImplementedException)
                    {

                    }
                    shouldAttachEnvironmentEventHandlers = false;
                }

                webView.CoreWebView2.FrameCreated += WebView_HandleIFrames;

                return;
            }

            // ERROR_DELETE_PENDING(0x8007012f)
            if (e.InitializationException.HResult == -2147024593)
            {
                MessageBox.Show($"Failed to create webview, because the profile's name has been marked as deleted, please use a different profile's name.");
                Close();
                return;
            }
            MessageBox.Show($"WebView2 creation failed with exception = {e.InitializationException}");
        }

        // <BrowserProcessExited>
        private bool shouldAttemptReinitOnBrowserExit = false;

        void Environment_BrowserProcessExited(object sender, CoreWebView2BrowserProcessExitedEventArgs e)
        {
            // Let ProcessFailed handler take care of process failure.
            if (e.BrowserProcessExitKind == CoreWebView2BrowserProcessExitKind.Failed)
            {
                return;
            }
            if (shouldAttemptReinitOnBrowserExit)
            {
                _webViewEnvironment = null;
                webView = GetReplacementControl(true);
                AttachControlToVisualTree(webView);
                shouldAttemptReinitOnBrowserExit = false;
            }
        }
        // </BrowserProcessExited>

        void WebView_HandleIFrames(object sender, CoreWebView2FrameCreatedEventArgs args)
        {
            _webViewFrames.Add(args.Frame);
            args.Frame.Destroyed += WebViewFrames_DestoryedNestedIFrames;
        }
        void WebViewFrames_DestoryedNestedIFrames(object sender, object args)
        {
            var frameToRemove = _webViewFrames.SingleOrDefault(r => r.IsDestroyed() == 1);
            if (frameToRemove != null)
                _webViewFrames.Remove(frameToRemove);
        }

        // <NewBrowserVersionAvailable>
        // A new version of the WebView2 Runtime is available, our handler gets called.
        // We close our WebView and set a handler to reinitialize it once the WebView2
        // Runtime collection of processes are gone, so we get the new version of the
        // WebView2 Runtime.
        void Environment_NewBrowserVersionAvailable(object sender, object e)
        {
            if (((App)Application.Current).newRuntimeEventHandled)
            {
                return;
            }

            ((App)Application.Current).newRuntimeEventHandled = true;
            System.Threading.SynchronizationContext.Current.Post((_) =>
            {
                UpdateIfSelectedByUser();
            }, null);
        }
        // </NewBrowserVersionAvailable>

        void UpdateIfSelectedByUser()
        {
            // New browser version available, ask user to close everything and re-init.
            StringBuilder messageBuilder = new StringBuilder(256);
            messageBuilder.Append("We detected there is a new version of the WebView2 Runtime installed. ");
            messageBuilder.Append("Do you want to switch to it now? This will re-create the WebView.");
            var selection = MessageBox.Show(this, messageBuilder.ToString(), "New WebView2 Runtime detected", MessageBoxButton.YesNo);
            if (selection == MessageBoxResult.Yes)
            {
                // If this or any other application creates additional WebViews from the same
                // environment configuration, all those WebViews need to be closed before
                // the browser process will exit. This sample creates a single WebView per
                // MainWindow, we let each MainWindow prepare to recreate and close its WebView.
                CloseAppWebViewsForUpdate();
            }
            ((App)Application.Current).newRuntimeEventHandled = false;
        }

        private void CloseAppWebViewsForUpdate()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    mainWindow.CloseWebViewForUpdate();
                }
            }
        }

        private void CloseWebViewForUpdate()
        {
            // We dispose of the control so the internal WebView objects are released
            // and the associated browser process exits.
            shouldAttemptReinitOnBrowserExit = true;
            RemoveControlFromVisualTree(webView);
            webView.Dispose();
        }




        private void sourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {

            if (webView.Source.AbsoluteUri.Contains("webdemoexe_exit")) Close();
        }


        void RequeryCommands()
        {
            // Seems like there should be a way to bind CanExecute directly to a bool property
            // so that the binding can take care keeping CanExecute up-to-date when the property's
            // value changes, but apparently there isn't.  Instead we listen for the WebView events
            // which signal that one of the underlying bool properties might have changed and
            // bluntly tell all commands to re-check their CanExecute status.
            //
            // Another way to trigger this re-check would be to create our own bool dependency
            // properties on this class, bind them to the underlying properties, and implement a
            // PropertyChangedCallback on them.  That arguably more directly binds the status of
            // the commands to the WebView's state, but at the cost of having an extraneous
            // dependency property sitting around for each underlying property, which doesn't seem
            // worth it, especially given that the WebView API explicitly documents which events
            // signal the property value changes.
            CommandManager.InvalidateRequerySuggested();
        }

        void WebView_DocumentTitleChanged(object sender, object e)
        {
            // <DocumentTitle>
            this.Title = webView.CoreWebView2.DocumentTitle;
            // </DocumentTitle>
        }

     
        // <OnPermissionRequested>
        void WebView_PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs args)
        {
            // allow everything!
            args.State = CoreWebView2PermissionState.Allow;
        }
        // </OnPermissionRequested>

   
    }
}
