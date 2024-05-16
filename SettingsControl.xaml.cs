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
        private Dictionary<string, dynamic> timeTrialPreInfoDataJson;
        private Dictionary<string, string> locations;

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

        private dynamic findValueFromJObject(JObject jObject, string key)
        {
            foreach (var pair in jObject)
            {
                if (pair.Key == key)
                {
                    return pair.Value;
                }
                if (pair.Value is JObject)
                {
                    var value = findValueFromJObject((JObject)pair.Value, key);
                    if (value != null)
                    {
                        return pair.Value;
                    }
                }
            }
            return null;
        }

        private dynamic findListFromJObject(JObject jObject, string key)
        {
            List<JToken> allData = jObject.Children().Children().ToList();
            var listData = allData.FirstOrDefault(x => x.Path == key);

            return listData.Values();
        }


        private void SettingsControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Assuming pluginManager is accessible in this context
            var timeTrialPreInfoData = (string)(plugin.PluginManager.GetPropertyValue("RacenetDataReceiver.Racenet.rawData.timeTrialPreInfo"));


            if (timeTrialPreInfoData != null)
            {
                // Deserialize the JSON string to a dictionary, where the key is the location ID and the value is the location name
                //timeTrialPreInfoDataJson = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(timeTrialPreInfoData);
                JObject data = JObject.Parse(timeTrialPreInfoData);
                var locations = this.findValueFromJObject(data, "locations");
                var location1 = this.findValueFromJObject(locations, "5");
                var locationList = this.findListFromJObject(data, "locations");

                var jsonData = JObject.Parse(timeTrialPreInfoData).Children();
                List<JToken> allData = jsonData.Children().ToList();
                var locationData = allData.FirstOrDefault(x => x.Path == "locations");

                // Fill the locationComboBox with the location names
                locationComboBox.Items.Clear();
                foreach (var location in locationList)
                {
                    locationComboBox.Items.Add(location);
                }

                // Attach the SelectionChanged event handler
                locationComboBox.SelectionChanged += LocationComboBox_SelectionChanged;
            }
        }

        private void LocationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(locationComboBox.SelectedItem == null) return;

            string selectedLocation = locationComboBox.SelectedItem.ToString();

            // Get the location ID for the selected location
            string selectedLocationId = locations.FirstOrDefault(x => x.Value == selectedLocation).Key;

            if (string.IsNullOrEmpty(selectedLocationId))
            {
                Logging.Current.Info($"Location: {selectedLocation} not found in locations dictionary.");
                return;
            }

            // Get the route IDs for the selected location
            var routeIds = timeTrialPreInfoDataJson["locationRoute"][selectedLocationId];

            Logging.Current.Info($"Selected Location: {selectedLocation}, Route IDs: {routeIds}");

            // Clear the stageComboBox
            stageComboBox.Items.Clear();

            // Fill the stageComboBox with the route names
            foreach (var routeId in routeIds)
            {
                var routeName = timeTrialPreInfoDataJson["routes"][routeId];
                stageComboBox.Items.Add(routeName);
            }
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