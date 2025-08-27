using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using Y1_ingester.Models;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace Y1_ingester.Utils
{
    public class DownloadQueueService : IDisposable
    {
        private readonly YoutubeDL _ytdl;
        private readonly BlockingCollection<QueuedSongModel> _queue = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly string _musicFolder;

        public event Action? SongsChanged;

        public DownloadQueueService(string musicFolder)
        {
            _musicFolder = musicFolder;
            _ytdl = new YoutubeDL
            {
                YoutubeDLPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yt-dlp.exe"),
                FFmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg.exe"),
                OutputFolder = _musicFolder
            };

            Task.Run(() => ProcessQueueAsync(_cts.Token));
        }

        public void Enqueue(QueuedSongModel song) => _queue.Add(song);

        private async Task ProcessQueueAsync(CancellationToken token)
        {
            foreach (var item in _queue.GetConsumingEnumerable(token))
            {
                try
                {
                    item.Status = "Fetching info…";
                    var infoResult = await _ytdl.RunVideoDataFetch(item.Url);
                    if (!infoResult.Success)
                    {
                        item.Status = "Failed (metadata)";
                        continue;
                    }

                    var info = infoResult.Data;
                    item.Title = info.Title ?? item.Url;
                    item.Status = "Downloading…";

                    var result = await _ytdl.RunAudioDownload(item.Url, AudioConversionFormat.Mp3);
                    if (!result.Success)
                    {
                        item.Status = "Failed (download)";
                        continue;
                    }

                    string file = Path.Combine(_musicFolder, result.Data);

                    // Tag file
                    var tagFile = TagLib.File.Create(file);
                    tagFile.Tag.Title = info.Title ?? Path.GetFileNameWithoutExtension(file);
                    tagFile.Tag.Album = info.Album ?? "Youtube Music";
                    tagFile.Tag.Performers = !string.IsNullOrEmpty(info.Uploader) ? new[] { info.Uploader } : new[] { "Unknown" };
                    tagFile.Tag.Year = (uint)(info.UploadDate?.Year ?? DateTime.Now.Year);

                    tagFile.Tag.Genres = info.Tags?.ToArray() ?? new[] { "" };
                    tagFile.Tag.Comment = "Hello World!: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    if (!string.IsNullOrEmpty(info.Thumbnail))
                    {
                        using var wc = new System.Net.WebClient();
                        var imageData = wc.DownloadData(info.Thumbnail);
                        var pic = new Picture(new ByteVector(imageData));
                        tagFile.Tag.Pictures = new IPicture[] { pic };
                    }
                    tagFile.Save();

                    item.Status = "Completed";
                    SongsChanged?.Invoke();
                } catch (Exception ex)
                {
                    item.Status = "Failed (" + ex.Message + ")";
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _queue.CompleteAdding();
        }
    }
}
