using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Y1_ingester.Models
{
    public partial class QueuedSongModel : ObservableObject
    {
        [ObservableProperty]
        private string url = String.Empty;

        //Filter for filename such as "1.[TITLE].ext"
        [ObservableProperty]
        private string filter = String.Empty;

        [ObservableProperty]
        private string title = "Pending…";

        [ObservableProperty]
        private string status = "Queued";
    }
}
