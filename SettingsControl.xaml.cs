using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SimHub.Plugins.Styles;
using System.IO;
using Newtonsoft.Json;
using SimHub;
using Newtonsoft.Json.Linq;
using System.Reflection.Emit;
using System.Globalization;


namespace Aaron.PluginRacenetReceiver
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private readonly RacenetDataReceiver plugin;

        // Add fields to save user's selections
        private string selectedLocation;
        private string selectedStage;
        private string selectedCarClass;
        private string selectedSurfaceCondition;
        private dynamic currentLeaderboardData;
        private int stageId;
        private int vehicleClassId;
        private int surfaceConditionId;
        public string CurrentPlayerName { get; set; }
        //private readonly bool hasLoaded = false;

        public SettingsControl(RacenetDataReceiver plugin)
        {
            InitializeComponent();
            this.plugin = plugin;

            // Load settings from file
            if (File.Exists("settings.json"))
            {
                string jsonSettings = File.ReadAllText("settings.json");
                plugin.Settings = JsonConvert.DeserializeObject<Settings>(jsonSettings);
            }
            else
            {
                plugin.Settings = new Settings
                {
                    RefreshToken = plugin.RefreshToken,
                    ClubName = plugin.ClubName
                };
            }

            FillClubList();

            plugin.RefreshToken = plugin.Settings.RefreshToken;

            // Attach the Loaded event handler
            this.Loaded += SettingsControl_Loaded;
        }

        private void SettingsControl_Loaded(object sender, RoutedEventArgs e)
        {
            FillLocationComboBox();            
            if (locationComboBox.SelectedItem != null)
            {
                FillStageComboBox(locationComboBox.SelectedItem.ToString());
            }
            FillCarClassComboBox();
            FillSurfaceConditionComboBox();

            Logging.Current.Info($"Selected Location: {selectedLocation}");
            Logging.Current.Info($"Selected Stage: {selectedStage}");
            Logging.Current.Info($"Selected Car Class: {selectedCarClass}");
            Logging.Current.Info($"Selected Surface Condition: {selectedSurfaceCondition}");

            // Save the default selections if the settings file does not exist or the selections are null
            if (!File.Exists("settings.json") || plugin.Settings.SelectedLocation == null || plugin.Settings.SelectedStage == null || plugin.Settings.SelectedCarClass == null || plugin.Settings.SelectedSurfaceCondition == null)
            {
                SaveSettings();
            }
        }

        private void FillLocationComboBox()
        {
            // Assuming pluginManager is accessible in this context
            var timeTrialPreInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.timeTrialPreInfo"));

            if (timeTrialPreInfoData != null)
            {
                // Parse timeTrialPreInfoData to a JObject
                JObject timeTrialPreInfoDataJson = JObject.Parse(timeTrialPreInfoData);

                // Get the locations from the JSON data
                var locations = timeTrialPreInfoDataJson["locations"];
                var orderedLocations = timeTrialPreInfoDataJson["orderedLocations"];

                // Save the currently selected item
                //var selectedItem = locationComboBox.SelectedItem;

                // Fill the locationComboBox with the location names in the order specified by orderedLocations
                locationComboBox.Items.Clear();
                foreach (var orderedLocation in orderedLocations.Children<JObject>())
                {
                    var locationId = orderedLocation["id"].Value<string>();
                    var location = locations[locationId];
                    if (location != null)
                    {
                        locationComboBox.Items.Add(location.Value<string>());
                    }
                }

                if (plugin.Settings.SelectedLocation != null)
                {
                    locationComboBox.SelectedItem = plugin.Settings.SelectedLocation;
                }
                else if (locationComboBox.HasItems)
                {
                    // Set the default selected item
                    locationComboBox.SelectedIndex = 0;
                }
            }
        }

        private void LocationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(locationComboBox.SelectedItem == null) return;

            string selectedLocation = locationComboBox.SelectedItem.ToString();

            // Save the user's selection
            this.selectedLocation = selectedLocation;
            plugin.Settings.SelectedLocation = selectedLocation;
            plugin.Settings.SelectedStage = null;

            FillStageComboBox(selectedLocation);
            Logging.Current.Info($"Selected Location Changed To: {selectedLocation}");
            SaveSettings();
            CheckAndFillLeaderboardDataGrid();
        }

        private void FillStageComboBox(string selectedLocation)
        {
            // Assuming timeTrialPreInfoData is accessible in this context
            var timeTrialPreInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.timeTrialPreInfo"));

            // Parse timeTrialPreInfoData to a JObject
            JObject timeTrialPreInfoDataJson = JObject.Parse(timeTrialPreInfoData);

            // Get the location ID for the selected location
            string selectedLocationId = timeTrialPreInfoDataJson["locations"]
                .Children<JProperty>()
                .FirstOrDefault(x => x.Value.ToString() == selectedLocation)?.Name;

            if (string.IsNullOrEmpty(selectedLocationId))
            {
                Logging.Current.Info($"Location: {selectedLocation} not found in locations dictionary.");
                return;
            }

            // Get the route IDs for the selected location
            var routeIds = timeTrialPreInfoDataJson["locationRoute"][selectedLocationId];

            // Clear the stageComboBox
            stageComboBox.Items.Clear();

            // Fill the stageComboBox with the route names
            foreach (var routeId in routeIds)
            {
                var routeName = timeTrialPreInfoDataJson["routes"][routeId.ToString()];
                stageComboBox.Items.Add(routeName);
            }

            // Check if there is a selected stage in the settings
            if (plugin.Settings.SelectedStage != null)
            {
                foreach (var item in stageComboBox.Items)
                {
                    if (item.ToString() == plugin.Settings.SelectedStage)
                    {
                        stageComboBox.SelectedItem = item;
                        break;
                    }
                }         
            }
            else if (stageComboBox.HasItems)
            {
                // Set the default selected item
                stageComboBox.SelectedIndex = 0;
            }
        }

        private void StageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(stageComboBox.SelectedItem == null) return;

            string selectedStage = stageComboBox.SelectedItem.ToString();

            // Save the user's selection
            this.selectedStage = selectedStage;
            plugin.Settings.SelectedStage = selectedStage;
            Logging.Current.Info($"Selected Stage Changed To: {selectedStage}");
            SaveSettings();
            CheckAndFillLeaderboardDataGrid();
        }

        private void FillCarClassComboBox()
        {
            // Assuming pluginManager is accessible in this context
            var timeTrialPreInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.timeTrialPreInfo"));
            
            if (timeTrialPreInfoData != null)
            {
                // Parse timeTrialPreInfoData to a JObject
                JObject timeTrialPreInfoDataJson = JObject.Parse(timeTrialPreInfoData);

                // Get the orderedVehicleClasses from the JSON data
                var orderedVehicleClasses = timeTrialPreInfoDataJson["orderedVehicleClasses"];

                // Fill the carClassComboBox with the vehicle class names in the order specified by orderedVehicleClasses
                carClassComboBox.Items.Clear();
                foreach (var orderedVehicleClass in orderedVehicleClasses.Children<JObject>())
                {
                    var vehicleClassName = orderedVehicleClass["value"].Value<string>();
                    carClassComboBox.Items.Add(vehicleClassName);
                }

                if (plugin.Settings.SelectedCarClass != null)
                {
                    carClassComboBox.SelectedItem = plugin.Settings.SelectedCarClass;
                }
                else if (carClassComboBox.HasItems)
                {
                    // Set the default selected item
                    carClassComboBox.SelectedIndex = 0;
                    
                }
            }
        }

        private void CarClassComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(carClassComboBox.SelectedItem == null) return;

            string selectedCarClass = carClassComboBox.SelectedItem.ToString();

            // Save the user's selection
            this.selectedCarClass = selectedCarClass;
            plugin.Settings.SelectedCarClass = selectedCarClass;
            Logging.Current.Info($"Selected Car Class Changed To: {selectedCarClass}");
            SaveSettings();
            CheckAndFillLeaderboardDataGrid();
        }

        private void FillSurfaceConditionComboBox()
        {
            // Assuming pluginManager is accessible in this context
            var timeTrialPreInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.timeTrialPreInfo"));

            if (timeTrialPreInfoData != null)
            {
                // Parse timeTrialPreInfoData to a JObject
                JObject timeTrialPreInfoDataJson = JObject.Parse(timeTrialPreInfoData);

                // Get the surface conditions from the JSON data
                var surfaceConditions = timeTrialPreInfoDataJson["surfaceConditions"];

                // Fill the surfaceConditionComboBox with the surface condition names
                surfaceConditionComboBox.Items.Clear();
                foreach (var surfaceCondition in surfaceConditions.Children<JProperty>())
                {
                    surfaceConditionComboBox.Items.Add(surfaceCondition.Value.Value<string>());
                }

                if (plugin.Settings.SelectedSurfaceCondition != null)
                {
                    surfaceConditionComboBox.SelectedItem = plugin.Settings.SelectedSurfaceCondition;
                }
                else if (surfaceConditionComboBox.HasItems)
                {
                    // Set the default selected item
                    surfaceConditionComboBox.SelectedIndex = 0;
                }
            }
        }

        private void SurfaceConditionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(surfaceConditionComboBox.SelectedItem == null) return;

            string selectedSurfaceCondition = surfaceConditionComboBox.SelectedItem.ToString();

            // Save the user's selection
            this.selectedSurfaceCondition = selectedSurfaceCondition;
            plugin.Settings.SelectedSurfaceCondition = selectedSurfaceCondition;
            Logging.Current.Info($"Selected Surface Condition Changed To: {selectedSurfaceCondition}");
            SaveSettings();
            CheckAndFillLeaderboardDataGrid();
        }

        private void FillClubList()
        {
            if (clubNameComboBox.IsLoaded)
            {
                //clean up the list
                clubNameComboBox.Items.Clear();
                // Sort the club list by club name

                var selectedIndex = 0;
                var sortedClubList = plugin.ClubListData.OrderBy(club => club.clubName.ToString()).ToList();
                var idx = 0;
                foreach (var club in sortedClubList)
                {
                    clubNameComboBox.Items.Add(club.clubName);
                    if(club.clubName == plugin.Settings.ClubName)
                    {
                        selectedIndex = idx;
                    }
                    idx++;
                }



                // Set the selected item to the saved club name
                clubNameComboBox.SelectedIndex = selectedIndex;
            }
            else
            {
                clubNameComboBox.Loaded += (s, e) => FillClubList();
            }
        }

        private void OpenDialogButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TokenInputDialog();
            if (dialog.ShowDialog() == true)
            {
                string refreshToken = dialog.Token;
                plugin.Settings.RefreshToken = refreshToken;
                plugin.RefreshToken = refreshToken;
                plugin.DoRefreshToken(false);
                SaveSettings();
            }
        }

        private void ClubNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(clubNameComboBox.SelectedItem == null) return;

            string clubName = clubNameComboBox.SelectedItem.ToString();
            plugin.Settings.ClubName = clubName;
            plugin.Settings.RefreshToken = plugin.RefreshToken;
            plugin.ClubName = clubName;
            SaveSettings();

            // Log the selected club name
            Logging.Current.Info($"Selected Club Name: {clubName}");
        }

        private void ClubNameComboBox_DropDownOpened(object sender, EventArgs e)
        {
            plugin.FetchClubListAsync().ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        FillClubList();
                    });
                }
            });
        }

        private void SaveSettings()
        {
            // Check if the token and club name are not empty before saving
            if (!string.IsNullOrEmpty(plugin.Settings.RefreshToken) && !string.IsNullOrEmpty(plugin.Settings.ClubName))
            {
                // settings.RefreshToken = plugin.RefreshToken;
                string json = JsonConvert.SerializeObject(plugin.Settings);
                File.WriteAllText("settings.json", json);
            }
            else
            {
                // Log an error message or throw an exception
                Logging.Current.Info("Error: Token or Club Name is empty. Settings not saved.");
            }
        }

        // Define a class to represent a row in the leaderboardDataGrid
        public class LeaderboardRow
        {
            public int Position { get; set; }
            public string Player { get; set; }
            public string Vehicle { get; set; }
            public string Assists { get; set; }
            public string Penalty { get; set; }
            public string Time { get; set; }
            public string DiffFirst { get; set; }
            public bool IsCurrentUser { get; set; }
            // public List<BitmapImage> AssistIcons { get; set; }
            public BitmapImage[] AssistIcons { get; set; }
        }

        private void FillLeaderboardDataGrid(dynamic leaderboardData)
        {
            // Update the current leaderboard data
            currentLeaderboardData = leaderboardData;

            // Get the current player's name
            var personalInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.personalInfo"));
            CurrentPlayerName = JObject.Parse(personalInfoData)["displayName"] != null ? JObject.Parse(personalInfoData)["displayName"].ToString() : string.Empty;
            
            // Convert the leaderboardData to a list of LeaderboardRow
            List<LeaderboardRow> rows = new List<LeaderboardRow>();
            foreach (var entry in leaderboardData.entries)
            {
                // Format Time and DiffFirst to limit to 12 characters
                string time = entry.time != null ? (entry.time.ToString().Length > 12 ? entry.time.ToString().Substring(0, 12) : entry.time.ToString()) : string.Empty;
                string diffFirst = entry.differenceToFirst != null ? (entry.differenceToFirst.ToString().Length > 12 ? entry.differenceToFirst.ToString().Substring(0, 12) : entry.differenceToFirst.ToString()) : string.Empty;

                // Convert assistFlags from integer array to string array
                int[] assistFlagsIntArray = entry.assistFlags.ToObject<int[]>();
                string[] assistFlagsStrArray = Array.ConvertAll(assistFlagsIntArray, x => x.ToString()); 
                
                // Logging.Current.Info($"Rank: {entry.rank}, Player: {entry.displayName}, Vehicle: {entry.vehicle}, Assists: {entry.assistFlags.Count}, Penalty: {entry.timePenalty}, Time: {entry.time}, Diff1st: {entry.differenceToFirst}");
                // Create a new LeaderboardRow
                LeaderboardRow row = new LeaderboardRow
                {
                    Position = entry.rank,
                    Player = entry.displayName,
                    Vehicle = entry.vehicle,
                    Assists = string.Join("- ", assistFlagsStrArray),
                    AssistIcons = GetAssistIcons(assistFlagsIntArray),
                    Penalty = entry.timePenalty,
                    Time = time,
                    DiffFirst = diffFirst, 
                    IsCurrentUser = entry.displayName == CurrentPlayerName
                };
                rows.Add(row);
            }

            // Fill the leaderboardDataGrid with the rows
            leaderboardDataGrid.ItemsSource = rows;
        }

        private async void CheckAndFillLeaderboardDataGrid()
        {
            // Check if all the combo boxes have a selected item
            if (stageComboBox.SelectedItem != null && carClassComboBox.SelectedItem != null && surfaceConditionComboBox.SelectedItem != null)
            {
                // Get the selected values
                string selectedStage = stageComboBox.SelectedItem.ToString();
                string selectedCarClass = carClassComboBox.SelectedItem.ToString();
                string selectedSurfaceCondition = surfaceConditionComboBox.SelectedItem.ToString();

                // Get the timeTrialPreInfoData
                var timeTrialPreInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.timeTrialPreInfo"));

                // Create a JsonDataHelper instance
                JsonDataHelper jsonDataHelper = new JsonDataHelper(timeTrialPreInfoData);

                // Convert the selected values to the parameters needed for FetchTimeTrialLeaderboardDataAsync
                stageId = int.Parse(jsonDataHelper.GetKeyByValue(selectedStage));
                vehicleClassId = int.Parse(jsonDataHelper.GetKeyByValue(selectedCarClass));
                surfaceConditionId = int.Parse(jsonDataHelper.GetKeyByValue(selectedSurfaceCondition));

                // Fetch the leaderboard data
                var leaderboardData = await plugin.FetchTimeTrialLeaderboardDataAsync(stageId, vehicleClassId, surfaceConditionId);

                // Update the current leaderboard data
                currentLeaderboardData = leaderboardData;

                // Fill the leaderboardDataGrid with the fetched data
                FillLeaderboardDataGrid(leaderboardData);
            }
        }

        private async void PreviousPageButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the previous cursor from the current leaderboard data
            string previousCursor = currentLeaderboardData["previous"].ToString();

            // Fetch the previous page of leaderboard data
            var leaderboardData = await plugin.FetchTimeTrialLeaderboardDataAsync(stageId, vehicleClassId, surfaceConditionId, cursor:previousCursor);

            // Fill the leaderboardDataGrid with the fetched data
            FillLeaderboardDataGrid(leaderboardData);
        }

        private async void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the next cursor from the current leaderboard data
            string nextCursor = currentLeaderboardData["next"].ToString();
            

            // Fetch the next page of leaderboard data
            var leaderboardData = await plugin.FetchTimeTrialLeaderboardDataAsync(stageId, vehicleClassId, surfaceConditionId, cursor:nextCursor);

            // Fill the leaderboardDataGrid with the fetched data
            FillLeaderboardDataGrid(leaderboardData);
        }
        
        private BitmapImage[] GetAssistIcons(int[] assistFlags)
        {
            BitmapImage[] icons = new BitmapImage[3];
            foreach (int flag in assistFlags)
            {
                switch (flag)
                {
                    case 1:
                        icons[0] = new BitmapImage(new Uri(@"C:\Projects\Simhub-Plugin-RacenetReceiver\icons\1.png"));
                        break;
                    case 2:
                        icons[1] = new BitmapImage(new Uri(@"C:\Projects\Simhub-Plugin-RacenetReceiver\icons\2.png"));
                        break;
                    case 4:
                        icons[2] = new BitmapImage(new Uri(@"C:\Projects\Simhub-Plugin-RacenetReceiver\icons\4.png"));
                        break;
                }
            }
            return icons;
        }
    }
}