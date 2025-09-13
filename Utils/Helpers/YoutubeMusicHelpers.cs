using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models.Info;

namespace Y1_ingester.Utils.Helpers
{
    internal class YoutubeMusicHelpers
    {

        public static YouTubeMusicClient client = new();

        public static async Task<SongVideoInfo?> GetSongVideoInfo(string videoUrl)
        {
            SongVideoInfo songInfo = await client.GetSongVideoInfoAsync(videoUrl);
            return songInfo;
        }
    }
}
