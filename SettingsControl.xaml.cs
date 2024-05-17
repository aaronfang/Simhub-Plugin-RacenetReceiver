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
        private bool hasLoaded = false;

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

            // Restore user's selections
            // selectedLocation = plugin.Settings.SelectedLocation ?? locationComboBox.Items[0].ToString();
            // selectedStage = plugin.Settings.SelectedStage ?? stageComboBox.Items[0].ToString();
            // selectedCarClass = plugin.Settings.SelectedCarClass ?? carClassComboBox.Items[0].ToString();
            // selectedSurfaceCondition = plugin.Settings.SelectedSurfaceCondition ?? surfaceConditionComboBox.Items[0].ToString();
            // locationComboBox.SelectedItem = selectedLocation;
            // stageComboBox.SelectedItem = selectedStage;
            // carClassComboBox.SelectedItem = selectedCarClass;
            // surfaceConditionComboBox.SelectedItem = selectedSurfaceCondition;

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
                var selectedItem = locationComboBox.SelectedItem;

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
    }
}