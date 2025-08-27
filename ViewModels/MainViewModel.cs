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
        [ObservableProperty] private string urlText;
        [ObservableProperty] private bool isRockBoxConnected;

        public MainViewModel()
        {
            string musicFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Music");
            _songLibrary = new SongLibraryService(musicFolder);
            _downloadQueue = new DownloadQueueService(musicFolder);
            _rockboxService = new RockboxService();

            _downloadQueue.SongsChanged += () => RefreshSongs();

            RefreshSongs();

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedRockboxDrive))
                {
                    IsRockBoxConnected = SelectedRockboxDrive != "None";
                    RockboxSongs.Clear();
                    foreach (var song in _rockboxService.LoadSongs(SelectedRockboxDrive))
                    {
                        RockboxSongs.Add(song);
                    }
                }
            };

            RefreshDevices();
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
            SelectedRockboxDrive = RockboxDrives.First();
        }

        [RelayCommand]
        private void DownloadUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Please enter a valid URL.");
                return;
            }

            var item = new QueuedSongModel { Url = url };
            QueuedSongs.Add(item);
            _downloadQueue.Enqueue(item);
            UrlText = string.Empty;
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
    }
}