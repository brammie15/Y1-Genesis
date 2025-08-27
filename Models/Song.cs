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
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Year { get; set; }
        public string Genre { get; set; }
        public string Description { get; set; }

        [ObservableProperty]
        private bool isOnRockbox;
        public Brush BorderColor => IsOnRockbox ? Brushes.Green : Brushes.Black;
        public BitmapImage AlbumArt { get; set; }
    }
}
