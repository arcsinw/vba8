﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Live;
using Microsoft.Live.Controls;
using PhoneDirect3DXamlAppInterop.Resources;
using Windows.Storage;
using System.Threading.Tasks;
using PhoneDirect3DXamlAppComponent;
using PhoneDirect3DXamlAppInterop.Database;
using System.Windows.Media;
using Microsoft.Phone.Tasks;
using Windows.Phone.Storage.SharedAccess;
using System.IO;
using System.Collections.ObjectModel;
using Telerik.Windows.Controls;
using Telerik.Windows.Data;
using System.Windows.Media.Imaging;
using CloudSixConnector.FilePicker;
using CloudSixConnector.FileSaver;
using Coding4Fun.Toolkit.Controls;
using Microsoft.Phone.Net.NetworkInformation;
using Ionic.Zip;
using System.Windows.Data;


//"C:\Program Files (x86)\Microsoft SDKs\Windows Phone\v8.0\Tools\IsolatedStorageExplorerTool\ISETool.exe" ts deviceindex:7 0a409e81-ab14-47f3-bd4e-2f57bb5bae9a "D:\Duc\Documents\Visual Studio 2012\Projects\WP8VBA8\trunk"

namespace PhoneDirect3DXamlAppInterop
{


    public partial class MainPage : PhoneApplicationPage
    {
        private ApplicationBarIconButton resumeButton;
        

        public static bool shouldUpdateBackgroud = false;

        private bool checkAutoUpload = false; //to notify when we should check for auto upload

        public static bool shouldRefreshRecentROMList = false;
        public static bool shouldRefreshAllROMList = false;
        private bool shouldInitialize = true;

        private bool firstLaunch = true;
        
        
        public MainPage()
        {
            InitializeComponent();

#if BETA
            //get license request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://personal.utulsa.edu/~duc-le/VBA8betalicense.dat");
            request.Method = "POST";
            request.ContentType = "text/plain";

            request.BeginGetResponse(ResponseCallback, request);


#endif


            //add tilt effect to tiltablegrid
            Microsoft.Phone.Controls.TiltEffect.TiltableItems.Add(typeof(TiltableGrid));
            Microsoft.Phone.Controls.TiltEffect.TiltableItems.Add(typeof(TiltableCanvas));



            //create ad control
            if (App.HasAds)
            {
                AdControl adControl = new AdControl();
                LayoutRoot.Children.Add(adControl);
                adControl.SetValue(Grid.RowProperty, 1);
            }


            //set data context
            this.DataContext = ROMDatabase.Current;


            //create data context to display rom list
            SortRomList();
            

            //increase app launch counter
            App.metroSettings.NAppLaunch += 1;


            this.InitAppBar();

            //create recent rom list
            this.RefreshRecentROMList();


            //refresh rom list
            //this.db.Commit += () =>
            //{
            //    this.RefreshROMList();
            //};

            

            this.Loaded += MainPage_Loaded;

            

#if GBC
            SystemTray.GetProgressIndicator(this).Text = AppResources.ApplicationTitle2;
            //this.mainPanorama.Title = AppResources.ApplicationTitle2;
#endif
        }

        private void SortRomList()
        {
            //sort list of all roms by names
            CollectionViewSource SortedAllROMEntries = new CollectionViewSource();

            SortedAllROMEntries.SortDescriptions.Add(new System.ComponentModel.SortDescription("DisplayName",
                    System.ComponentModel.ListSortDirection.Ascending));


            SortedAllROMEntries.Source = ROMDatabase.Current.AllROMDBEntries;
            this.romList.DataContext = SortedAllROMEntries;
            this.romList.SelectedItem = null;



        }

        async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            //var indicator = SystemTray.GetProgressIndicator(this);
            //indicator.IsIndeterminate = true;

            try
            {
                
                if (NavigationContext.QueryString.ContainsKey(FileHandler.ROM_URI_STRING))
                {
                    String romFileName = NavigationContext.QueryString[FileHandler.ROM_URI_STRING];
                    NavigationContext.QueryString.Remove(FileHandler.ROM_URI_STRING);

                    ROMDBEntry entry = ROMDatabase.Current.GetROM(romFileName);
                    await this.StartROM(entry);
                }
            }
            
            catch (Exception)
            {
                MessageBox.Show(AppResources.TileOpenError, AppResources.ErrorCaption, MessageBoxButton.OK);
            }

            try
            {
                if (NavigationContext.QueryString.ContainsKey("fileToken"))
                {
                    String fileID = NavigationContext.QueryString["fileToken"];
                    NavigationContext.QueryString.Remove("fileToken");

                    string incomingFileName = HttpUtility.HtmlDecode(SharedStorageAccessManager.GetSharedFileName(fileID).Replace("[1]", ""));
                    string incomingFileType = Path.GetExtension(incomingFileName).ToLower();

                    if (incomingFileType.Contains("cloudsix")) //this is from cloudsix, need to get the true file name and file type
                    {
                        CloudSixFileSelected fileinfo = CloudSixPicker.GetAnswer(fileID);
                        incomingFileName = fileinfo.Filename;
                        incomingFileType = Path.GetExtension(incomingFileName).ToLower();
                    }


                    //import file
                    if (incomingFileType == ".gb" || incomingFileType == ".gbc" || incomingFileType == ".gba")
                    {
                        await FileHandler.ImportRomBySharedID(fileID, incomingFileName, this);
                        this.RefreshRecentROMList();
                    }
                    else if (incomingFileType == ".sgm" || incomingFileType == ".sav")
                        await FileHandler.ImportSaveBySharedID(fileID, incomingFileName, this);

                    else //need to open cloudsix import page to show the content of zip file
                    {
                        this.NavigationService.Navigate(new Uri("/CloudSixImportPage.xaml?fileToken=" + fileID, UriKind.Relative));
                        App.metroSettings.NAppLaunch--; //so that we don't miss asking for review
                    }


                }
            }
            catch (Exception)
            {
                MessageBox.Show(AppResources.FileAssociationError, AppResources.ErrorCaption, MessageBoxButton.OK);
            }


            //ask to rate
            if (App.metroSettings.NAppLaunch % 50 == 14 && App.metroSettings.CanAskReview)
            {
                //ask to rate
                RadMessageBox.Show(AppResources.ReviewPromptTitle, MessageBoxButtons.OKCancel, AppResources.ReviewPromptText,
                    AppResources.NeverShowAgainText, closedHandler: (args) =>
                        {
                            DialogResult result = args.Result;
                            if (result == DialogResult.OK)
                            {
                                App.metroSettings.CanAskReview = false;
                                MarketplaceReviewTask marketplaceReviewTask = new MarketplaceReviewTask();
                                marketplaceReviewTask.Show();
                            }

                            if (args.IsCheckBoxChecked) //user don't want to see remind box again
                            {
                                App.metroSettings.CanAskReview = false;
                            }
                        });
                
                App.metroSettings.NAppLaunch++;
            }
            //else if (firstLaunch) //show toast notifications
            //{
            //    firstLaunch = false;

            //    ToastPrompt toast = new ToastPrompt();

            //    toast.Title = "Tips: ";
            //    toast.Message = "Some message";

            //    toast.Show();

            //}


            //== auto back up
            AutoBackup();

            //set indicator after everything is done
            //indicator.IsIndeterminate = false;
            return;
        }


