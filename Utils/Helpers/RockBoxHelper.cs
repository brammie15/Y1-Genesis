using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Y1_ingester.Models;

namespace Y1_ingester
{
    public static class RockboxHelper
    {
        public static string? DetectRockboxDrive()
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable))
            {
                try
                {
                    string rockboxPath = Path.Combine(drive.RootDirectory.FullName, ".rockbox");
                    Console.WriteLine("Checking: " + rockboxPath);
                    if (Directory.Exists(rockboxPath))
                    {
                        return drive.RootDirectory.FullName;
                    }
                } catch { /* skip drives we can't access */ }
            }
            return null;
        }

        public static bool UploadMp3(string driveRoot, string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Source MP3 file not found.", sourceFilePath);

            try
            {
                string musicFolder = Path.Combine(driveRoot, "Music");
                if (!Directory.Exists(musicFolder))
                {
                    Directory.CreateDirectory(musicFolder);
                }

                string fileName = Path.GetFileName(sourceFilePath);
                string destPath = Path.Combine(musicFolder, fileName);

                File.Copy(sourceFilePath, destPath, true);
                Console.WriteLine($"Uploaded {fileName} to {destPath}");
                return true;
            } catch
            {
                return false;
            }
        }

        public static int UploadMultipleMp3(string driveRoot, IEnumerable<string> mp3Files)
        {
            int successCount = 0;
            foreach (var file in mp3Files)
            {
                if (UploadMp3(driveRoot, file))
                    successCount++;
            }
            return successCount;
        }

        public static List<SongModel> LoadRockboxSongs(string rockBoxDrive)
        {
            if(rockBoxDrive == "None" || string.IsNullOrEmpty(rockBoxDrive) || !Directory.Exists(rockBoxDrive))
            {
                return new List<SongModel>();
            }
            List<SongModel> RockboxSongs = new List<SongModel>();
            RockboxSongs.Clear();

            string rockboxMusicPath = Path.Combine(rockBoxDrive, "Music");
            if (!Directory.Exists(rockboxMusicPath))
            {
                return RockboxSongs;
            }

            var files = Directory.GetFiles(rockboxMusicPath, "*.mp3", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);

                    BitmapImage albumArt = null;
                    if (tagFile.Tag.Pictures.Length > 0)
                    {
                        using var ms = new MemoryStream(tagFile.Tag.Pictures[0].Data.Data);
                        albumArt = new BitmapImage();
                        albumArt.BeginInit();
                        albumArt.CacheOption = BitmapCacheOption.OnLoad;
                        albumArt.StreamSource = ms;
                        albumArt.EndInit();
                    }

                    RockboxSongs.Add(new SongModel
                    {
                        FilePath = file,
                        Title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(file),
                        Artist = tagFile.Tag.FirstPerformer ?? "Unknown",
                        Album = tagFile.Tag.Album ?? "Unknown",
                        AlbumArt = albumArt,
                        Year = tagFile.Tag.Year > 0 ? tagFile.Tag.Year.ToString() : "Unknown",
                        Genre = tagFile.Tag.FirstGenre ?? "Unknown",
                        Description = tagFile.Tag.Comment ?? "",
                    });
                } catch
                {
                    // Ignore invalid files
                }
            }

            return RockboxSongs;
        }
    }
}

