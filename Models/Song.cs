using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Y1_ingester.Models
{
    public partial class SongModel: ObservableObject
    {
        public required string FilePath { get; set; }
        public string Title { get; set; } = "Unknown";
        public string Artist { get; set; } = "Unknown";
        public string Album { get; set; } = "Unknown";
        public string Year { get; set; } = "Unknown";
        public string Genre { get; set; } = "Unknown";
        public string Description { get; set; } = "Unknown";

        [ObservableProperty]
        private bool isOnRockbox;

        [ObservableProperty]
        private bool isLocal;
        public Brush BorderColor => IsOnRockbox ? Brushes.Green : Brushes.Black;
        public BitmapImage? AlbumArt { get; set; } = null;

        ~SongModel() {
            AlbumArt = null;
        }
    }
}