        private async void AutoBackup()
        {
            if (this.checkAutoUpload && App.metroSettings.AutoBackup)
            {
                this.checkAutoUpload = false; //set it to false until the next time we launch a game

                if (!DeviceNetworkInformation.IsNetworkAvailable)
                    return;

                if (App.metroSettings.BackupOnlyWifi)  //check for wifi
                {
                    if (!IsWifiConnected())
                        return;
                }

                if (App.session == null)
                {
                    MessageBox.Show(AppResources.BackupFailOnedriveText);
                    return;
                }


                LiveConnectClient client = new LiveConnectClient(App.session);
                if (App.exportFolderID == null || App.exportFolderID == "")
                    App.exportFolderID = await ExportSelectionPage.CreateExportFolder(client); //get ID of upload folder


                ROMDBEntry entry = EmulatorPage.currentROMEntry;

                if (DateTime.Compare(entry.LastPlayed, App.LastAutoBackupTime) > 0)
                {

                    if (App.metroSettings.AutoBackupMode == 0) //simple mode
                    {
                        //manual save state
                        if (App.metroSettings.BackupManualSave)
                        {
                            SavestateEntry state = entry.Savestates.Where(s => s.Slot != 9 && s.Savetime != FileHandler.DEFAULT_DATETIME)
                                .OrderByDescending(s => s.Savetime)
                                .FirstOrDefault();

                            if (state != null && DateTime.Compare(state.Savetime, App.LastAutoBackupTime) > 0)
                            {

                                var indicator = SystemTray.GetProgressIndicator(this);
                                indicator.IsIndeterminate = true;
                                indicator.Text = String.Format(AppResources.UploadProgressText, state.FileName);

                                try
                                {
                                    using (var iso = IsolatedStorageFile.GetUserStoreForApplication())
                                    {
                                        String path = FileHandler.ROM_DIRECTORY + "/" + FileHandler.SAVE_DIRECTORY + "/" + state.FileName;

                                        using (IsolatedStorageFileStream fs = iso.OpenFile(path, System.IO.FileMode.Open))
                                        {
                                            await client.UploadAsync(App.exportFolderID, state.FileName, fs, OverwriteOption.Overwrite);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                { }

#if GBC
                        indicator.Text = AppResources.ApplicationTitle2;
#else
                                indicator.Text = AppResources.ApplicationTitle;
#endif
                                indicator.IsIndeterminate = false;

                            }

                        }

                        //auto save state
                        if (App.metroSettings.BackupAutoSave)
                        {
                            SavestateEntry state = entry.Savestates.Where(s => s.Slot == 9).FirstOrDefault();
                            if (EmulatorSettings.Current.AutoSaveLoad && state != null)
                            {
                                //at this point, probably the auto save file is being created, so need to wait for it to complete
                                //if (!App.autoSaveCompleteEvent.WaitOne(3000))
                                //    return;

                                var indicator = SystemTray.GetProgressIndicator(this);
                                indicator.IsIndeterminate = true;
                                indicator.Text = String.Format(AppResources.UploadProgressText, state.FileName);

                                try
                                {
                                    using (var iso = IsolatedStorageFile.GetUserStoreForApplication())
                                    {
                                        String path = FileHandler.ROM_DIRECTORY + "/" + FileHandler.SAVE_DIRECTORY + "/" + state.FileName;

                                        using (IsolatedStorageFileStream fs = iso.OpenFile(path, System.IO.FileMode.Open))
                                        {
                                            await client.UploadAsync(App.exportFolderID, state.FileName, fs, OverwriteOption.Overwrite);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                { }

#if GBC
                                indicator.Text = AppResources.ApplicationTitle2;
#else
                                indicator.Text = AppResources.ApplicationTitle;
#endif
                                indicator.IsIndeterminate = false;

                            }

                        }



                        //ingame state
                        if (App.metroSettings.BackupIngameSave)
                        {
                            int index = entry.FileName.LastIndexOf('.');
                            int diff = entry.FileName.Length - 1 - index;

                            String sramName = entry.FileName.Substring(0, entry.FileName.Length - diff) + "sav";
                            String sramPath = FileHandler.ROM_DIRECTORY + "/" + FileHandler.SAVE_DIRECTORY + "/" + sramName;




                            var indicator = SystemTray.GetProgressIndicator(this);

                            try
                            {
                                using (var iso = IsolatedStorageFile.GetUserStoreForApplication())
                                {
                                    if (iso.FileExists(sramPath))
                                    {

                                        indicator.IsIndeterminate = true;
                                        indicator.Text = String.Format(AppResources.UploadProgressText, sramName);


                                        using (IsolatedStorageFileStream fs = iso.OpenFile(sramPath, System.IO.FileMode.Open))
                                        {
                                            await client.UploadAsync(App.exportFolderID, sramName, fs, OverwriteOption.Overwrite);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            { }

#if GBC
                                indicator.Text = AppResources.ApplicationTitle2;
#else
                            indicator.Text = AppResources.ApplicationTitle;
#endif
                            indicator.IsIndeterminate = false;
                        }
                    } //end simple mode

                    else if (App.metroSettings.AutoBackupMode == 1) //rotating mode
                    {
                        

                        int nfile = 0;

                        using (ZipFile zip = new ZipFile())
                        {
                            using (var iso = IsolatedStorageFile.GetUserStoreForApplication())
                            {
                                //manual save
                                if (App.metroSettings.BackupManualSave)
                                {
                                    SavestateEntry state = entry.Savestates.Where(s => s.Slot != 9 && s.Savetime != FileHandler.DEFAULT_DATETIME)
                                        .OrderByDescending(s => s.Savetime)
                                        .FirstOrDefault();

                                    if (state != null && DateTime.Compare(state.Savetime, App.LastAutoBackupTime) > 0)
                                    {
                                        String path = FileHandler.ROM_DIRECTORY + "/" + FileHandler.SAVE_DIRECTORY + "/" + state.FileName;

                                        IsolatedStorageFileStream fs = iso.OpenFile(path, System.IO.FileMode.Open);
                                        zip.AddEntry(state.FileName, fs);
                                        nfile++;
                                    }

                                }

                                //auto save state
                                if (App.metroSettings.BackupAutoSave)
                                {
                                    SavestateEntry state = entry.Savestates.Where(s => s.Slot == 9).FirstOrDefault();
                                    if (EmulatorSettings.Current.AutoSaveLoad && state != null)
                                    {
                                        String path = FileHandler.ROM_DIRECTORY + "/" + FileHandler.SAVE_DIRECTORY + "/" + state.FileName;
                                        IsolatedStorageFileStream fs = iso.OpenFile(path, System.IO.FileMode.Open);
                                        zip.AddEntry(state.FileName, fs);
                                        nfile++;
                                    }
                                }


                                //ingame state
                                if (App.metroSettings.BackupIngameSave)
                                {
                                    int index = entry.FileName.LastIndexOf('.');
                                    int diff = entry.FileName.Length - 1 - index;

                                    String sramName = entry.FileName.Substring(0, entry.FileName.Length - diff) + "sav";
                                    String sramPath = FileHandler.ROM_DIRECTORY + "/" + FileHandler.SAVE_DIRECTORY + "/" + sramName;


                                    if (iso.FileExists(sramPath))
                                    {
                                        IsolatedStorageFileStream fs = iso.OpenFile(sramPath, System.IO.FileMode.Open);
                                        zip.AddEntry(sramName, fs);
                                        nfile++;

                                    }
                                }

                                if (nfile > 0) //we perform upload now
                                {
                                    if (entry.AutoSaveIndex == null)
                                        entry.AutoSaveIndex = 1;
                                    else
                                        entry.AutoSaveIndex++;


                                    if (entry.AutoSaveIndex > App.metroSettings.NRotatingBackup)
                                        entry.AutoSaveIndex = 1;

                                    ROMDatabase.Current.CommitChanges();

                                    string exportFileName = entry.DisplayName +  entry.AutoSaveIndex.ToString() + ".zip";
                                    

                                    var indicator = SystemTray.GetProgressIndicator(this);
                                    indicator.IsIndeterminate = true;

                                    indicator.Text = String.Format(AppResources.UploadProgressText, exportFileName);

                                    try
                                    {

                                        MemoryStream stream = new MemoryStream();
                                        zip.Save(stream);

                                    
                                        stream.Seek(0, SeekOrigin.Begin);
                                        await client.UploadAsync(App.exportFolderID, exportFileName, stream, OverwriteOption.Overwrite);

                                    }
                                    catch (Exception ex) { }

#if GBC
                                    indicator.Text = AppResources.ApplicationTitle2;
#else
                                    indicator.Text = AppResources.ApplicationTitle;
#endif
                                    indicator.IsIndeterminate = false;
                                }


                            } //end using iso
                        } //end using ZipFile
                    } //end rotating mode
                    
                    App.LastAutoBackupTime = DateTime.Now;

                }
            }
        }
        private bool IsWifiConnected()
        {
            if (DeviceNetworkInformation.IsNetworkAvailable && DeviceNetworkInformation.IsWiFiEnabled)
            {
                return NetworkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
            }

            return false;
        }




#if BETA
        void ResponseCallback(IAsyncResult result)
        {
            try
            {
                HttpWebRequest request = result.AsyncState as HttpWebRequest;

                HttpWebResponse response = request.EndGetResponse(result) as HttpWebResponse;

                using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                {
                    string responseText = sr.ReadToEnd();

                    if (int.Parse(responseText) == 0)
                    {
                        System.Windows.Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            MessageBox.Show("Trial expired. Exiting now.");

                            Application.Current.Terminate();

                        });


                    }
                }
            }
            catch (Exception ex)
            {
                //System.Windows.Deployment.Current.Dispatcher.BeginInvoke(() =>
                //{
                //    MessageBox.Show("Cannot get trial license. Exiting now.");

                //    Application.Current.Terminate();

                //});
            }
        }
#endif
        private async Task Initialize()
        {
            await FileHandler.CreateInitialFolderStructure();
            await this.CopyDemoROM();



            //if (db.Initialize())
            //{
            //    await FileHandler.FillDatabaseAsync();
            //    this.RefreshROMList();
            //}
            
            

            await this.ParseIniFile();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            //set indicator to let the user know we are working on something
            //var indicator = SystemTray.GetProgressIndicator(this);
            //indicator.IsIndeterminate = true;

            //set app bar color in case the user return from setting page
            if (ApplicationBar != null)
            {
                ApplicationBar.BackgroundColor = (Color)App.Current.Resources["CustomChromeColor"];
                ApplicationBar.ForegroundColor = (Color)App.Current.Resources["CustomForegroundColor"];
            }

            //await this.createFolderTask;
            //await this.copyDemoTask;
            if (shouldInitialize)
            {
                await this.Initialize();
                shouldInitialize = false;
            }

            this.LoadInitialSettings();

            if (shouldUpdateBackgroud)
            {
                UpdateBackgroundImage();
                shouldUpdateBackgroud = false;

            }

            if (shouldRefreshRecentROMList)
            {
                this.RefreshRecentROMList();
                shouldRefreshRecentROMList = false;

            }

            if (shouldRefreshAllROMList)
            {
                this.SortRomList();
                shouldRefreshAllROMList = false;
            }


            //enable/disable resume button
            if (this.lastRomImage.DataContext != null)
                this.resumeButton.IsEnabled = true;
            else
                this.resumeButton.IsEnabled = false;

            //set indicator to signal everything is done
            //indicator.IsIndeterminate = false;

            base.OnNavigatedTo(e);
        }


        private void UpdateBackgroundImage()
        {
            if (App.metroSettings.BackgroundUri != null)
            {
                panorama.Background = new ImageBrush
                {
                    Opacity = App.metroSettings.BackgroundOpacity,
                    Stretch = Stretch.None,
                    AlignmentX = System.Windows.Media.AlignmentX.Center,
                    AlignmentY = System.Windows.Media.AlignmentY.Top,
                    ImageSource = FileHandler.getBitmapImage(App.metroSettings.BackgroundUri, FileHandler.DEFAULT_BACKGROUND_IMAGE)

                };
            }
            else
            {
                panorama.Background = null;
            }
        }

        async void btnSignin_SessionChanged(object sender, Microsoft.Live.Controls.LiveConnectSessionChangedEventArgs e)
        {
            if (e.Status == LiveConnectSessionStatus.Connected)
            {
                App.session = e.Session;
                //this.statusLabel.Text = AppResources.StatusSignedIn;
                this.gotoImportButton.IsEnabled = true;
                this.gotoBackupButton.IsEnabled = true;

                LiveConnectClient client = new LiveConnectClient(App.session);
                if (App.metroSettings.AutoBackup && (App.exportFolderID == null || App.exportFolderID == ""))
                    App.exportFolderID = await ExportSelectionPage.CreateExportFolder(client); //get ID of upload folder

                //this.gotoRestoreButton.IsEnabled = true;
            }
            else
            {
                this.gotoImportButton.IsEnabled = false;
                this.gotoBackupButton.IsEnabled = false;
                //this.gotoRestoreButton.IsEnabled = false;
                //this.statusLabel.Text = AppResources.StatusNotSignedIn;
                App.session = null;

                //if (e.Error != null)
                //{
                //    MessageBox.Show(String.Format(AppResources.SkyDriveError, e.Error.Message), AppResources.ErrorCaption, MessageBoxButton.OK);
                //    //statusLabel.Text = e.Error.ToString();
                //}
            }
        }

        private void gotoImportButton_Click_1(object sender, RoutedEventArgs e)
        {
            int romCount = ROMDatabase.Current.GetNumberOfROMs();
            if (!App.IsTrial || romCount < 2)
            {
                if (App.session != null)
                {
                    PhoneApplicationService.Current.State["parameter"] = App.session;
                    this.NavigationService.Navigate(new Uri("/SkyDriveImportPage.xaml", UriKind.Relative));
                }
                else
                {
                    MessageBox.Show(AppResources.NotSignedInError, AppResources.ErrorCaption, MessageBoxButton.OK);
                }
            }
            else
            {
                this.ShowBuyImportDialog();
            }
        }







        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            FileHandler.UpdateLiveTile();

            base.OnNavigatedFrom(e);
        }

        private async Task ParseIniFile()
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync("Assets/vba-over.ini");

            GBAIniParser parser = new GBAIniParser();
            EmulatorSettings.Current.ROMConfigurations = await parser.ParseAsync(file);
        }

        private async Task CopyDemoROM()
        {
            IsolatedStorageSettings isoSettings = IsolatedStorageSettings.ApplicationSettings;
            if (!isoSettings.Contains("DEMOCOPIED"))
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFolder romFolder = await localFolder.CreateFolderAsync("roms", CreationCollisionOption.OpenIfExists);
#if !GBC
                StorageFile file = await StorageFile.GetFileFromPathAsync("Assets/Bunny Advance (Demo).gba");
#else
                StorageFile file = await StorageFile.GetFileFromPathAsync("Assets/Pong.gb");
#endif
                await file.CopyAsync(romFolder);

                isoSettings["DEMOCOPIED"] = true;
                isoSettings.Save();

                await FileHandler.FillDatabaseAsync();

                this.RefreshRecentROMList();
            }

            
        }

        private void LoadInitialSettings()
        {
            EmulatorSettings settings = EmulatorSettings.Current;
            if (!settings.Initialized)
            {
                IsolatedStorageSettings isoSettings = IsolatedStorageSettings.ApplicationSettings;
                settings.Initialized = true;

                if (!isoSettings.Contains(SettingsPage.EnableSoundKey))
                {
                    isoSettings[SettingsPage.EnableSoundKey] = true;
                }
                if (!isoSettings.Contains(SettingsPage.VControllerPosKey))
                {
                    isoSettings[SettingsPage.VControllerPosKey] = false;
                }
                if (!isoSettings.Contains(SettingsPage.LowFreqModeKey))
                {
                    isoSettings[SettingsPage.LowFreqModeKey] = false;
                }
                //if (!isoSettings.Contains(SettingsPage.LowFreqModeMeasuredKey))
                //{
                //    isoSettings[SettingsPage.LowFreqModeMeasuredKey] = false;
                //}
                if (!isoSettings.Contains(SettingsPage.OrientationKey))
                {
                    isoSettings[SettingsPage.OrientationKey] = 0;
                }
                if (!isoSettings.Contains(SettingsPage.ControllerScaleKey))
                {
                    isoSettings[SettingsPage.ControllerScaleKey] = 120;
                }
                if (!isoSettings.Contains(SettingsPage.ButtonScaleKey))
                {
                    isoSettings[SettingsPage.ButtonScaleKey] = 100;
                }
                if (!isoSettings.Contains(SettingsPage.OpacityKey))
                {
                    isoSettings[SettingsPage.OpacityKey] = 30;
                }
                if (!isoSettings.Contains(SettingsPage.AspectKey))
                {
                    isoSettings[SettingsPage.AspectKey] = AspectRatioMode.Original;
                }
                if (!isoSettings.Contains(SettingsPage.SkipFramesKey))
                {
                    isoSettings[SettingsPage.SkipFramesKey] = 3;
                }
                if (!isoSettings.Contains(SettingsPage.ImageScalingKey))
                {
                    isoSettings[SettingsPage.ImageScalingKey] = 100;
                }
                if (!isoSettings.Contains(SettingsPage.TurboFrameSkipKey))
                {
#if GBC
                    // Must be divisible by two for GBC
                    isoSettings[SettingsPage.TurboFrameSkipKey] = 4;
#else
                    isoSettings[SettingsPage.TurboFrameSkipKey] = 4;
#endif
                }
                if (!isoSettings.Contains(SettingsPage.SyncAudioKey))
                {
                    isoSettings[SettingsPage.SyncAudioKey] = true;
                }
                if (!isoSettings.Contains(SettingsPage.PowerSaverKey))
                {
                    isoSettings[SettingsPage.PowerSaverKey] = 0;
                }
                if (!isoSettings.Contains(SettingsPage.DPadStyleKey))
                {
                    isoSettings[SettingsPage.DPadStyleKey] = 1;
                }
                if (!isoSettings.Contains(SettingsPage.DeadzoneKey))
                {
                    isoSettings[SettingsPage.DeadzoneKey] = 10.0f;
                }
                if (!isoSettings.Contains(SettingsPage.CameraAssignKey))
                {
                    isoSettings[SettingsPage.CameraAssignKey] = 0;
                }
                if (!isoSettings.Contains(SettingsPage.ConfirmationKey))
                {
                    isoSettings[SettingsPage.ConfirmationKey] = false;
                }
                if (!isoSettings.Contains(SettingsPage.ConfirmationLoadKey))
                {
                    isoSettings[SettingsPage.ConfirmationLoadKey] = false;
                }
                if (!isoSettings.Contains(SettingsPage.AutoIncKey))
                {
                    isoSettings[SettingsPage.AutoIncKey] = false;
                }
                if (!isoSettings.Contains(SettingsPage.SelectLastState))
                {
                    isoSettings[SettingsPage.SelectLastState] = true;
                }
                if (!isoSettings.Contains(SettingsPage.RestoreCheatKey))
                {
                    isoSettings[SettingsPage.RestoreCheatKey] = false;
                }
                if (!isoSettings.Contains(SettingsPage.CreateManualSnapshotKey))
                {
                    isoSettings[SettingsPage.CreateManualSnapshotKey] = false;
                }
                if (!isoSettings.Contains(SettingsPage.UseMogaControllerKey))
                {
#if BETA
                    isoSettings[SettingsPage.UseMogaControllerKey] = true;
#else
                    isoSettings[SettingsPage.UseMogaControllerKey] = false;
#endif
                }
                if (!isoSettings.Contains(SettingsPage.UseColorButtonKey))
                {
                    isoSettings[SettingsPage.UseColorButtonKey] = true;
                }
                if (!isoSettings.Contains(SettingsPage.BgcolorRKey))
                {
                    isoSettings[SettingsPage.BgcolorRKey] = 210;
                }
                if (!isoSettings.Contains(SettingsPage.BgcolorGKey))
                {
                    isoSettings[SettingsPage.BgcolorGKey] = 210;
                }
                if (!isoSettings.Contains(SettingsPage.BgcolorBKey))
                {
                    isoSettings[SettingsPage.BgcolorBKey] = 210;
                }
                if (!isoSettings.Contains(SettingsPage.AutoSaveLoadKey))
                {
                    isoSettings[SettingsPage.AutoSaveLoadKey] = App.metroSettings.LoadLastState;  //this is for compability with a faulty update (2.9.0)
                }
                if (!isoSettings.Contains(SettingsPage.VirtualControllerStyleKey))
                {
                    isoSettings[SettingsPage.VirtualControllerStyleKey] = 0;
                }

                //get default controller position
                int[] cpos = CustomizeControllerPage.GetDefaultControllerPosition();
                

                //set default controller position
                if (!isoSettings.Contains(SettingsPage.PadCenterXPKey))
                {
                    isoSettings[SettingsPage.PadCenterXPKey] = cpos[0];
                }
                if (!isoSettings.Contains(SettingsPage.PadCenterYPKey))
                {
                    isoSettings[SettingsPage.PadCenterYPKey] = cpos[1];
                }
                if (!isoSettings.Contains(SettingsPage.ALeftPKey))
                {
                    isoSettings[SettingsPage.ALeftPKey] = cpos[2];
                }
                if (!isoSettings.Contains(SettingsPage.ATopPKey))
                {
                    isoSettings[SettingsPage.ATopPKey] = cpos[3];
                }
                if (!isoSettings.Contains(SettingsPage.BLeftPKey))
                {
                    isoSettings[SettingsPage.BLeftPKey] = cpos[4];
                }
                if (!isoSettings.Contains(SettingsPage.BTopPKey))
                {
                    isoSettings[SettingsPage.BTopPKey] = cpos[5];
                }
                if (!isoSettings.Contains(SettingsPage.StartLeftPKey))
                {
                    isoSettings[SettingsPage.StartLeftPKey] = cpos[6];
                }
                if (!isoSettings.Contains(SettingsPage.StartTopPKey))
                {
                    isoSettings[SettingsPage.StartTopPKey] = cpos[7];
                }
                if (!isoSettings.Contains(SettingsPage.SelectRightPKey))
                {
                    isoSettings[SettingsPage.SelectRightPKey] = cpos[8];
                }
                if (!isoSettings.Contains(SettingsPage.SelectTopPKey))
                {
                    isoSettings[SettingsPage.SelectTopPKey] = cpos[9];
                }
                if (!isoSettings.Contains(SettingsPage.LLeftPKey))
                {
                    isoSettings[SettingsPage.LLeftPKey] = cpos[10];
                }
                if (!isoSettings.Contains(SettingsPage.LTopPKey))
                {
                    isoSettings[SettingsPage.LTopPKey] = cpos[11];
                }
                if (!isoSettings.Contains(SettingsPage.RRightPKey))
                {
                    isoSettings[SettingsPage.RRightPKey] = cpos[12];
                }
                if (!isoSettings.Contains(SettingsPage.RTopPKey))
                {
                    isoSettings[SettingsPage.RTopPKey] = cpos[13];
                }
                if (!isoSettings.Contains(SettingsPage.PadCenterXLKey))
                {
                    isoSettings[SettingsPage.PadCenterXLKey] = cpos[14];
                }
                if (!isoSettings.Contains(SettingsPage.PadCenterYLKey))
                {
                    isoSettings[SettingsPage.PadCenterYLKey] = cpos[15];
                }
                if (!isoSettings.Contains(SettingsPage.ALeftLKey))
                {
                    isoSettings[SettingsPage.ALeftLKey] = cpos[16];
                }
                if (!isoSettings.Contains(SettingsPage.ATopLKey))
                {
                    isoSettings[SettingsPage.ATopLKey] = cpos[17];
                }
                if (!isoSettings.Contains(SettingsPage.BLeftLKey))
                {
                    isoSettings[SettingsPage.BLeftLKey] = cpos[18];
                }
                if (!isoSettings.Contains(SettingsPage.BTopLKey))
                {
                    isoSettings[SettingsPage.BTopLKey] = cpos[19];
                }
                if (!isoSettings.Contains(SettingsPage.StartLeftLKey))
                {
                    isoSettings[SettingsPage.StartLeftLKey] = cpos[20];
                }
                if (!isoSettings.Contains(SettingsPage.StartTopLKey))
                {
                    isoSettings[SettingsPage.StartTopLKey] = cpos[21];
                }
                if (!isoSettings.Contains(SettingsPage.SelectRightLKey))
                {
                    isoSettings[SettingsPage.SelectRightLKey] = cpos[22];
                }
                if (!isoSettings.Contains(SettingsPage.SelectTopLKey))
                {
                    isoSettings[SettingsPage.SelectTopLKey] = cpos[23];
                }
                if (!isoSettings.Contains(SettingsPage.LLeftLKey))
                {
                    isoSettings[SettingsPage.LLeftLKey] = cpos[24];
                }
                if (!isoSettings.Contains(SettingsPage.LTopLKey))
                {
                    isoSettings[SettingsPage.LTopLKey] = cpos[25];
                }
                if (!isoSettings.Contains(SettingsPage.RRightLKey))
                {
                    isoSettings[SettingsPage.RRightLKey] = cpos[26];
                }
                if (!isoSettings.Contains(SettingsPage.RTopLKey))
                {
                    isoSettings[SettingsPage.RTopLKey] = cpos[27];
                }

                //moga mapping
                if (!isoSettings.Contains(SettingsPage.MogaAKey))
                {
                    isoSettings[SettingsPage.MogaAKey] = 2;
                }
                if (!isoSettings.Contains(SettingsPage.MogaBKey))
                {
                    isoSettings[SettingsPage.MogaBKey] = 1;
                }
                if (!isoSettings.Contains(SettingsPage.MogaXKey))
                {
                    isoSettings[SettingsPage.MogaXKey] = 16;
                }
                if (!isoSettings.Contains(SettingsPage.MogaYKey))
                {
                    isoSettings[SettingsPage.MogaYKey] = 16;
                }
                if (!isoSettings.Contains(SettingsPage.MogaL1Key))
                {
                    isoSettings[SettingsPage.MogaL1Key] = 4;
                }
                if (!isoSettings.Contains(SettingsPage.MogaL2Key))
                {
                    isoSettings[SettingsPage.MogaL2Key] = 4;
                }
                if (!isoSettings.Contains(SettingsPage.MogaR1Key))
                {
                    isoSettings[SettingsPage.MogaR1Key] = 8;
                }
                if (!isoSettings.Contains(SettingsPage.MogaR2Key))
                {
                    isoSettings[SettingsPage.MogaR2Key] = 8;
                }
                if (!isoSettings.Contains(SettingsPage.MogaLeftJoystickKey))
                {
                    isoSettings[SettingsPage.MogaLeftJoystickKey] = 16;
                }
                if (!isoSettings.Contains(SettingsPage.MogaRightJoystickKey))
                {
                    isoSettings[SettingsPage.MogaRightJoystickKey] = 16;
                }


                isoSettings.Save();

                settings.LowFrequencyMode = (bool)isoSettings[SettingsPage.LowFreqModeKey];
                settings.SoundEnabled = (bool)isoSettings[SettingsPage.EnableSoundKey];
                //settings.VirtualControllerOnTop = (bool)isoSettings[SettingsPage.VControllerPosKey];
                //settings.LowFrequencyModeMeasured = (bool)isoSettings[SettingsPage.LowFreqModeMeasuredKey];
                settings.Orientation = (int)isoSettings[SettingsPage.OrientationKey];
                settings.ControllerScale = (int)isoSettings[SettingsPage.ControllerScaleKey];
                settings.ButtonScale = (int)isoSettings[SettingsPage.ButtonScaleKey];
                settings.ControllerOpacity = (int)isoSettings[SettingsPage.OpacityKey];
                settings.AspectRatio = (AspectRatioMode)isoSettings[SettingsPage.AspectKey];
                settings.FrameSkip = (int)isoSettings[SettingsPage.SkipFramesKey];
                settings.ImageScaling = (int)isoSettings[SettingsPage.ImageScalingKey];
                settings.TurboFrameSkip = (int)isoSettings[SettingsPage.TurboFrameSkipKey];
                settings.SynchronizeAudio = (bool)isoSettings[SettingsPage.SyncAudioKey];
                settings.PowerFrameSkip = (int)isoSettings[SettingsPage.PowerSaverKey];
                settings.DPadStyle = (int)isoSettings[SettingsPage.DPadStyleKey];
                settings.Deadzone = (float)isoSettings[SettingsPage.DeadzoneKey];
                settings.CameraButtonAssignment = (int)isoSettings[SettingsPage.CameraAssignKey];
                settings.AutoIncrementSavestates = (bool)isoSettings[SettingsPage.AutoIncKey];
                settings.HideConfirmationDialogs = (bool)isoSettings[SettingsPage.ConfirmationKey];
                settings.HideLoadConfirmationDialogs = (bool)isoSettings[SettingsPage.ConfirmationLoadKey];
                settings.SelectLastState = (bool)isoSettings[SettingsPage.SelectLastState];
                settings.RestoreOldCheatValues = (bool)isoSettings[SettingsPage.RestoreCheatKey];
                settings.ManualSnapshots = (bool)isoSettings[SettingsPage.CreateManualSnapshotKey];
                settings.UseMogaController = (bool)isoSettings[SettingsPage.UseMogaControllerKey];
                settings.BgcolorR = (int)isoSettings[SettingsPage.BgcolorRKey];
                settings.BgcolorG = (int)isoSettings[SettingsPage.BgcolorGKey];
                settings.BgcolorB = (int)isoSettings[SettingsPage.BgcolorBKey];
                settings.AutoSaveLoad = (bool)isoSettings[SettingsPage.AutoSaveLoadKey];
                settings.VirtualControllerStyle = (int)isoSettings[SettingsPage.VirtualControllerStyleKey];

                settings.PadCenterXP = (int)isoSettings[SettingsPage.PadCenterXPKey];
                settings.PadCenterYP = (int)isoSettings[SettingsPage.PadCenterYPKey];
                settings.ALeftP = (int)isoSettings[SettingsPage.ALeftPKey];
                settings.ATopP = (int)isoSettings[SettingsPage.ATopPKey];
                settings.BLeftP = (int)isoSettings[SettingsPage.BLeftPKey];
                settings.BTopP = (int)isoSettings[SettingsPage.BTopPKey];
                settings.StartLeftP = (int)isoSettings[SettingsPage.StartLeftPKey];
                settings.StartTopP = (int)isoSettings[SettingsPage.StartTopPKey];
                settings.SelectRightP = (int)isoSettings[SettingsPage.SelectRightPKey];
                settings.SelectTopP = (int)isoSettings[SettingsPage.SelectTopPKey];
                settings.LLeftP = (int)isoSettings[SettingsPage.LLeftPKey];
                settings.LTopP = (int)isoSettings[SettingsPage.LTopPKey];
                settings.RRightP = (int)isoSettings[SettingsPage.RRightPKey];
                settings.RTopP = (int)isoSettings[SettingsPage.RTopPKey];


                settings.PadCenterXL = (int)isoSettings[SettingsPage.PadCenterXLKey];
                settings.PadCenterYL = (int)isoSettings[SettingsPage.PadCenterYLKey];
                settings.ALeftL = (int)isoSettings[SettingsPage.ALeftLKey];
                settings.ATopL = (int)isoSettings[SettingsPage.ATopLKey];
                settings.BLeftL = (int)isoSettings[SettingsPage.BLeftLKey];
                settings.BTopL = (int)isoSettings[SettingsPage.BTopLKey];
                settings.StartLeftL = (int)isoSettings[SettingsPage.StartLeftLKey];
                settings.StartTopL = (int)isoSettings[SettingsPage.StartTopLKey];
                settings.SelectRightL = (int)isoSettings[SettingsPage.SelectRightLKey];
                settings.SelectTopL = (int)isoSettings[SettingsPage.SelectTopLKey];
                settings.LLeftL = (int)isoSettings[SettingsPage.LLeftLKey];
                settings.LTopL = (int)isoSettings[SettingsPage.LTopLKey];
                settings.RRightL = (int)isoSettings[SettingsPage.RRightLKey];
                settings.RTopL = (int)isoSettings[SettingsPage.RTopLKey];

                settings.MogaA = (int)isoSettings[SettingsPage.MogaAKey];
                settings.MogaB = (int)isoSettings[SettingsPage.MogaBKey];
                settings.MogaX = (int)isoSettings[SettingsPage.MogaXKey];
                settings.MogaY = (int)isoSettings[SettingsPage.MogaYKey];
                settings.MogaL1 = (int)isoSettings[SettingsPage.MogaL1Key];
                settings.MogaL2 = (int)isoSettings[SettingsPage.MogaL2Key];
                settings.MogaR1 = (int)isoSettings[SettingsPage.MogaR1Key];
                settings.MogaR2 = (int)isoSettings[SettingsPage.MogaR2Key];
                settings.MogaLeftJoystick = (int)isoSettings[SettingsPage.MogaLeftJoystickKey];
                settings.MogaRightJoystick = (int)isoSettings[SettingsPage.MogaRightJoystickKey];

                settings.SettingsChanged = this.SettingsChangedDelegate;
            }
        }

        

        private void SettingsChangedDelegate()
        {
            EmulatorSettings settings = EmulatorSettings.Current;
            IsolatedStorageSettings isoSettings = IsolatedStorageSettings.ApplicationSettings;

            isoSettings[SettingsPage.EnableSoundKey] = settings.SoundEnabled;
            //isoSettings[SettingsPage.VControllerPosKey] = settings.VirtualControllerOnTop;
            isoSettings[SettingsPage.LowFreqModeKey] = settings.LowFrequencyMode;
            //isoSettings[SettingsPage.LowFreqModeMeasuredKey] = settings.LowFrequencyModeMeasured;
            isoSettings[SettingsPage.OrientationKey] = settings.Orientation;
            isoSettings[SettingsPage.ControllerScaleKey] = settings.ControllerScale;
            isoSettings[SettingsPage.ButtonScaleKey] = settings.ButtonScale;
            isoSettings[SettingsPage.OpacityKey] = settings.ControllerOpacity;
            isoSettings[SettingsPage.SkipFramesKey] = settings.FrameSkip;
            isoSettings[SettingsPage.AspectKey] = settings.AspectRatio;
            isoSettings[SettingsPage.ImageScalingKey] = settings.ImageScaling;
            isoSettings[SettingsPage.TurboFrameSkipKey] = settings.TurboFrameSkip;
            isoSettings[SettingsPage.SyncAudioKey] = settings.SynchronizeAudio;
            isoSettings[SettingsPage.PowerSaverKey] = settings.PowerFrameSkip;
            isoSettings[SettingsPage.DPadStyleKey] = settings.DPadStyle;
            isoSettings[SettingsPage.DeadzoneKey] = settings.Deadzone;
            isoSettings[SettingsPage.CameraAssignKey] = settings.CameraButtonAssignment;
            isoSettings[SettingsPage.ConfirmationKey] = settings.HideConfirmationDialogs;
            isoSettings[SettingsPage.AutoIncKey] = settings.AutoIncrementSavestates;
            isoSettings[SettingsPage.ConfirmationLoadKey] = settings.HideLoadConfirmationDialogs;
            isoSettings[SettingsPage.SelectLastState] = settings.SelectLastState;
            isoSettings[SettingsPage.RestoreCheatKey] = settings.RestoreOldCheatValues;
            isoSettings[SettingsPage.CreateManualSnapshotKey] = settings.ManualSnapshots;
            isoSettings[SettingsPage.UseMogaControllerKey] = settings.UseMogaController;
            isoSettings[SettingsPage.BgcolorRKey] = settings.BgcolorR;
            isoSettings[SettingsPage.BgcolorGKey] = settings.BgcolorG;
            isoSettings[SettingsPage.BgcolorBKey] = settings.BgcolorB;
            isoSettings[SettingsPage.AutoSaveLoadKey] = settings.AutoSaveLoad;
            isoSettings[SettingsPage.VirtualControllerStyleKey] = settings.VirtualControllerStyle;

            isoSettings.Save();
        }

        private void RefreshRecentROMList()
        {
            

            //StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            //StorageFolder romFolder = await localFolder.CreateFolderAsync(ROM_DIRECTORY, CreationCollisionOption.OpenIfExists);
            //IReadOnlyList<StorageFile> roms = await romFolder.GetFilesAsync();
            //IList<ROMEntry> romNames = new List<ROMEntry>(roms.Count);
            //foreach (var file in roms)
            //{
            //    romNames.Add(new ROMEntry() { Name = file.Name } );
            //}
            this.lastRomImage.DataContext = ROMDatabase.Current.GetLastPlayed();

            if (this.lastRomImage.DataContext != null)
                this.resumeButton.IsEnabled = true;
            else
                this.resumeButton.IsEnabled = false;

            if (this.lastRomImage.DataContext != null && App.metroSettings.ShowLastPlayedGame == true)
                lastRomGrid.Visibility = Visibility.Visible;
            else
                lastRomGrid.Visibility = Visibility.Collapsed;

            //this.romList.ItemsSource = ROMDatabase.Current.GetROMList();

            this.recentList.ItemsSource = ROMDatabase.Current.GetRecentlyPlayed();
         
        }







        private void romList_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {

            this.StartROMFromList(this.romList);
        }

        private void recentList_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            this.StartROMFromList(this.recentList);
        }

        private async void StartROMFromList(ListBox list)
        {
            if (list.SelectedItem == null)
                return;

            ROMDBEntry entry = (ROMDBEntry)list.SelectedItem;
            list.SelectedItem = null;

            await StartROM(entry);
        }

        private async Task StartROM(ROMDBEntry entry)
        {
            if (entry.AutoLoadLastState == false)
                EmulatorPage.ROMLoaded = false;  //force reloading of ROM after reimport save file

            EmulatorPage.currentROMEntry = entry;
            LoadROMParameter param = await FileHandler.GetROMFileToPlayAsync(entry.FileName);

            //entry.LastPlayed = DateTime.Now;
            //this.db.CommitChanges();

            this.checkAutoUpload = true;


            PhoneApplicationService.Current.State["parameter"] = param;
            this.NavigationService.Navigate(new Uri("/EmulatorPage.xaml", UriKind.Relative));
        }

        private void InitAppBar()
        {
            ApplicationBar = new ApplicationBar();
            ApplicationBar.IsMenuEnabled = true;
            ApplicationBar.BackgroundColor = (Color)App.Current.Resources["CustomChromeColor"];
            ApplicationBar.ForegroundColor = (Color)App.Current.Resources["CustomForegroundColor"];
            //ApplicationBar.Mode = ApplicationBarMode.Minimized;

            var helpButton = new ApplicationBarMenuItem(AppResources.HelpButtonText);
            helpButton.Click += helpButton_Click;
            ApplicationBar.MenuItems.Add(helpButton);

            var aboutItem = new ApplicationBarMenuItem(AppResources.aboutText);
            aboutItem.Click += aboutItem_Click;
            ApplicationBar.MenuItems.Add(aboutItem);

            resumeButton = new ApplicationBarIconButton(new Uri("/Assets/Icons/transport.play.png", UriKind.Relative))
            {
                Text = AppResources.ResumeButtonText,
                IsEnabled = false
            };
            resumeButton.Click += resumeButton_Click;
            ApplicationBar.Buttons.Add(resumeButton);



            var settingsButton = new ApplicationBarIconButton(new Uri("/Assets/Icons/feature.settings.png", UriKind.Relative))
            {
                Text = AppResources.SettingsButtonText
            };
            settingsButton.Click += settingsButton_Click;
            ApplicationBar.Buttons.Add(settingsButton);


            var purchaseButton = new ApplicationBarIconButton(new Uri("/Assets/Icons/marketplace.png", UriKind.Relative))
            {
                Text = AppResources.PurchaseText
            };
            purchaseButton.Click += purchaseButton_Click;
            ApplicationBar.Buttons.Add(purchaseButton);

            var reviewButton = new ApplicationBarIconButton(new Uri("/Assets/Icons/social.like.png", UriKind.Relative))
            {
                Text = AppResources.ReviewText
            };
            reviewButton.Click += reviewButton_Click;
            ApplicationBar.Buttons.Add(reviewButton);
        }



        private void purchaseButton_Click(object sender, EventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/PurchasePage.xaml", UriKind.Relative));
        }

        private void reviewButton_Click(object sender, EventArgs e)
        {
            MarketplaceReviewTask marketplaceReviewTask = new MarketplaceReviewTask();

            marketplaceReviewTask.Show();
        }

        void helpButton_Click(object sender, EventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/HelpPage.xaml", UriKind.Relative));
        }

        void aboutItem_Click(object sender, EventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/AboutPage.xaml", UriKind.Relative));

            
        }

        async void resumeButton_Click(object sender, EventArgs e)
        {
            var entry = ROMDatabase.Current.GetLastPlayed();

            await StartROM(entry);
            this.romList.SelectedItem = null;


        }

        void settingsButton_Click(object sender, EventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        //void importButton_Click(object sender, EventArgs e)
        //{
        //    this.NavigationService.Navigate(new Uri("/ImportPage.xaml", UriKind.Relative));
        //}

        private void gotoBackupButton_Click_1(object sender, RoutedEventArgs e)
        {

            if (App.session != null)
            {
                PhoneApplicationService.Current.State["parameter"] = App.session;
                BackupPage.backupMedium = "onedrive";
                this.NavigationService.Navigate(new Uri("/BackupPage.xaml", UriKind.Relative));
            }
            else
            {
                MessageBox.Show(AppResources.NotSignedInError, AppResources.ErrorCaption, MessageBoxButton.OK);
            }

        }

        private void gotoRestoreButton_Click_1(object sender, RoutedEventArgs e)
        {
            if (!App.IsTrial)
            {
                if (App.session != null)
                {
                    PhoneApplicationService.Current.State["parameter"] = App.session;
                    this.NavigationService.Navigate(new Uri("/RestorePage.xaml", UriKind.Relative));
                }
                else
                {
                    MessageBox.Show(AppResources.NotSignedInError, AppResources.ErrorCaption, MessageBoxButton.OK);
                }
            }
            else
            {
                ShowBuyDialog();
            }
        }

        void ShowBuyDialog()
        {
            ShowDialog(AppResources.BuyNowText);
        }

        void ShowBuyImportDialog()
        {
            ShowDialog(AppResources.BuyNowImportText);
        }

        private static void ShowDialog(String text)
        {

            var result = MessageBox.Show(text, AppResources.InfoCaption, MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                MarketplaceDetailTask marketplaceDetailTask = new MarketplaceDetailTask();
                marketplaceDetailTask.ContentType = MarketplaceContentType.Applications;
#if !GBC
                marketplaceDetailTask.ContentIdentifier = "4e3142c4-b99c-4075-bedc-b10a3086327d";
#else
                marketplaceDetailTask.ContentIdentifier = "be33ce3e-e519-4d2c-b30e-83347601ed57";                    
#endif
                marketplaceDetailTask.Show();
            }
        }

        //private void StackPanel_Hold(object sender, System.Windows.Input.GestureEventArgs e)
        //{
        //    ROMDBEntry entry = (sender as Grid).DataContext as ROMDBEntry;
        //    PhoneApplicationService.Current.State["parameter"] = entry;

        //    this.NavigationService.Navigate(new Uri("/ContextPage.xaml", UriKind.Relative));
        //}

        private void ImportSD_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/SDCardImportPage.xaml", UriKind.Relative));
        }



        private void TextBlock_Tap_1(object sender, System.Windows.Input.GestureEventArgs e)
        {
            WebBrowserTask wbtask = new WebBrowserTask();
            wbtask.Uri = new Uri("http://www.youtube.com/watch?v=YfqzZhcr__o");
            wbtask.Show();
        }

        private void TextBlock_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            WebBrowserTask wbtask = new WebBrowserTask();
            wbtask.Uri = new Uri("http://www.youtube.com/watch?v=3WopTRM4ets");
            wbtask.Show();
        }

        private void TextBlock_Tap_2(object sender, System.Windows.Input.GestureEventArgs e)
        {
            WebBrowserTask wbtask = new WebBrowserTask();
            wbtask.Uri = new Uri("http://forums.wpcentral.com/showthread.php?t=252987");
            wbtask.Show();
        }

        private void contactBlock_Tap_1(object sender, System.Windows.Input.GestureEventArgs e)
        {
            EmailComposeTask emailcomposer = new EmailComposeTask();

            emailcomposer.To = AppResources.AboutContact;
            emailcomposer.Subject = AppResources.EmailSubjectText;
            emailcomposer.Body = String.Format( AppResources.EmailBodyText, Microsoft.Phone.Info.DeviceStatus.DeviceName);
            emailcomposer.Show();
        }

        private void TextBlock_Tap_3(object sender, System.Windows.Input.GestureEventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/HelpPage.xaml", UriKind.Relative));
        }

        private void TextBlock_Tap_4(object sender, System.Windows.Input.GestureEventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/HelpPage.xaml?index=1", UriKind.Relative));
        }

        private void TextBlock_Tap_5(object sender, System.Windows.Input.GestureEventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/HelpPage.xaml?index=2", UriKind.Relative));
        }

        private async void Image_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ROMDBEntry entry = (ROMDBEntry)this.lastRomImage.DataContext;
            await StartROM(entry);
        }




        private void pinBlock_Tap(object sender, ContextMenuItemSelectedEventArgs e)
        {
            if (!App.IsTrial)
            {
                try
                {
                    var menuItem = sender as RadContextMenuItem;
                    var fe = VisualTreeHelper.GetParent(menuItem) as FrameworkElement;
                    ROMDBEntry entry = fe.DataContext as ROMDBEntry;


                    FileHandler.CreateROMTile(entry);
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show(AppResources.MaximumTilesPinned);
                }
            }
            else
            {
                ShowBuyDialog();
            }
        }


        private void renameBlock_Tap(object sender, ContextMenuItemSelectedEventArgs e)
        {
            var menuItem = sender as RadContextMenuItem;
            var fe = VisualTreeHelper.GetParent(menuItem) as FrameworkElement;
            ROMDBEntry entry = fe.DataContext as ROMDBEntry;

            PhoneApplicationService.Current.State["parameter"] = entry;
            PhoneApplicationService.Current.State["parameter2"] = ROMDatabase.Current;

            this.NavigationService.Navigate(new Uri("/RenamePage.xaml", UriKind.Relative));
        }


        private void cheatBlock_Tap(object sender, ContextMenuItemSelectedEventArgs e)
        {
            if (!App.IsTrial)
            {
                var menuItem = sender as RadContextMenuItem;
                var fe = VisualTreeHelper.GetParent(menuItem) as FrameworkElement;
                ROMDBEntry entry = fe.DataContext as ROMDBEntry;

                PhoneApplicationService.Current.State["parameter"] = entry;
                this.NavigationService.Navigate(new Uri("/CheatPage.xaml", UriKind.Relative));
            }
            else
            {
                ShowBuyDialog();
            }
        }

        private void deleteManageBlock_Tap(object sender, ContextMenuItemSelectedEventArgs e)
        {
            var menuItem = sender as RadContextMenuItem;
            var fe = VisualTreeHelper.GetParent(menuItem) as FrameworkElement;
            ROMDBEntry entry = fe.DataContext as ROMDBEntry;

            PhoneApplicationService.Current.State["parameter"] = entry;
            this.NavigationService.Navigate(new Uri("/ManageSavestatePage.xaml", UriKind.Relative));
        }

        private async void deleteSavesBlock_Tap(object sender, ContextMenuItemSelectedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(AppResources.DeleteConfirmText, AppResources.DeleteConfirmTitle, MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.Cancel)
                return;


            var menuItem = sender as RadContextMenuItem;
            var fe = VisualTreeHelper.GetParent(menuItem) as FrameworkElement;

            ROMDBEntry entry = fe.DataContext as ROMDBEntry;

            await FileHandler.DeleteSRAMFile(entry);

            //CustomMessageBox msgbox = new CustomMessageBox();
            //msgbox.Background = (SolidColorBrush)App.Current.Resources["PhoneChromeBrush"];
            //msgbox.Foreground = (SolidColorBrush)App.Current.Resources["PhoneForegroundBrush"];
            //msgbox.Message = AppResources.SRAMDeletedSuccessfully;
            //msgbox.Caption = AppResources.InfoCaption;
            //msgbox.LeftButtonContent = "OK";
            //msgbox.Show();
            MessageBox.Show(AppResources.SRAMDeletedSuccessfully, AppResources.InfoCaption, MessageBoxButton.OK);
        }

        private async void deleteBlock_Tap(object sender, ContextMenuItemSelectedEventArgs e)
        {
            try
            {
                MessageBoxResult result = MessageBox.Show(AppResources.DeleteConfirmText, AppResources.DeleteConfirmTitle, MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.Cancel)
                    return;

                var menuItem = sender as RadContextMenuItem;
                var fe = VisualTreeHelper.GetParent(menuItem) as FrameworkElement;

                ROMDBEntry entry = fe.DataContext as ROMDBEntry;
                await FileHandler.DeleteROMAsync(entry);

                this.RefreshRecentROMList();

            }
            catch (System.IO.FileNotFoundException)
            { }
        }


        private void test_Tapped(object sender, ContextMenuItemSelectedEventArgs e)
        {
            var menuItem = sender as RadContextMenuItem;
            var fe = VisualTreeHelper.GetParent(menuItem) as FrameworkElement;

            ROMDBEntry entry = fe.DataContext as ROMDBEntry;
        }

        private void contactBlock_Tap_2(object sender, System.Windows.Input.GestureEventArgs e)
        {
            WebBrowserTask wbtask = new WebBrowserTask();
            wbtask.Uri = new Uri("https://twitter.com/wp8emu");
            wbtask.Show();

        }

        private void CloudSixImportButton_Click(object sender, RoutedEventArgs e)
        {
#if GBC
            var launcher = new CloudSixConnector.FilePicker.CloudSixPicker("cloudsix2vgbc8");
#elif BETA
            var launcher = new CloudSixConnector.FilePicker.CloudSixPicker("cloudsix2vba8beta");
#else
            var launcher = new CloudSixConnector.FilePicker.CloudSixPicker("cloudsix2vba8");
#endif
            launcher.Token = "FromCloudSix";
            launcher.Caption = ".gb, .gbc, .gba, .sgm, .sav, .zip, .rar, .7z";
            launcher.FileExtensions.Add(new CloudSixFileExtension() { Extension = "gb" });
            launcher.FileExtensions.Add(new CloudSixFileExtension() { Extension = "gbc" });
            launcher.FileExtensions.Add(new CloudSixFileExtension() { Extension = "gba"});
            launcher.FileExtensions.Add(new CloudSixFileExtension() { Extension = "sgm" });
            launcher.FileExtensions.Add(new CloudSixFileExtension() { Extension = "sav" });
            launcher.FileExtensions.Add(new CloudSixFileExtension() { Extension = "zip" });
            launcher.FileExtensions.Add(new CloudSixFileExtension() { Extension = "zib" });
            launcher.FileExtensions.Add(new CloudSixFileExtension() { Extension = "rar" });
            launcher.FileExtensions.Add(new CloudSixFileExtension() { Extension = "7z" });
            launcher.Show();

        }

        private void CloudSixExportButton_Click(object sender, RoutedEventArgs e)
        {
            BackupPage.backupMedium = "cloudsix";
            this.NavigationService.Navigate(new Uri("/BackupPage.xaml", UriKind.Relative));

            //var saver = new CloudSixSaver("df.dd", )
        }





    } //end MainPage class


    class ROMEntry
    {
        public String Name { get; set; }
    }

    class LoadROMParameter
    {
        public StorageFile file;
        public StorageFolder folder;
    }

    // add these 3 lines  
    public class TiltableGrid : Grid
    {
    }

    public class TiltableCanvas : Canvas
    {
    }  



}