using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TagLib;
using Y1_ingester.Models;
using Y1_ingester.Utils;
using Y1_ingester.Utils.Services;
using Y1_ingester.Views;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace Y1_ingester.ViewModels
{
    internal partial class MainViewModel : ObservableObject
    {
        private readonly SongLibraryService _songLibrary;
        private readonly DownloadQueueService _downloadQueue;
        private readonly RockboxService _rockboxService;

        public ObservableCollection<SongModel> Songs { get; } = new();
        public ObservableCollection<QueuedSongModel> QueuedSongs { get; } = new();
        public ObservableCollection<SongModel> RockboxSongs { get; } = new();

        [ObservableProperty] private ObservableCollection<string> rockboxDrives = new();
        [ObservableProperty] private string selectedRockboxDrive = "None";
        [ObservableProperty] private string urlText = String.Empty;
        [ObservableProperty] private bool isRockBoxConnected = false;

        public List<string> FilterOptions { get; } = new() { "All", "Only Local", "Only On Rockbox" };

        [ObservableProperty]
        public string selectedFilter = "All";

        public List<string> SortOptions { get; } = new() { "Title", "Artist", "Album", "Year" };

        [ObservableProperty]
        private string selectedSort = "Title";

        public ICollectionView SongsView { get; }

        public MainViewModel()
        {
            string musicFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Music");
            _songLibrary = new SongLibraryService(musicFolder);
            _downloadQueue = new DownloadQueueService(musicFolder);
            _rockboxService = new RockboxService();

            _downloadQueue.SongsChanged += () => RefreshSongs();

            RefreshDevices();
            RefreshSongs();

            SongsView = CollectionViewSource.GetDefaultView(Songs);
            SongsView.Filter = FilterSongs;

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedRockboxDrive))
                {
                    UpdateRockBox();
                }
                else if (e.PropertyName == nameof(SelectedFilter))
                {
                    SongsView?.Refresh();
                } else if (e.PropertyName == nameof(SelectedSort))
                {
                    ApplySorting();
                }
            };
        }

        private bool FilterSongs(object obj)
        {
            if (obj is not SongModel song) return false;

            return SelectedFilter switch
            {
                "Only Local" => !song.IsOnRockbox,
                "Only On Rockbox" => song.IsOnRockbox,
                _ => true, // "All"
            };
        }

        private void ApplySorting()
        {
            if (SongsView == null) return;

            SongsView.SortDescriptions.Clear();

            if (!string.IsNullOrEmpty(SelectedSort))
            {
                SongsView.SortDescriptions.Add(new SortDescription(SelectedSort, ListSortDirection.Ascending));
            }
        }

        private void UpdateRockBox()
        {
            IsRockBoxConnected = SelectedRockboxDrive != "None";
            RockboxSongs.Clear();
            foreach (var song in _rockboxService.LoadSongs(SelectedRockboxDrive))
            {
                RockboxSongs.Add(song);
            }
        }

        [RelayCommand]
        private void RefreshSongs()
        {
            Songs.Clear();
            foreach (var s in _songLibrary.LoadSongs(RockboxSongs))
            {
                Songs.Add(s);
            }
        }
        
        [RelayCommand]
        private void RefreshDevices()
        {
            RockboxDrives.Clear();
            foreach (var d in _rockboxService.DetectDrives())
            {
                RockboxDrives.Add(d);
            }

            if(RockboxDrives.Count > 1)
            {
                Console.WriteLine("Found RockBox device: " + RockboxDrives[1]);
                SelectedRockboxDrive = RockboxDrives[1];
                UpdateRockBox();
            } else
            {
                SelectedRockboxDrive = RockboxDrives.First();
            }
        }

        [RelayCommand]
        private async Task DownloadUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Please enter a valid URL.");
                return;
            }

            var playlist = await _downloadQueue.CheckForPlaylist(url);
            if(playlist.IsPlaylist)
            {
                var result = MessageBox.Show($"Playlist detected with {playlist.VideoUrls.Count} entries. Add all to download queue?", "Playlist Detected", MessageBoxButton.YesNo);
                if(result == MessageBoxResult.Yes)
                {
                    Console.WriteLine($"Adding playlist '{playlist.Name}' with {playlist.VideoUrls.Count} entries to queue.");
                    int songIndex = 1;
                    foreach(var entry in playlist.VideoUrls)
                    {
                        Console.WriteLine("Adding to queue: " + entry);
                        var item = new QueuedSongModel { 
                            Url = entry,
                            Filter = $"{playlist.Name}/{songIndex++}.[TITLE].[EXT]"
                        };
                        QueuedSongs.Add(item);
                        _downloadQueue.Enqueue(item);
                    }
                    UrlText = string.Empty;
                }
                return;
            } else {
                var item = new QueuedSongModel { Url = url };
                QueuedSongs.Add(item);
                _downloadQueue.Enqueue(item);
                UrlText = string.Empty;
            }
        }

        [RelayCommand]
        private void UploadSongs()
        {
            if (!IsRockBoxConnected)
            {
                MessageBox.Show("No Rockbox drive selected.");
                return;
            }
            var notOnRockbox = Songs.Where(s => !s.IsOnRockbox);
            _rockboxService.UploadSongs(SelectedRockboxDrive, notOnRockbox);
        }

        [RelayCommand]
        private void DeleteSong(SongModel song)
        {
            if (song == null) return;

            _songLibrary.DeleteSong(song);

            Songs.Remove(song);
        }

        [RelayCommand]
        private void EditSong(SongModel song)
        {
            if (song == null) return;

            var editor = new MetadataEditorWindow(song);
            if (editor.ShowDialog() == true)
            {
                RefreshSongs();
            }
        }

        [RelayCommand]
        private void HardSync() { 
            if (!IsRockBoxConnected)
            {
                MessageBox.Show("No Rockbox drive selected.");
                return;
            }

            var result = MessageBox.Show("This will delete any songs on the Rockbox device that are not in the local library. Continue?", "Confirm Hard Sync", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                _rockboxService.HardSync(SelectedRockboxDrive, Songs);
                UpdateRockBox();
                SongsView.Refresh();
            }
        }

        [RelayCommand]
        private void OpenIssues() {
            string url = "https://github.com/brammie15/Y1-Genesis/issues";
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }

        [RelayCommand]
        private void ChangeTheme(string themeName) {
            Application.Current.Resources.MergedDictionaries.Clear();

            if (themeName != "Default")
            {
                string themePath = themeName switch
                {
                    "Pink" => "/themes/Pinktheme.xaml",
                    "Dark" => "/themes/DarkTheme.xaml",
                };

                if (themePath != null)
                {
                    try
                    {
                        var dict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
                        Application.Current.Resources.MergedDictionaries.Add(dict);
                    } catch
                    {
                        // euuuh
                    }
                }
            }
        }
    }
}