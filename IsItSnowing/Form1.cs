using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using IsItSnowing.Models;

namespace IsItSnowing
{
    public partial class Form1 : Form
    {
        private static string ApiUrl;
        private static Config AppConfig;
        public static string CacheFolder;
        public const int UpdateInterval = 150000;

        public CurrentConditionsWeatherResponse CurrentWeather;
        public Forecast Forecast;


        public Form1()
        {
            InitializeComponent();

            AppConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(System.IO.File.ReadAllText(System.IO.Directory.GetCurrentDirectory() + "\\Config.json"));
            if (AppConfig.ApiKey == null)
                throw new ArgumentNullException("Missing API KEY in Config.json file");
            ApiUrl = string.Format("http://api.wunderground.com/api/{0}/", AppConfig.ApiKey);

            CacheFolder = System.IO.Directory.GetCurrentDirectory() + "\\CachedWeather";
            System.IO.Directory.CreateDirectory(CacheFolder);
            timer1.Interval = UpdateInterval;
            backgroundWorker1.RunWorkerAsync();
            timer1.Start();
        }

        public void GetWeather()
        {           

            CurrentWeather = GetCurrentWeather(AppConfig.City, AppConfig.State);
            Forecast = GetForecast(AppConfig.City, AppConfig.State);
        }

        public void CacheCurrentWeather(Models.CurrentConditionsWeatherResponse weather)
        {
            var file = CacheFolder + "\\CurrentWeather.json";
            System.IO.File.WriteAllText(file, Newtonsoft.Json.JsonConvert.SerializeObject(weather));
        }

        public JsonSerializerSettings JsonSerializerSettings()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Formatting = Formatting.Indented;
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            return settings;
        }

        public void CacheForecast(Models.Forecast forecast)
        {
            var file = CacheFolder + "\\Forecast.json";
            System.IO.File.WriteAllText(file, Newtonsoft.Json.JsonConvert.SerializeObject(forecast));
        }

        public Models.CurrentConditionsWeatherResponse GetCurrentWeather(string city, string state)
        {
            var cachedFile = new FileInfo(CacheFolder + "\\CurrentWeather.json");
            Models.CurrentConditionsWeatherResponse weather = null;
            if (cachedFile.Exists && (DateTime.Now - cachedFile.LastWriteTime).TotalSeconds < UpdateInterval)
            {
                weather = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.CurrentConditionsWeatherResponse>(File.ReadAllText(cachedFile.FullName), JsonSerializerSettings());
            }
            else
            {
                var client = new System.Net.WebClient {Proxy = {Credentials = CredentialCache.DefaultCredentials}};
                var weatherJson = client.DownloadString($"{ApiUrl}conditions/q/{state}/{city}.json");
                weather = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.CurrentConditionsWeatherResponse>(weatherJson, JsonSerializerSettings());
                if (weather.current_observation != null)
                    CacheCurrentWeather(weather);
                else
                    MessageBox.Show($"Weather returned null, please check your API key, city, or state in the Config.json file. Current API Key: {AppConfig.ApiKey}");
            }

            return weather;
        }

        public Models.Forecast GetForecast(string city, string state)
        {
            var cachedFile = new FileInfo(CacheFolder + "\\Forecast.json");
            Models.Forecast forecast = null;
            if (cachedFile.Exists && (DateTime.Now - cachedFile.LastWriteTime).TotalSeconds < UpdateInterval)
            {
                forecast = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.Forecast>(File.ReadAllText(cachedFile.FullName), JsonSerializerSettings());
            }
            else
            {
                var client = new System.Net.WebClient {Proxy = {Credentials = CredentialCache.DefaultCredentials}};
                var weatherJson = client.DownloadString($"{ApiUrl}forecast/q/{state}/{city}.json");
                forecast = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.Forecast>(weatherJson, JsonSerializerSettings());
                if (forecast.simpleforecast != null)
                    CacheForecast(forecast);
                else
                    MessageBox.Show($"Forecast returned null, please check your API key, city, or state in the Config.json file. Current API Key: {AppConfig.ApiKey}");                
            }
            return forecast;
        }

        public void RefreshWeather(BackgroundWorker worker)
        {
            worker.ReportProgress(0, "Getting weather");
            GetWeather();
            worker.ReportProgress(100, "Displaying weather");
        }

        public void UpdateUI()
        {
            if (CurrentWeather.current_observation != null)
            {
                var tempInF = Math.Round(CurrentWeather.current_observation.temp_f);
                lblCurrentTemp.Text = tempInF.ToString() + "°F";
                if (tempInF <= 32)
                    lblCurrentTemp.ForeColor = Color.Aqua;
                else
                    lblCurrentTemp.ForeColor = Color.DarkSlateGray;
                var isSnowing = (CurrentWeather.current_observation.icon.ToLower().Contains("snow") || CurrentWeather.current_observation.icon.ToLower().Contains("flur"));
                lblSnowing.Text = isSnowing ? "Yes" : "No";

                if (isSnowing)
                {
                    lblSnowing.ForeColor = Color.WhiteSmoke;
                    lblSnowing.BackColor = Color.Black;
                }
                else
                {
                    lblSnowing.ForeColor = Color.Black;
                    lblSnowing.BackColor = Color.Transparent;
                }

                lblStatus.Text = $"Refreshed {DateTime.Now.ToString("h:mm:ss tt").ToUpper()}";
            }
            else
            {
                lblStatus.Text = $"Error getting weather";
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            backgroundWorker1.RunWorkerAsync();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = (BackgroundWorker)sender;
            RefreshWeather(worker);
        }

        private void backgroundWorker1_ProgressChanged_1(object sender, ProgressChangedEventArgs e)
        {
            var output = $"{DateTime.Now.ToString("hh:mm:ss")} {e.UserState}";
            lblStatus.Text = output;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateUI();
        }

        private void lblCurrentTemp_DoubleClick(object sender, EventArgs e)
        {
            backgroundWorker1.RunWorkerAsync();
        }
    }
}
