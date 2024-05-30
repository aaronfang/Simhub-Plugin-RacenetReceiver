using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimHub.Plugins;
using SimHub;
using GameReaderCommon;
using System.Net.Http;
using System.Xml.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;
using System.Net;
using System.Timers;

namespace Aaron.PluginRacenetReceiver
{
    [PluginName("Racenet Receiver")]
    [PluginDescription("Receives data from Racenet.com")]
    [PluginAuthor("Aaron Fang")]
    public class RacenetDataReceiver : IPlugin, IDataPlugin, IWPFSettings
    {
        public PluginManager PluginManager { get; set; }
        public Settings Settings { get; set; }
        private System.Timers.Timer refreshTokenTimer;
        private DateTime lastFetchTimeForTimeTrial = DateTime.MinValue;
        private DateTime lastFetchTimeForClubChampionship = DateTime.MinValue;
        private string access_token;
        public string RefreshToken { get; set; }
        public string ClubName { get; set; }
        private dynamic timeTrialPreInfo;
        private dynamic personalInfo;
        private dynamic clubChampionshipInfo;
        public List<dynamic> ClubListData { get; private set; }
        private string curGame;
        private string vehicleId;
        private string trackName;
        private int VehicleClassId;
        private string VehicleClassName;
        private string trackId;
        private string previousTrackId;

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            curGame = data.GameName;

