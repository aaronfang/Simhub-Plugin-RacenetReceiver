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


namespace Aaron.PluginRacenetReceiver
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private readonly RacenetDataReceiver plugin;
        // private readonly Settings settings;

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
            // plugin.DoRefreshToken(false);
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