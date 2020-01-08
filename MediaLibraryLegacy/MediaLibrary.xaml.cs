﻿using MediaLibraryLegacy.Controls;
using Microsoft.Toolkit.Uwp.UI.Controls;
using SharedCode.SQLite;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace MediaLibraryLegacy
{
    public sealed partial class MediaLibrary : UserControl
    {
        string mediaPath;

        public event EventHandler<PlayMediaEventArgs> OnPlayMedia;
        public event EventHandler OnMediaDeleted;
        public event EventHandler OnMediaAddedToPlaylist;
        public event EventHandler OnUrlCopiedToClipboard;
        public event EventHandler<LaunchUrlEventArgs> OnOpenUrl;

        public MediaLibrary()
        {
            this.InitializeComponent();
        }

        public void InitialSetup(string mediapath)
        {
            mediaPath = mediapath;
            Show();
            tbMediaDirectory.Text = mediapath;
        }

        private void ShowMediaFolder(object sender, RoutedEventArgs e) => OpenMediaFolder();

        private async void OpenMediaFolder()
        {
            StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(mediaPath);
            await Launcher.LaunchFolderAsync(folder);
        }

        public void Show()
        {
            //wvMain.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            grdLibrary.Visibility = Visibility.Visible;
            LoadLibraryItems(true);
            //ShowHideMediaPlayer(false);
        }

        public void Hide()
        {
            //wvMain.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            grdLibrary.Visibility = Visibility.Collapsed;
            LoadLibraryItems(false);
            //ShowHideMediaPlayer(false);
        }

        private void LoadLibraryItems(bool load)
        {
            if (load)
            {
                icLibraryItems.ItemsSource = EntitiesHelper.RetrieveMediaMetadataAsViewCollection(mediaPath);
            }
            else
            {
                //icLibraryItems.Items.Clear();
                icLibraryItems.ItemsSource = null;
            }
        }

        private void PlayMedia(object sender, RoutedEventArgs e)
        {
            var but = sender as Button;
            if (but.DataContext is ViewMediaMetadata)
            {
                var vmd = (ViewMediaMetadata)but.DataContext;
                OnPlayMedia?.Invoke(null, new PlayMediaEventArgs() { ViewMediaMetadata = vmd });
            }
        }

        private void OnPlaylistSelected(object sender, EventArgs e)
        {
            XamlHelper.CloseFlyout(sender);
            if (e is PlaylistSelectedEventArgs && sender is FrameworkElement)
            {
                var uie = (FrameworkElement)sender;
                if (uie != null && uie.DataContext is ViewMediaMetadata) {
                    var playlistSelectedEventArgs = (PlaylistSelectedEventArgs)e;
                    var viewMediaMetadata = (ViewMediaMetadata)uie.DataContext;
                    EntitiesHelper.AddPlaylistMediaMetadata(viewMediaMetadata.UniqueId, playlistSelectedEventArgs.SelectedPlaylist.UniqueId);
                    OnMediaAddedToPlaylist?.Invoke(null, null);
                }
            }
        }

        private async void ExtraToolsSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var item = (ListBoxItem)e.AddedItems[0];
                var viewMediaMetadata = (ViewMediaMetadata)item.DataContext;
                switch (item.Content)
                {
                    case "Delete Media": 
                        await DeleteMedia(viewMediaMetadata.YID, viewMediaMetadata.MediaType);
                        OnMediaDeleted?.Invoke(null, null);
                        break;
                    case "Open LiveTile Editor": OpenImagesEditor(viewMediaMetadata); break;
                    case "Pin to Start": break;
                    case "Open in YouTube": OnOpenUrl.Invoke(null, new LaunchUrlEventArgs() { Url = $"{App.youtubeHomeUrl}/watch?v={viewMediaMetadata.YID}" }); break;
                    case "Copy URL to Clipboard": CopyUrlToClipboard($"{App.youtubeHomeUrl}/watch?v={viewMediaMetadata.YID}"); break;
                }
            }
            if (sender is ListBox) {
                ((ListBox)sender).SelectedIndex = -1;
            }
            XamlHelper.CloseFlyout(sender);
        }

        private void CopyUrlToClipboard(string url) {
            var dataPackage = new DataPackage();
            dataPackage.SetText(url);
            Clipboard.SetContent(dataPackage);
            OnUrlCopiedToClipboard?.Invoke(null, null);
        }

        private void OpenImagesEditor(object viewModel) {
            var windowContent = new ImagesEditor();
            windowContent.DataContext = viewModel;
            WindowHelper.OpenWindow(windowContent, WindowHelper.DefaultEditorWindowWidth, WindowHelper.DefaultEditorWindowHeight, ()=> { windowContent.InitialSetup(); });            
        }

        private async Task DeleteMedia(string yid, string fileType) {
            // use YId as the key for deleting
            var mediaPathFolder = await StorageFolder.GetFolderFromPathAsync(mediaPath);
            if (mediaPathFolder != null) {

                // get extra content folder if it exists & delete it
                await StorageHelper.TryDeleteFolder(yid, mediaPathFolder);
                
                // delete root images
                await StorageHelper.TryDeleteFile($"{yid}-high.jpg", mediaPathFolder);
                await StorageHelper.TryDeleteFile($"{yid}-medium.jpg", mediaPathFolder);
                await StorageHelper.TryDeleteFile($"{yid}-low.jpg", mediaPathFolder);
                await StorageHelper.TryDeleteFile($"{yid}-max.jpg", mediaPathFolder);
                await StorageHelper.TryDeleteFile($"{yid}-standard.jpg", mediaPathFolder);

                // delete root mp3/mp4
                await StorageHelper.TryDeleteFile($"{yid}.{fileType}", mediaPathFolder);
            }

            // delete DB data
            EntitiesHelper.DeleteAllByYID(yid);
        }

    }

    public class PlayMediaEventArgs : EventArgs {
        public ViewMediaMetadata ViewMediaMetadata { get; set; }
    }

    public class LaunchUrlEventArgs : EventArgs
    {
        public string Url { get; set; }
    }
    
}

// https://docs.microsoft.com/en-us/windows/uwp/design/layout/app-window