            if (curGame == "EAWRC23")
            {
                vehicleId = (string)(pluginManager.GetPropertyValue("DataCorePlugin.GameData.CarId"));
                trackName = (string)(pluginManager.GetPropertyValue("DataCorePlugin.GameData.TrackName"));
                trackId = (string)(pluginManager.GetPropertyValue("DataCorePlugin.GameData.TrackId"));
                string newTrackId = (string)(pluginManager.GetPropertyValue("DataCorePlugin.GameData.TrackId"));

                if (string.IsNullOrEmpty(vehicleId) && string.IsNullOrEmpty(trackName) && string.IsNullOrEmpty(trackId))
                {
                    return;
                }else
                {
                    // Split the trackId into 3 parts and destructure
                    try
                    {
                        string[] trackParts = trackId.Split('_');
                        string locationId = trackParts[0];
                        string stageId = trackParts[1];
                        string trackLength = trackParts[2];
                        // Get the vehicle class id and name
                        (int classId, string vehicleClassName) = GetVehicleClassIdAndName(vehicleId);
                        VehicleClassId = classId;
                        VehicleClassName = vehicleClassName;

                        // Fetch leaderboard data if stageId and VehicleClassId are not null
                        if (!string.IsNullOrEmpty(stageId) && VehicleClassId != 0)
                        {
                            // Check if it's been more than a minute since the last fetch
                            if ((DateTime.Now - lastFetchTimeForTimeTrial).TotalMinutes >= 1)
                            {
                                lastFetchTimeForTimeTrial = DateTime.Now;
                                Task.Run(async () =>
                                {
                                    var leaderboardData0 = await FetchTimeTrialLeaderboardDataAsync(int.Parse(stageId), VehicleClassId, 0, 20, false, "");
                                    var leaderboardData1 = await FetchTimeTrialLeaderboardDataAsync(int.Parse(stageId), VehicleClassId, 1, 20, false, "");

                                    // Set the leaderboard data to properties
                                    PluginManager.SetPropertyValue("Racenet.rawData.leaderboard.dry", this.GetType(), JsonConvert.SerializeObject(leaderboardData0, Formatting.Indented));
                                    PluginManager.SetPropertyValue("Racenet.rawData.leaderboard.wet", this.GetType(), JsonConvert.SerializeObject(leaderboardData1, Formatting.Indented));
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Current.Info($"Error parsing trackId: {ex.Message}");
                    }
                }

                // Check if ClubID and SelectedClubLeaderboardID are not empty
                if (!string.IsNullOrEmpty(Settings.ClubID) && !string.IsNullOrEmpty(trackId) && !string.IsNullOrEmpty(vehicleId) && clubChampionshipInfo != null && clubChampionshipInfo.Count != 0)
                {
                    // Check if it's been more than a minute since the last fetch
                    if ((DateTime.Now - lastFetchTimeForClubChampionship).TotalMinutes >= 1 || newTrackId != previousTrackId)
                    {
                        previousTrackId = newTrackId;
                        
                        lastFetchTimeForClubChampionship = DateTime.Now;

                        // Fetch club championship leaderboard data
                        Task.Run(async () =>
                        {
                            try
                            {
                                int stageId = int.Parse(trackId.Split('_')[1]);
                                string locationId = trackId.Split('_')[0];
                                string leaderboardID = GetLeaderboardIdByStageId(stageId);
                                var clubLeaderboardData = await FetchClubChampionshipLeaderboardDataAsync(Settings.ClubID, leaderboardID);

                                // Set the club leaderboard data to property
                                PluginManager.SetPropertyValue("Racenet.rawData.clubLeaderboardCurrentStage", this.GetType(), JsonConvert.SerializeObject(clubLeaderboardData, Formatting.Indented));
                            }
                            catch (Exception ex)
                            {
                                Logging.Current.Error($"Error in Task.Run: {ex.Message}");
                            }
                        });
                    }
                }
                else
                {
                    Logging.Current.Info("ClubID or trackId is empty.");
                }
            }
        }

        public void Init(PluginManager pluginManager)
        {
            // Add properties to the plugin manager
            pluginManager.AddProperty("Racenet.rawData.timeTrialPreInfo", this.GetType(), "-");
            pluginManager.AddProperty("Racenet.rawData.personalInfo", this.GetType(), "-");
            pluginManager.AddProperty("Racenet.rawData.clubListData", this.GetType(), "-");
            pluginManager.AddProperty("Racenet.rawData.leaderboard.dry", this.GetType(), "-");
            pluginManager.AddProperty("Racenet.rawData.leaderboard.wet", this.GetType(), "-");
            pluginManager.AddProperty("Racenet.rawData.clubChampionshipInfo", this.GetType(), "-");
            pluginManager.AddProperty("Racenet.rawData.clubLeaderboardCurrentStage", this.GetType(), "-");

            // Load settings from file
            if (File.Exists("settings.json"))
            {
                string jsonSettings = File.ReadAllText("settings.json");
                Settings settings = JsonConvert.DeserializeObject<Settings>(jsonSettings);
                //RefreshToken = settings.RefreshToken;
                ClubName = settings.ClubName;
            }
            
            DoRefreshToken();

            // Initialize the timer
            refreshTokenTimer = new System.Timers.Timer
            {
                // Set the timer interval to the token's expiration time minus a small buffer time
                // Here we assume the token expires in 1 hour (3600 seconds)
                Interval = (3600) * 1000 // Convert to milliseconds
            };

            // Set the timer event
            refreshTokenTimer.Elapsed += OnTimedEvent;

            // Start the timer
            refreshTokenTimer.Start();
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            DoRefreshToken();
        }
        
        public void End(PluginManager pluginManager)
        {
            // Stop the timer when the plugin is ended
            refreshTokenTimer.Stop();
        }

        public System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            return new SettingsControl(this);
        }

        private async Task<dynamic> SendGetRequestAsync(string url, Dictionary<string, string> headers)
        {
            using (var webClient = new WebClient())
            {
                webClient.Encoding = System.Text.Encoding.UTF8;

                foreach (var header in headers)
                {
                    webClient.Headers.Add(header.Key, header.Value);
                }

                var response = await webClient.DownloadStringTaskAsync(url);
                if (!string.IsNullOrEmpty(response))
                {
                    return JsonConvert.DeserializeObject<dynamic>(response);
                }
                else
                {
                    Logging.Current.Info("Error: Response is null or empty");
                    return null;
                }
            }
        }

        public async void DoRefreshToken(bool checkConfig=true, Action callback=null)
        {
            // Set your API information
            string url = "https://web-api.racenet.com/api/identity/refresh-auth";
            var headers = new Dictionary<string, string>
            {
                { "Cookie", $"RACENET-REFRESH-TOKEN={RefreshToken}" },
                { "User-Agent", "Apifox/1.0.0 (https://apifox.com)" },
                { "Content-Type", "application/json" }
            };
            var data = new
            {
                clientId = "RACENET_1_JS_WEB_APP",
                grantType = "refresh_token",
                redirectUri = "https://racenet.com/oauthCallback"
            };

            using (var webClient = new WebClient())
            {
                Settings settings = new Settings(); ;

                if (File.Exists("settings.json") && checkConfig)
                {
                    string jsonSettings = File.ReadAllText("settings.json");
                    settings = JsonConvert.DeserializeObject<Settings>(jsonSettings);
                    headers["Cookie"] = $"RACENET-REFRESH-TOKEN={settings.RefreshToken}";
                }

                foreach (var header in headers)
                {
                    webClient.Headers.Add(header.Key, header.Value);
                }

                var response = await webClient.UploadStringTaskAsync(url, "POST", JsonConvert.SerializeObject(data));
                var result = JsonConvert.DeserializeObject<dynamic>(response) ?? throw new Exception("The response content was null.");
                Logging.Current.Info("Get access token and new refresh token...");
                access_token = result.access_token;
                RefreshToken = result.refresh_token;

                // Update the refresh token and save the settings
                Logging.Current.Info("Save new refresh token to settings.json...");
                settings.RefreshToken = RefreshToken;
                string jsonToWrite = JsonConvert.SerializeObject(settings);
                File.WriteAllText("settings.json", jsonToWrite);

                // Call the callback function if it is provided
                callback?.Invoke();

                // Check if the token is valid
                if (!string.IsNullOrEmpty(access_token))
                {
                    // Call the methods
                    await FetchTimeTrialPreInfo();
                    await FetchPersonalInfo();
                    await FetchClubListAsync();
                    if (!string.IsNullOrEmpty(settings.ClubID))
                    {
                        await FetchClubChampionshipInfoAsync(settings.ClubID);
                    }

                }
            }
        }

        public async Task FetchTimeTrialPreInfo()
        {
            // Start timing
            var startTime = DateTime.Now;

            string url = "https://web-api.racenet.com/api/wrc2023Stats/values";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {access_token}" },
                { "User-Agent", "Apifox/1.0.0 (https://apifox.com)" }
            };

            var response = await SendGetRequestAsync(url, headers);
            timeTrialPreInfo = response;
            string timeTrialPreInfoJson = JsonConvert.SerializeObject(response, Formatting.Indented);
            PluginManager.SetPropertyValue("Racenet.rawData.timeTrialPreInfo", this.GetType(), timeTrialPreInfoJson);
            
            // End timing and calculate elapsed time
            var endTime = DateTime.Now;
            var elapsedTime = endTime - startTime;
            Logging.Current.Info($"Time Trial Pre Info Fetched in: {elapsedTime.TotalSeconds} seconds");
        }

        public async Task FetchPersonalInfo()
        {
            // Start timing
            var startTime = DateTime.Now;

            var url = "https://web-api.racenet.com/api/identity/secured";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {access_token}" },
                { "User-Agent", "Apifox/1.0.0 (https://apifox.com)" }
            };

            var response = await SendGetRequestAsync(url, headers);
            personalInfo = response;
            string personalInfoJson = JsonConvert.SerializeObject(response, Formatting.Indented);
            PluginManager.SetPropertyValue("Racenet.rawData.personalInfo", this.GetType(), personalInfoJson);
            
            // End timing and calculate elapsed time
            var endTime = DateTime.Now;
            var elapsedTime = endTime - startTime;
            Logging.Current.Info($"Personal Info Fetched in: {elapsedTime.TotalSeconds} seconds");
        }

