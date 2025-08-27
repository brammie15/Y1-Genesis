using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Y1_ingester
{
    internal class DownloaddHelper
    {
        public static bool CheckToolsDownloaded()
        {
            return System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yt-dlp.exe")) &&
                   System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg.exe"));
        }

        public static async Task DownloadTools()
        {
            if (!System.IO.Directory.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools")))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools"));
            }
            if (!CheckToolsDownloaded())
            {
                await YoutubeDLSharp.Utils.DownloadYtDlp("tools");
                await YoutubeDLSharp.Utils.DownloadFFmpeg("tools");
            } else
            {
                Console.WriteLine("Tools already downloaded.");
            }
        }
    }
}
