using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Y1_ingester.Models;

namespace Y1_ingester.Utils.Services
{
    public class SongLibraryService
    {
        private readonly string _musicFolder;

        public SongLibraryService(string musicFolder)
        {
            _musicFolder = musicFolder;
            if (!Directory.Exists(_musicFolder))
                Directory.CreateDirectory(_musicFolder);
        }

        public IEnumerable<SongModel> LoadSongs(IEnumerable<SongModel> rockboxSongs)
        {
            var songs = new List<SongModel>();
            var files = Directory.GetFiles(_musicFolder, "*.mp3", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);
                    BitmapImage? albumArt = null;

                    if (tagFile.Tag.Pictures.Length > 0)
                    {
                        using var ms = new MemoryStream(tagFile.Tag.Pictures[0].Data.Data);
                        albumArt = new BitmapImage();
                        albumArt.BeginInit();
                        albumArt.CacheOption = BitmapCacheOption.OnLoad;
                        albumArt.StreamSource = ms;
                        albumArt.EndInit();
                        albumArt.Freeze();
                    }

                    var song = new SongModel
                    {
                        FilePath = file,
                        Title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(file),
                        Artist = tagFile.Tag.FirstPerformer ?? "Unknown",
                        Album = tagFile.Tag.Album ?? "Unknown",
                        AlbumArt = albumArt,
                        Year = tagFile.Tag.Year > 0 ? tagFile.Tag.Year.ToString() : "Unknown",
                        IsOnRockbox = rockboxSongs.Any(r => r.Title == tagFile.Tag.Title),
                        IsLocal = true
                    };

                    songs.Add(song);
                } catch {
                    //ifnore bad files
                }
            }

            return songs;
        }

        public void DeleteSong(SongModel song)
        {
            if (File.Exists(song.FilePath))
            {
                File.Delete(song.FilePath);
            }
        }
    }
}
