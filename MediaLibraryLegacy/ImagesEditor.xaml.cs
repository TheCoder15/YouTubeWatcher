﻿using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.ObjectModel;
using VideoEffects;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.Effects;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace MediaLibraryLegacy
{
    public sealed partial class ImagesEditor : UserControl
    {
        private ObservableCollection<ViewImageEditorMetadata> snapshots;

        public ImagesEditor()
        {
            this.InitializeComponent();
        }
        public void InitialSetup() {
            BindDatasources();
            LoadVideo();
        }

        private void BindDatasources() {
            snapshots = new ObservableCollection<ViewImageEditorMetadata>();
            icLibraryItems.ItemsSource = snapshots;
        }

        private async void LoadVideo() {
            if (DataContext is ViewMediaMetadata) {
                var viewMediaMetadata = (ViewMediaMetadata)DataContext;
                var mediaUri = new Uri($"{App.mediaPath}\\{viewMediaMetadata.YID}.{viewMediaMetadata.MediaType}", UriKind.Absolute);

                var videoFile = await StorageFile.GetFileFromPathAsync(mediaUri.OriginalString);
                MediaClip clip = await MediaClip.CreateFromFileAsync(videoFile);

                var videoEffectDefinition = new VideoEffectDefinition(typeof(SnapshotVidoEffect).FullName);
                clip.VideoEffectDefinitions.Add(videoEffectDefinition);

                MediaComposition compositor = new MediaComposition();
                compositor.Clips.Add(clip);
                mePlayer.SetMediaStreamSource(compositor.GenerateMediaStreamSource());
            }
        }

        private async void TakeSnapshot(object sender, RoutedEventArgs e)
        {
            var bitmap = SnapshotVidoEffect.GetSnapShot();

            if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || bitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
            {
                bitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(bitmap);

            var newSnapshot = new ViewImageEditorMetadata();
            newSnapshot.Bitmap = bitmap;
            newSnapshot.Source = source;
            newSnapshot.Number = snapshots.Count + 1;
            var pos = mePlayer.Position;
            newSnapshot.Position = new TimeSpan(pos.Days, pos.Hours, pos.Minutes, pos.Seconds); ;

            snapshots.Add(newSnapshot);
        }

        private async void SaveSoftwareBitmapToFile(SoftwareBitmap softwareBitmap, StorageFile outputFile)
        {
            using (IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                // Set properties
                var propertySet = new BitmapPropertySet();
                var qualityValue = new BitmapTypedValue(
                    1.0, // Maximum quality
                    Windows.Foundation.PropertyType.Single
                    );
                propertySet.Add("ImageQuality", qualityValue);

                // Create an encoder with the desired format
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, propertySet);

                // Set the software bitmap
                encoder.SetSoftwareBitmap(softwareBitmap);

                //// Set additional encoding parameters, if needed
                //encoder.BitmapTransform.ScaledWidth = 320;
                //encoder.BitmapTransform.ScaledHeight = 240;
                //encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees;
                //encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                encoder.IsThumbnailGenerated = true;

                try
                {
                    await encoder.FlushAsync();
                }
                catch (Exception err)
                {
                    const int WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982F81);
                    switch (err.HResult)
                    {
                        case WINCODEC_ERR_UNSUPPORTEDOPERATION:
                            // If the encoder does not support writing a thumbnail, then try again
                            // but disable thumbnail generation.
                            encoder.IsThumbnailGenerated = false;
                            break;
                        default:
                            throw;
                    }
                }

                //throwing error?
                if (encoder.IsThumbnailGenerated == false)
                {
                    await encoder.FlushAsync();
                }
            }
        }

        private void DeleteSnapshot(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ViewImageEditorMetadata)
            {
                var viewImageEditorMetadata = (ViewImageEditorMetadata)((FrameworkElement)sender).DataContext;
                snapshots.Remove(viewImageEditorMetadata);
                UpdateSnapshotNumbers();
            }
        }

        private void UpdateSnapshotNumbers() {
            for (int i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                snapshot.Number = i + 1;
                snapshots[i] = snapshot;
            }
        }

        private void TabChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var tvi = (TabViewItem)e.AddedItems[0];
                switch (tvi.Header)
                {
                    case "Image Editor":
                        grdImageEditor.Visibility = Visibility.Visible;
                        grdMediaEditor.Visibility = Visibility.Collapsed;
                        mePlayer.Pause();
                        break;
                    case "Video Editor":
                        grdImageEditor.Visibility = Visibility.Collapsed;
                        grdMediaEditor.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private async void SaveSnapshots(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewMediaMetadata)
            {
                var viewMediaMetadata = (ViewMediaMetadata)DataContext;

                var mediaPathFolder = await StorageFolder.GetFolderFromPathAsync(App.mediaPath);
                if (mediaPathFolder != null)
                {
                    // if folder exists delete it 
                    await StorageHelper.TryDeleteFolder(viewMediaMetadata.YID, mediaPathFolder);
                    // create folder 
                    var yidFolder = await mediaPathFolder.CreateFolderAsync(viewMediaMetadata.YID);

                    // save each snapshot as an image into the new folder
                    foreach (var snapshot in snapshots) {
                        var newSnapshotFile = await yidFolder.CreateFileAsync($"{viewMediaMetadata.YID}-{snapshot.Number}.png");

                        SaveSoftwareBitmapToFile(snapshot.Bitmap, newSnapshotFile);
                    }

                    // create DB record for each snapshot    

                }

            }

        }

        private void SnapshotSelected(object sender, PointerRoutedEventArgs e)
        {
            var viewImageEditorMetadata = (ViewImageEditorMetadata)((Image)sender).DataContext;
            grdImageEditor.DataContext = viewImageEditorMetadata;

            tvMain.SelectedIndex = 0;
        }
    }
}

// software bitmap
// https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/imaging
