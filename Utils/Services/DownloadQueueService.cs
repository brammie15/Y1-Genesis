using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using TagLib.Ape;
using Y1_ingester.Models;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;


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

        public struct PlaylistCheckResult
        {
            public bool IsPlaylist => VideoUrls != null && VideoUrls.Count > 0;
            public string Name { get; set; }
            public List<string> VideoUrls = [];

            public PlaylistCheckResult(string playlistName, List<string> videoUrls)
            {
                Name = playlistName;
                VideoUrls = videoUrls;
            }
        }
        public async Task<PlaylistCheckResult> CheckForPlaylist(string url)
        {
            if (!DownloaddHelper.CheckToolsDownloaded())
            {
                await DownloaddHelper.DownloadTools();
            }
            var infoResult = await _ytdl.RunVideoDataFetch(url);
            if (!infoResult.Success)
            {
                Console.WriteLine("Failed to fetch video data: " + infoResult.ToString());
                return new PlaylistCheckResult("", []);
            }

            var info = infoResult.Data;
            if (info.Entries != null && info.Entries.Length > 0)
            {
                Console.WriteLine($"Playlist detected with {info.Entries.Length} entries.");
                var playlistName = info.Title ?? "Unnamed Playlist";
                var videoUrls = info.Entries.Select(e => e.Url).Where(u => !string.IsNullOrEmpty(u)).ToList()!;
                return new PlaylistCheckResult(playlistName, videoUrls);
            } else
            {
                Console.WriteLine($"Regular video detected.");
                return new PlaylistCheckResult("", []);
            }
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
                    tagFile.Tag.Copyright = "Downloaded via Y1-ingester";

                    if (!string.IsNullOrEmpty(info.Thumbnail))
                    {
                        using var wc = new System.Net.WebClient();
                        var imageData = wc.DownloadData(info.Thumbnail);

                        using var ms = new MemoryStream(imageData);
                        using var loaded = Image.Load<Rgba32>(ms); // load original

                        const int targetSize = 200;

                        float scale = Math.Min((float)targetSize / loaded.Width, (float)targetSize / loaded.Height);
                        int newWidth = Math.Max(1, (int)(loaded.Width * scale));
                        int newHeight = Math.Max(1, (int)(loaded.Height * scale));

                        loaded.Mutate(x => x.Resize(newWidth, newHeight));

                        using var canvas = new Image<Rgba32>(targetSize, targetSize, new Rgba32(0, 0, 0));

                        var offsetX = (targetSize - newWidth) / 2;
                        var offsetY = (targetSize - newHeight) / 2;
                        canvas.Mutate(ctx => ctx.DrawImage(loaded, new SixLabors.ImageSharp.Point(offsetX, offsetY), 1f));

                        using var outStream = new MemoryStream();
                        var encoder = new JpegEncoder
                        {
                            Quality = 90
                        };
                        canvas.Save(outStream, encoder);

                        var pic = new Picture(new ByteVector(outStream.ToArray()))
                        {
                            Type = PictureType.FrontCover,
                            Description = "Cover",
                            MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg
                        };

                        tagFile.Tag.Pictures = new IPicture[] { pic };
                    }
                    tagFile.Save();


                    //TODO: messy, needs to be redone
                    var filter = item.Filter;
                    if (!string.IsNullOrEmpty(filter))
                    {
                        var newFileName = filter.Replace("[TITLE]", tagFile.Tag.Title ?? "Unknown")
                                                .Replace("[ARTIST]", tagFile.Tag.Performers.Length > 0 ? tagFile.Tag.Performers[0] : "Unknown")
                                                .Replace("[ALBUM]", tagFile.Tag.Album ?? "Unknown")
                                                .Replace("[YEAR]", tagFile.Tag.Year != 0 ? tagFile.Tag.Year.ToString() : "Unknown")
                                                .Replace("[EXT]", Path.GetExtension(file).TrimStart('.'));

                        var parts = newFileName.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        for (int i = 0; i < parts.Length; i++)
                        {
                            parts[i] = SanitizeFileName(parts[i]); 
                        }
                        newFileName = Path.Combine(parts);

                        Console.WriteLine("Renaming to: " + newFileName);

                        var newFilePath = Path.Combine(_musicFolder, newFileName);

                        var newDir = Path.GetDirectoryName(newFilePath);
                        if (!string.IsNullOrEmpty(newDir) && !Directory.Exists(newDir))
                        {
                            Directory.CreateDirectory(newDir);
                        }

                        if (!string.Equals(file, newFilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (System.IO.File.Exists(newFilePath))
                            {
                                System.IO.File.Delete(newFilePath);
                            }
                            System.IO.File.Move(file, newFilePath);
                            file = newFilePath;
                        }
                    }

                    item.Status = "Completed";
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        SongsChanged?.Invoke();
                    });
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
        
        private static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_'); // or remove instead of replace
            }
            return fileName;
        }
    }
}
