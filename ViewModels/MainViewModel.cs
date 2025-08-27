using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using TagLib;
using Y1_ingester.Models;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace Y1_ingester.ViewModels
{
    internal partial class MainViewModel : ObservableObject
    {

        YoutubeDL ytdl = new();
        public ObservableCollection<SongModel> Songs { get; } = new();

        private readonly string musicFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Music");

        [ObservableProperty]
        private ObservableCollection<string> rockboxDrives = new();

        [ObservableProperty]
        private string selectedRockboxDrive = "None";

        [ObservableProperty]
        private string urlText;

        [ObservableProperty]
        private ObservableCollection<QueuedSongModel> queuedSongs = new();

        private readonly BlockingCollection<QueuedSongModel> _downloadQueue = new();

        private readonly CancellationTokenSource _cts = new();

        public ObservableCollection<SongModel> RockboxSongs { get; } = new();

        [ObservableProperty]
        public bool isRockBoxConnected;

        public MainViewModel()
        {
            if (!Directory.Exists(musicFolder))
                Directory.CreateDirectory(musicFolder);

            LoadSongs();

            Task.Run(() => ProcessQueueAsync(_cts.Token));

            var rockboxDrive = RockboxHelper.DetectRockboxDrive();
            RockboxDrives.Add("None");
            if (rockboxDrive != null)
            {
                RockboxDrives.Add(rockboxDrive);
            }
            SelectedRockboxDrive = RockboxDrives[0];

            //When selectedrockbox drive changes, load songs from it
            PropertyChanged += (s, e) => { 
                if (e.PropertyName == nameof(SelectedRockboxDrive))
                {
                    //Check if rockbox is connected
                    IsRockBoxConnected = SelectedRockboxDrive != "None";
                    if (!IsRockBoxConnected)
                    {
                        MessageBox.Show("Rockbox drive disconnected.");
                    }

                    RockboxSongs.Clear();

                    var driveSongs = RockboxHelper.LoadRockboxSongs(SelectedRockboxDrive);

                    foreach (var song in driveSongs)
                    {
                        Console.WriteLine("Found song on Rockbox: " + song.Title);
                        RockboxSongs.Add(song);
                    }
                }
            };
        }

        [RelayCommand]
        private void RefreshSongs()
        {
            LoadSongs();

            RockboxDrives.Clear();
            var rockboxDrive = RockboxHelper.DetectRockboxDrive();
            RockboxDrives.Add("None");
            if (rockboxDrive != null)
            {
                RockboxDrives.Add(rockboxDrive);
            }
        }

        private void LoadSongs()
        {
            Songs.Clear();

            var files = Directory.GetFiles(musicFolder, "*.mp3", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);

                    BitmapImage albumArt = null;
                    if (tagFile.Tag.Pictures.Length > 0)
                    {
                        using (var ms = new MemoryStream(tagFile.Tag.Pictures[0].Data.Data))
                        {
                            albumArt = new BitmapImage();
                            albumArt.BeginInit();
                            albumArt.CacheOption = BitmapCacheOption.OnLoad;
                            albumArt.StreamSource = ms;
                            albumArt.EndInit();
                        }
                    }
                    Console.WriteLine("Loaded song: " + (tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(file)));
                    Console.WriteLine("Is on Rockbox: " + (RockboxSongs.Any(r => r.Title == tagFile.Tag.Title) ? "Yes" : "No"));
                    var song = new SongModel
                    {
                        FilePath = file,
                        Title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(file),
                        Artist = tagFile.Tag.FirstPerformer ?? "Unknown",
                        Album = tagFile.Tag.Album ?? "Unknown",
                        AlbumArt = albumArt,
                        Year = tagFile.Tag.Year > 0 ? tagFile.Tag.Year.ToString() : "Unknown",
                        IsOnRockbox = RockboxSongs.Any(r => r.Title == tagFile.Tag.Title)
                    };

                    Songs.Add(song);
                } catch
                {
                    // Ignore invalid or corrupted files
                }
            }
        }

        [RelayCommand]
        async private Task DownloadUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Please enter a valid URL.");
                return;
            }

            Console.WriteLine("Downloading...");

            await DownloaddHelper.DownloadTools();
            ytdl.YoutubeDLPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yt-dlp.exe");
            ytdl.FFmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg.exe");

            ytdl.OutputFolder = musicFolder;

            var queuedItem = new QueuedSongModel { Url = url };
            QueuedSongs.Add(queuedItem);

            _downloadQueue.Add(queuedItem);
            UrlText = string.Empty;
            return;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _downloadQueue.CompleteAdding();
        }

        private async Task ProcessQueueAsync(CancellationToken token)
        {
            foreach (var queuedItem in _downloadQueue.GetConsumingEnumerable(token))
            {
                try
                {
                    queuedItem.Status = "Fetching info…";

                    var infoResult = await ytdl.RunVideoDataFetch(queuedItem.Url);
                    if (!infoResult.Success)
                    {
                        queuedItem.Status = "Failed (metadata)";
                        continue;
                    }

                    var info = infoResult.Data;
                    queuedItem.Title = info.Title ?? queuedItem.Url;
                    queuedItem.Status = "Downloading…";

                    var downloadResult = await ytdl.RunAudioDownload(queuedItem.Url, AudioConversionFormat.Mp3);
                    if (!downloadResult.Success)
                    {
                        queuedItem.Status = "Failed (download)";
                        continue;
                    }

                    string downloadedFile = Path.Combine(musicFolder, downloadResult.Data);

                    // Tag file
                    var tagFile = TagLib.File.Create(downloadedFile);
                    tagFile.Tag.Title = info.Title ?? Path.GetFileNameWithoutExtension(downloadedFile);
                    tagFile.Tag.Album = info.Album ?? "Youtube Music";
                    tagFile.Tag.Performers = !string.IsNullOrEmpty(info.Uploader) ? new[] { info.Uploader } : new[] { "Unknown" };
                    tagFile.Tag.Year = (uint)(info.UploadDate?.Year ?? DateTime.Now.Year);

                    if (!string.IsNullOrEmpty(info.Thumbnail))
                    {
                        using var wc = new System.Net.WebClient();
                        var imageData = wc.DownloadData(info.Thumbnail);
                        var pic = new Picture(new ByteVector(imageData));
                        tagFile.Tag.Pictures = new IPicture[] { pic };
                    }

                    tagFile.Save();

                    queuedItem.Status = "Completed";

                    Application.Current.Dispatcher.Invoke(LoadSongs);
                } catch (Exception ex)
                {
                    queuedItem.Status = "Failed (" + ex.Message + ")";
                }
            }
        }

        [RelayCommand]
        public void UploadSongs()
        {
            if (!IsRockBoxConnected || SelectedRockboxDrive == "None")
            {
                MessageBox.Show("No Rockbox drive selected.");
                return;
            }
            List<string> fileNames = new();
            foreach (var song in Songs)
            {
                if (!song.IsOnRockbox)
                {
                    fileNames.Add(song.FilePath);
                }
            }
            RockboxHelper.UploadMultipleMp3(SelectedRockboxDrive, fileNames);
            Console.WriteLine("Uploaded " + fileNames.Count + " songs to Rockbox.");
        }

    }
}