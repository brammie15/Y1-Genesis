using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Y1_ingester.Models;

namespace Y1_ingester.Utils.Services
{
    public class RockboxService
    {
        public IEnumerable<string> DetectDrives()
        {
            var drives = new List<string> { "None" };
            var drive = RockboxHelper.DetectRockboxDrive();
            if (drive != null)
                drives.Add(drive);
            return drives;
        }

        public IEnumerable<SongModel> LoadSongs(string drive) =>
            drive != "None" ? RockboxHelper.LoadRockboxSongs(drive) : Enumerable.Empty<SongModel>();

        public void UploadSongs(string drive, IEnumerable<SongModel> songs)
        {
            var files = songs.Select(s => s.FilePath).ToList();
            RockboxHelper.UploadMultipleMp3(drive, files);
        }

        public void HardSync(string drive, IEnumerable<SongModel> localSongs)
        {
            if (drive == "None")
                return;

            string rockboxMusicPath = Path.Combine(drive, "Music");
            if (!Directory.Exists(rockboxMusicPath))
                Directory.CreateDirectory(rockboxMusicPath);

            var remoteFiles = Directory.GetFiles(rockboxMusicPath, "*.mp3", SearchOption.TopDirectoryOnly)
                                       .Select(Path.GetFileName)
                                       .ToList();

            var localFileNames = localSongs.Select(s => Path.GetFileName(s.FilePath)).ToHashSet();

            foreach (var remoteFile in remoteFiles)
            {
                // Would be wierd if remoteFile is null, but just to be safe
                if (!localFileNames.Contains(remoteFile!))
                {
                    string fullPath = Path.Combine(rockboxMusicPath, remoteFile!);
                    try
                    {
                        File.Delete(fullPath);
                        Console.WriteLine($"Deleted from Rockbox: {remoteFile}");
                    } catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete {remoteFile}: {ex.Message}");
                    }
                }
            }

            UploadSongs(drive, localSongs);
        }
    }
}
