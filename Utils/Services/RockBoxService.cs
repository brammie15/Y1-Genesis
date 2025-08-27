using System;
using System.Collections.Generic;
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
    }
}
