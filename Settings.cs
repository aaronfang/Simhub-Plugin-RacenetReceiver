using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aaron.PluginRacenetReceiver
{
    public class Settings
    {
        public string RefreshToken { get; set; }
        public string ClubName { get; set; }
        public string ClubID { get; set; }
        public string SelectedLocation { get; set; }
        public string SelectedStage { get; set; }
        public string SelectedCarClass { get; set; }
        public string SelectedSurfaceCondition { get; set; }
    }
}
