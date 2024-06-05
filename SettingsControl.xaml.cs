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
using System.Reflection;


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
        private string currentSelectedStage;
        private string selectedCarClass;
        private string selectedSurfaceCondition;
        private dynamic currentLeaderboardData;
        private dynamic currentClubLeaderboardData;
        private int stageId;
        private int vehicleClassId;
        private int surfaceConditionId;
        public string CurrentPlayerName { get; set; }
        public string SelectedEventLocation { get; set; }
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

            if (!string.IsNullOrEmpty(plugin.Settings.SelectedStage))
            {
                currentSelectedStage = plugin.Settings.SelectedStage;
            }

            FillClubList();

            plugin.RefreshToken = plugin.Settings.RefreshToken;

            // If ClubID is not empty, fetch club championship info
            if (!string.IsNullOrEmpty(plugin.Settings.ClubID))
            {
                Task.Run(async () =>
                {
                    await plugin.FetchClubChampionshipInfoAsync(plugin.Settings.ClubID);
                });
            }

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

            // Save the current selected stage before changing the location
            if (stageComboBox.SelectedItem != null)
            {
                currentSelectedStage = stageComboBox.SelectedItem.ToString();
            }

            string selectedLocation = locationComboBox.SelectedItem.ToString();

            // Save the user's selection
            this.selectedLocation = selectedLocation;
            plugin.Settings.SelectedLocation = selectedLocation;

            // Reset the selected stage in the settings
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

             // Try to restore the selected stage after filling the stageComboBox
            if (!string.IsNullOrEmpty(currentSelectedStage))
            {
                foreach (var item in stageComboBox.Items)
                {
                    if (item.ToString() == currentSelectedStage)
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
            // If no item is selected, select the first item
            if (stageComboBox.SelectedItem == null && stageComboBox.HasItems)
            {
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
                    var isActiveNow = false;
                    if (club.currentChampionshipSummary != null)
                    {
                        isActiveNow = club.currentChampionshipSummary.isActiveNow;
                    }

                    clubNameComboBox.Items.Add(new{club.clubName, club.clubID, isActiveNow});
                    if(club.clubName == plugin.Settings.ClubName)
                    {
                        selectedIndex = idx;
                    }
                    idx++;
                }

                // Set the selected item to the saved club name
                clubNameComboBox.SelectedIndex = selectedIndex;

                // Set the DisplayMemberPath to "clubName" to only display the club name
                // clubNameComboBox.DisplayMemberPath = "clubName";
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
            if (clubNameComboBox.SelectedItem == null) return;

            // string clubName = clubNameComboBox.SelectedItem.ToString();
            var selectedItem = (dynamic)clubNameComboBox.SelectedItem;
            string clubName = selectedItem.clubName;
            string clubID = selectedItem.clubID;

            plugin.Settings.ClubName = clubName;
            plugin.Settings.ClubID = clubID;
            plugin.Settings.RefreshToken = plugin.RefreshToken;
            plugin.ClubName = clubName;
            // plugin.Settings.SelectedEventLocation = null;
            

            // Fetch the club championship info
            Task.Run(async () =>
            {
                await plugin.FetchClubChampionshipInfoAsync(clubID);
                // Update the event list for the selected club
                this.Dispatcher.Invoke(() =>
                {
                    FillClubEventList();
                    // if(clubEventComboBox.HasItems)
                    // {
                    //     clubEventComboBox.SelectedIndex = 0;
                    // }
                });
            });
            SaveSettings();
            // Log the selected club name
            Logging.Current.Info($"Selected Club Name: {clubName}");

            CheckAndFillClubLeaderboardDataGrid();
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

        private void FillClubEventList()
        {
            if (clubEventComboBox.IsLoaded)
            {
                // Assuming pluginManager is accessible in this context
                var clubChampionshipInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.clubChampionshipInfo"));
                try
                {
                    // Unsubscribe from the SelectionChanged event
                    // clubEventComboBox.SelectionChanged -= ClubEventComboBox_SelectionChanged;
                    
                    // Parse clubChampionshipInfoData to a JObject
                    JObject clubChampionshipInfoDataJson = JObject.Parse(clubChampionshipInfoData);

                    // Get the events from the JSON data
                    var clubChampionship = clubChampionshipInfoDataJson["currentChampionship"];
                    var clubChampionshipEvents = clubChampionshipInfoDataJson["currentChampionship"]["events"];

                    // Clear the clubEventComboBox
                    clubEventComboBox.Items.Clear();

                    // Fill the clubEventComboBox with the club event>eventSettings>location value
                    foreach (var clubChampionshipEvent in clubChampionshipEvents.Children<JObject>())
                    {
                        var clubEventLocationName = clubChampionshipEvent["eventSettings"]["location"].Value<string>();
                        clubEventComboBox.Items.Add(clubEventLocationName);
                    }

                    // Set the selected item to the saved event location
                    if (plugin.Settings.SelectedEventLocation != null)
                    {
                        foreach (var item in clubEventComboBox.Items)
                        {
                            if (item.ToString() == plugin.Settings.SelectedEventLocation)
                            {
                                clubEventComboBox.SelectedItem = item;
                                break;
                            }
                        }
                        // clubEventComboBox.SelectedItem = plugin.Settings.SelectedEventLocation;
                    }
                    if (clubEventComboBox.SelectedItem == null && clubEventComboBox.HasItems)
                    {
                        clubEventComboBox.SelectedIndex = 0;
                    }

                    // Resubscribe to the SelectionChanged event
                    // clubEventComboBox.SelectionChanged += ClubEventComboBox_SelectionChanged;
                }
                catch (JsonReaderException ex)
                {
                    // Log the error or handle it appropriately
                    Logging.Current.Info($"Error parsing JSON: {ex.Message}");
                }
            }
            else
            {
                clubEventComboBox.Loaded += (s, e) => FillClubEventList();
            }
        }

        private void ClubEventComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (clubEventComboBox.SelectedItem == null) return;

            string selectedEventLocation = clubEventComboBox.SelectedItem.ToString();

            // Save the user's selection
            plugin.Settings.SelectedEventLocation = selectedEventLocation;
            // Reset the selected club stage
            // plugin.Settings.SelectedClubStage = null;、
            SaveSettings();
            // Fill the clubStageComboBox with the stages of the selected event
            FillClubStageList(selectedEventLocation);
        }

        private void FillClubStageList(string selectedEventLocation)
        {
            if (clubStageComboBox.IsLoaded)
            {
                // Assuming pluginManager is accessible in this context
                var clubChampionshipInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.clubChampionshipInfo"));
                try
                {
                    // Parse clubChampionshipInfoData to a JObject
                    JObject clubChampionshipInfoDataJson = JObject.Parse(clubChampionshipInfoData);

                    // Get the selected event from the JSON data
                    var clubChampionshipEvent = clubChampionshipInfoDataJson["currentChampionship"]["events"].FirstOrDefault(e => e["eventSettings"]["location"].Value<string>() == selectedEventLocation);

                    if (clubChampionshipEvent != null)
                    {
                        // Unsubscribe from the SelectionChanged event
                        // clubStageComboBox.SelectionChanged -= ClubStageComboBox_SelectionChanged;

                        // Clear the clubStageComboBox
                        clubStageComboBox.Items.Clear();

                        // Fill the clubStageComboBox with the club event>stages>stageSettings>route value
                        foreach (var stage in clubChampionshipEvent["stages"].Children<JObject>())
                        {
                            var stageRoute = stage["stageSettings"]["route"].Value<string>();
                            var leaderboardID = stage["leaderboardID"].Value<string>();
                            clubStageComboBox.Items.Add(new { Route = stageRoute, LeaderboardID = leaderboardID });
                        }

                        // Set the DisplayMemberPath to "Route" to only display the route
                        clubStageComboBox.DisplayMemberPath = "Route";

                        // Set the selected item to the saved event location
                        if (plugin.Settings.SelectedClubStage != null)
                        {
                            foreach (var item in clubStageComboBox.Items)
                            {
                                if (((dynamic)item).Route == plugin.Settings.SelectedClubStage)
                                {
                                    clubStageComboBox.SelectedItem = item;
                                    break;
                                }
                            }
                            // clubStageComboBox.SelectedItem = clubStageComboBox.Items.Cast<dynamic>().FirstOrDefault(item => item.Route == plugin.Settings.SelectedClubStage);
                        }
                        if (clubStageComboBox.SelectedItem == null && clubStageComboBox.HasItems)
                        {
                            // Set the default selected item
                            clubStageComboBox.SelectedIndex = 0;
                        }

                        // Resubscribe to the SelectionChanged event
                        // clubStageComboBox.SelectionChanged += ClubStageComboBox_SelectionChanged;
                    }
                }
                catch (JsonReaderException ex)
                {
                    // Log the error or handle it appropriately
                    Logging.Current.Info($"Error parsing JSON: {ex.Message}");
                }
            }
            else
            {
                clubStageComboBox.Loaded += (s, e) => FillClubStageList(selectedEventLocation);
            }
        }

        private void ClubStageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (clubStageComboBox.SelectedItem == null) return;
            
            // Save the selected stage and leaderboard ID to Settings
            var selectedItem = (dynamic)clubStageComboBox.SelectedItem;
            plugin.Settings.SelectedClubStage = selectedItem.Route;
            plugin.Settings.SelectedClubLeaderboardID = selectedItem.LeaderboardID;
            SaveSettings();
            CheckAndFillClubLeaderboardDataGrid();
        }

        private void SaveSettings()
        {
            // Check if the token and club name are not empty before saving
            if (!string.IsNullOrEmpty(plugin.Settings.RefreshToken) && !string.IsNullOrEmpty(plugin.Settings.ClubName))
            {
                // settings.RefreshToken = plugin.RefreshToken;
                string json = JsonConvert.SerializeObject(plugin.Settings, Formatting.Indented);
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
            public string Nation { get; set; }
            public BitmapImage NationFlag { get; set; }
            public string Vehicle { get; set; }
            public string Assists { get; set; }
            public string Penalty { get; set; }
            public string Time { get; set; }
            public string DiffFirst { get; set; }
            public bool IsCurrentUser { get; set; }
            // public List<BitmapImage> AssistIcons { get; set; }
            public BitmapImage[] AssistIcons { get; set; }
            public int Points { get; set; }
            public string Overall { get; set; }
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
                string time = entry.time != null ? FormatTime(entry.time.ToString()) : string.Empty;
                string diffFirst = entry.differenceToFirst != null ? FormatDiffTime(entry.differenceToFirst.ToString()) : string.Empty;
                // string penalty = entry.timePenalty != null ? FormatTime(entry.timePenalty.ToString()) : string.Empty;

                // Convert assistFlags from integer array to string array
                int[] assistFlagsIntArray = entry.assistFlags.ToObject<int[]>();
                if (assistFlagsIntArray.Length == 0)
                {
                    assistFlagsIntArray = new int[] { 0 };
                }
                string[] assistFlagsStrArray = Array.ConvertAll(assistFlagsIntArray, x => x.ToString());

                // Create a new LeaderboardRow
                LeaderboardRow row = new LeaderboardRow
                {
                    Position = entry.rank,
                    Player = entry.displayName,
                    Nation = entry.nationalityID,
                    NationFlag = GetNationFlag(entry.nationalityID.ToString()),
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

        private void FillClubLeaderboardDataGrid(dynamic leaderboardData)
        {
            // Update the current leaderboard data
            currentClubLeaderboardData = leaderboardData;

            // Get the current player's name
            var personalInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.personalInfo"));
            CurrentPlayerName = JObject.Parse(personalInfoData)["displayName"] != null ? JObject.Parse(personalInfoData)["displayName"].ToString() : string.Empty;
            
            // Convert the leaderboardData to a list of LeaderboardRow
            List<LeaderboardRow> rows = new List<LeaderboardRow>();
            foreach (var entry in leaderboardData.entries)
            {
                string time = entry.time != null ? FormatTime(entry.time.ToString()) : string.Empty;
                string diffFirst = entry.differenceToFirst != null ? FormatDiffTime(entry.differenceToFirst.ToString()) : string.Empty;

                // Convert assistFlags from integer array to string array
                int[] assistFlagsIntArray = entry.assists.ToObject<int[]>();
                string[] assistFlagsStrArray = Array.ConvertAll(assistFlagsIntArray, x => x.ToString()); 

                // Add Points and Overall to the row
                int points = entry.points != null ? int.Parse(entry.points.ToString()) : 0;
                string overall = entry.timeAccumulated != null ? FormatTime(entry.timeAccumulated.ToString()) : string.Empty;

                rows.Add(new LeaderboardRow
                {
                    Position = entry.rank,
                    Player = entry.displayName,
                    Nation = entry.nationalityID,
                    NationFlag = GetNationFlag(entry.nationalityID.ToString()),
                    Vehicle = entry.vehicle,
                    Assists = string.Join("- ", assistFlagsStrArray),
                    AssistIcons = GetAssistIcons(assistFlagsIntArray),
                    Penalty = entry.timePenalty,
                    Time = time,
                    DiffFirst = diffFirst, 
                    IsCurrentUser = entry.displayName == CurrentPlayerName, 
                    Points = points,
                    Overall = overall
                });
            }

            // Set the ItemsSource of the DataGrid
            clubDataGrid.ItemsSource = rows;
        }

        private BitmapImage GetNationFlag(string nationalityId)
        {
            {
                // Load the JSON file
                string json = LoadJsonFromResources("Aaron.PluginRacenetReceiver.nationalityID.json");

                // Parse the JSON file
                var jsonObject = JObject.Parse(json);

                // Get the image file name
                string imageFileName = (string)jsonObject[nationalityId];

                // Load the image from resources
                if (imageFileName == null)
                {
                    return null;
                }
                BitmapImage image = LoadImageFromResources($"Aaron.PluginRacenetReceiver.icons.flags.{imageFileName}");

                return image;
            }
        }

        private string FormatTime(string timeStr)
        {
            TimeSpan timeSpan = TimeSpan.Parse(timeStr);

            // If the time is less than one second, only display the milliseconds
            if (timeSpan.TotalSeconds < 1)
            {
                return "0." + timeSpan.Milliseconds.ToString("D3").Substring(0, 3);
            }
            // If the time is less than one minute, only display the seconds and milliseconds
            else if (timeSpan.TotalMinutes < 1)
            {
                return string.Format("{0}.{1}", 
                    timeSpan.Seconds.ToString("D2"), 
                    timeSpan.Milliseconds.ToString("D3").Substring(0, 3));
            }
            // If the time is less than one hour, only display the minutes, seconds, and milliseconds
            else if (timeSpan.TotalHours < 1)
            {
                return string.Format("{0}:{1}.{2}", 
                    timeSpan.Minutes.ToString("D2"), 
                    timeSpan.Seconds.ToString("D2"), 
                    timeSpan.Milliseconds.ToString("D3").Substring(0, 3));
            }
            // Otherwise, display the hours, minutes, seconds, and milliseconds
            else
            {
                return string.Format("{0}:{1}:{2}.{3}", 
                    timeSpan.Hours.ToString(), 
                    timeSpan.Minutes.ToString("D2"), 
                    timeSpan.Seconds.ToString("D2"), 
                    timeSpan.Milliseconds.ToString("D3").Substring(0, 3));
            }
        }

        private string FormatDiffTime(string timeStr)
        {
            TimeSpan timeSpan = TimeSpan.Parse(timeStr);

            // If the time is exactly zero, display "="
            if (timeSpan.TotalSeconds == 0)
            {
                return "=";
            }
            // If the time is less than one second, display "0." followed by the milliseconds
            else if (timeSpan.TotalSeconds < 1)
            {
                return "+ 0." + timeSpan.Milliseconds.ToString("D3").Substring(0, 3);
            }
            // If the time is less than one minute, only display the seconds and milliseconds
            else if (timeSpan.TotalMinutes < 1)
            {
                return string.Format("+ {0}.{1}", 
                    timeSpan.Seconds.ToString("D2"), 
                    timeSpan.Milliseconds.ToString("D3").Substring(0, 3));
            }
            // If the time is less than one hour, only display the minutes, seconds, and milliseconds
            else if (timeSpan.TotalHours < 1)
            {
                return string.Format("+ {0}:{1}.{2}", 
                    timeSpan.Minutes.ToString("D2"), 
                    timeSpan.Seconds.ToString("D2"), 
                    timeSpan.Milliseconds.ToString("D3").Substring(0, 3));
            }
            // Otherwise, display the hours, minutes, seconds, and milliseconds
            else
            {
                return string.Format("+ {0}:{1}:{2}.{3}", 
                    timeSpan.Hours.ToString(), 
                    timeSpan.Minutes.ToString("D2"), 
                    timeSpan.Seconds.ToString("D2"), 
                    timeSpan.Milliseconds.ToString("D3").Substring(0, 3));
            }
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

        private async void CheckAndFillClubLeaderboardDataGrid()
        {
            // Check if all the combo boxes have a selected item
            if (clubNameComboBox.SelectedItem != null && clubEventComboBox.SelectedItem != null && clubStageComboBox.SelectedItem != null)
            {
                // Fetch the leaderboard data
                var leaderboardData = await plugin.FetchClubChampionshipLeaderboardDataAsync(plugin.Settings.ClubID, plugin.Settings.SelectedClubLeaderboardID);

                // Update the current leaderboard data
                currentClubLeaderboardData = leaderboardData;

                // Fill the clubLeaderboardDataGrid with the fetched data
                FillClubLeaderboardDataGrid(leaderboardData);
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

        private async void PreviousClubPageButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the previous cursor from the current club leaderboard data
            string previousCursor = currentClubLeaderboardData["previous"].ToString();

            // Fetch the previous page of club leaderboard data
            var leaderboardData = await plugin.FetchClubChampionshipLeaderboardDataAsync(plugin.Settings.ClubID, plugin.Settings.SelectedClubLeaderboardID, cursor:previousCursor);

            // Fill the clubLeaderboardDataGrid with the fetched data
            FillClubLeaderboardDataGrid(leaderboardData);
        }

        private async void NextClubPageButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the next cursor from the current club leaderboard data
            string nextCursor = currentClubLeaderboardData["next"].ToString();

            // Fetch the next page of club leaderboard data
            var leaderboardData = await plugin.FetchClubChampionshipLeaderboardDataAsync(plugin.Settings.ClubID, plugin.Settings.SelectedClubLeaderboardID, cursor:nextCursor);

            // Fill the clubLeaderboardDataGrid with the fetched data
            FillClubLeaderboardDataGrid(leaderboardData);
        }
        
        private BitmapImage[] GetAssistIcons(int[] assistFlags)
        {
            // Set default icons
            BitmapImage[] icons = new BitmapImage[3]
            {
                LoadImageFromResources("Aaron.PluginRacenetReceiver.icons.0_1.png"),
                LoadImageFromResources("Aaron.PluginRacenetReceiver.icons.0_2.png"),
                LoadImageFromResources("Aaron.PluginRacenetReceiver.icons.0_4.png")
            };

            foreach (int flag in assistFlags)
            {
                switch (flag)
                {
                    case 1:
                        icons[0] = LoadImageFromResources("Aaron.PluginRacenetReceiver.icons.1.png");
                        break;
                    case 2:
                        icons[1] = LoadImageFromResources("Aaron.PluginRacenetReceiver.icons.2.png");
                        break;
                    case 4:
                        icons[2] = LoadImageFromResources("Aaron.PluginRacenetReceiver.icons.4.png");
                        break;
                }
            }
            return icons;
        }

        private BitmapImage LoadImageFromResources(string resourcePath)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.EndInit();
            return image;
        }

        public string LoadJsonFromResources(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream == null)
                {
                    throw new Exception($"Could not find resource {resourcePath}");
                }

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}