        public async Task FetchClubListAsync()
        {
            // Start timing
            var startTime = DateTime.Now;
            
            ClubListData = new List<dynamic>();
            int take = 20;
            int skip = 0;

            while (true)
            {
                string url = $"https://web-api.racenet.com/api/wrc2023clubs/memberships/active?take={take}&skip={skip}&includeChampionship=true";
                var headers = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {access_token}" },
                    { "User-Agent", "Apifox/1.0.0 (https://apifox.com)" }
                };

                var response = await SendGetRequestAsync(url, headers);
                if (response != null)
                {
                    ClubListData.AddRange(response.activeMemberships);
                    if (response.activeMemberships.Count < take)
                    {
                        break;
                    }
                    else
                    {
                        skip += take;
                    }
                }
                else
                {
                    break;
                }
            }

            // Print clubListData to log
            string clubListDataJson = JsonConvert.SerializeObject(ClubListData, Formatting.Indented);
            PluginManager.SetPropertyValue("Racenet.rawData.clubListData", this.GetType(), clubListDataJson);

            // End timing and calculate elapsed time
            var endTime = DateTime.Now;
            var elapsedTime = endTime - startTime;
            Logging.Current.Info($"Club List Data Fetched in: {elapsedTime.TotalSeconds} seconds");
        }

        public (int, string) GetVehicleClassIdAndName(string vehicleId)
        {
            string filePath = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "Documents", "My Games", "WRC", "telemetry", "readme", "ids.json");
            string jsonText = File.ReadAllText(filePath);
            JObject jsonObject = JObject.Parse(jsonText);

            JArray vehicles = (JArray)jsonObject["vehicles"];
            foreach (JObject vehicle in vehicles.Cast<JObject>())
            {
                if ((string)vehicle["id"] == vehicleId)
                {
                    int classId = (int)vehicle["class"];
                    string name = (string)vehicle["name"];
                    return (classId, name);
                }
            }

