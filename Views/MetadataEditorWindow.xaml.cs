using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Y1_ingester.Models;

namespace Y1_ingester.Views
{
    /// <summary>
    /// Interaction logic for MetadataEditorWindow.xaml
    /// </summary>
    public partial class MetadataEditorWindow : Window
    {
        private readonly SongModel _song;

        public MetadataEditorWindow(SongModel song)
        {
            InitializeComponent();
            _song = song;
            DataContext = _song;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = TagLib.File.Create(_song.FilePath);
                file.Tag.Title = _song.Title;
                file.Tag.Album = _song.Album;
                file.Tag.Performers = new[] { _song.Artist };
                if (uint.TryParse(_song.Year, out uint year))
                {
                    file.Tag.Year = year;
                }
                file.Save();

                DialogResult = true; // Close window and return success
            } catch (Exception ex)
            {
                MessageBox.Show($"Error saving metadata: {ex.Message}");
            }
        }
    }
}