            throw new Exception($"Vehicle with id {vehicleId} not found.");
        }

        public async Task<dynamic> FetchTimeTrialLeaderboardDataAsync(int stageId, int vehicleClassId, int surfaceConditionId, int maxResultCount = 20, bool focusOnMe = false, string cursor = null)
        {
            // Start timing
            var startTime = DateTime.Now;

            string url = $"https://web-api.racenet.com/api/wrc2023Stats/leaderboard/{stageId}/{vehicleClassId}/{surfaceConditionId}?maxResultCount={maxResultCount}&focusOnMe={focusOnMe}&platform=0&cursor={cursor}";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {access_token}" },
                { "User-Agent", "Apifox/1.0.0 (https://apifox.com)" }
            };

            var response = await SendGetRequestAsync(url, headers);
            if (response != null)
            {
                // End timing and calculate elapsed time
                var endTime = DateTime.Now;
                var elapsedTime = endTime - startTime;
                Logging.Current.Info($"Time Trial {surfaceConditionId} Leaderboard Data Fetched in: {elapsedTime.TotalSeconds} seconds");

                return response;
            }
            else
            {
                Logging.Current.Info("Error: Response is null or empty");
                return null;
            }
        }

        public async Task FetchClubChampionshipInfoAsync(string clubID)
        {
            // Start timing
            var startTime = DateTime.Now;

            string url = $"https://web-api.racenet.com/api/wrc2023clubs/{clubID}?includeChampionship=true";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {access_token}" },
                { "User-Agent", "Apifox/1.0.0 (https://apifox.com)" }
            };

            var response = await SendGetRequestAsync(url, headers);
            clubChampionshipInfo = response;
            string clubChampionshipInfoJson = JsonConvert.SerializeObject(response, Formatting.Indented);
            PluginManager.SetPropertyValue("Racenet.rawData.clubChampionshipInfo", this.GetType(), clubChampionshipInfoJson);
            
            // End timing and calculate elapsed time
            var endTime = DateTime.Now;
            var elapsedTime = endTime - startTime;
            Logging.Current.Info($"Club:{clubID} Championship Info Data Fetched in: {elapsedTime.TotalSeconds} seconds");
        }

        public async Task<dynamic> FetchClubChampionshipLeaderboardDataAsync(string clubID, string leaderboardID, int maxResultCount = 20, bool focusOnMe = false, string cursor = null, bool log = true)
        {
            // Start timing
            var startTime = DateTime.Now;

            string url = $"https://web-api.racenet.com/api/wrc2023clubs/{clubID}/leaderboard/{leaderboardID}?SortCumulative=false&MaxResultCount={maxResultCount}&FocusOnMe={focusOnMe}&Platform=0&Cursor={cursor}";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {access_token}" },
                { "User-Agent", "Apifox/1.0.0 (https://apifox.com)" }
            };

            var response = await SendGetRequestAsync(url, headers);
            if (response != null)
            {
                // End timing and calculate elapsed time
                var endTime = DateTime.Now;
                var elapsedTime = endTime - startTime;
                if (log)
                {
                    Logging.Current.Info($"Club:{clubID}, Leaderboard:{leaderboardID} Data Fetched in: {elapsedTime.TotalSeconds} seconds");
                }
                return response;
            }
            else
            {
                Logging.Current.Info("Error: Response is null or empty");
                return null;
            }
        }

        public string GetLeaderboardIdByStageId(int stageId)
        {
            // Assuming clubChampionshipInfo is a JObject that contains the parsed JSON data
            JArray events = (JArray)clubChampionshipInfo["currentChampionship"]["events"];
            foreach (JObject eventObj in events.Cast<JObject>())
            {
                JArray stages = (JArray)eventObj["stages"];
                foreach (JObject stage in stages.Cast<JObject>())
                {
                    if ((int)stage["stageSettings"]["routeID"] == stageId)
                    {
                        return (string)stage["leaderboardID"];
                    }
                }
            }

            throw new Exception($"Stage with id {stageId} not found.");
        }

        public int? GetVehicleClassIdByLeaderboardId(string leaderboardId)
        {
            // Assuming clubChampionshipInfo is a JObject that contains the parsed JSON data
            JArray events = (JArray)clubChampionshipInfo["currentChampionship"]["events"];
            foreach (JObject eventObj in events.Cast<JObject>())
            {
                JArray stages = (JArray)eventObj["stages"];
                foreach (JObject stage in stages.Cast<JObject>())
                {
                    if ((string)stage["leaderboardID"] == leaderboardId)
                    {
                        return (int)eventObj["eventSettings"]["vehicleClassID"];
                    }
                }
            }

            // Return null if no matching leaderboardId is found
            return null;
        }
    }

    public class JsonDataHelper
    {
        private readonly ILookup<string, string> data;

        public JsonDataHelper(string jsonData)
        {
            JObject jsonObject = JObject.Parse(jsonData);

            // Flatten the JSON structure into a lookup
            data = jsonObject.Descendants()
                .OfType<JProperty>()
                .ToLookup(prop => prop.Name, prop => prop.Value.ToString());
        }

        public string GetValueByKey(string key)
        {
            return data[key].FirstOrDefault();
        }

        public string GetKeyByValue(string value)
        {
            return data.FirstOrDefault(group => group.Contains(value)).Key;
        }

        public IEnumerable<string> GetAllKeys()
        {
            return data.Select(group => group.Key);
        }
    }
}