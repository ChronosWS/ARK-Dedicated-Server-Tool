using ARK_Server_Manager.Lib.ViewModel;
using System;
using System.Net;
using System.Windows;
using System.Windows.Input;
using ARK_Server_Manager.Lib;
using WPFSharp.Globalizer;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace ARK_Server_Manager
{
    /// <summary>
    /// Interaction logic for OpenRCON.xaml
    /// </summary>
    public partial class OpenRCON : Window
    {
        private GlobalizedApplication _globalizedApplication = GlobalizedApplication.Instance;

        public string ServerIP
        {
            get { return (string)GetValue(ServerIPProperty); }
            set { SetValue(ServerIPProperty, value); }
        }

        public static readonly DependencyProperty ServerIPProperty = DependencyProperty.Register(nameof(ServerIP), typeof(string), typeof(OpenRCON), new PropertyMetadata(IPAddress.Loopback.ToString()));

        public int RCONPort
        {
            get { return (int)GetValue(RCONPortProperty); }
            set { SetValue(RCONPortProperty, value); }
        }

        public static readonly DependencyProperty RCONPortProperty = DependencyProperty.Register(nameof(RCONPort), typeof(int), typeof(OpenRCON), new PropertyMetadata(32330));

        public string Password
        {
            get { return (string)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }

        public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(nameof(Password), typeof(string), typeof(OpenRCON), new PropertyMetadata(String.Empty));

        public OpenRCON()
        {
            InitializeComponent();
            WindowUtils.RemoveDefaultResourceDictionary(this);

            LoadDefaults();
            this.DataContext = this;
        }

        public static async Task<int> GetMaxPlayers()
        {

            JsonSerializer serializer = new JsonSerializer();
            using (var client = new WebClient())
            {

                string requestData = JsonConvert.SerializeObject(new
                {
                    api_key = "changeme"
                });
                Uri URI = new Uri("http://ark.zkportfolio.info:8081/getServerData");
                client.Headers.Add("Content-Type", "application/json");
                var response = await client.UploadStringTaskAsync(URI, "POST", requestData);
                JObject query = JObject.Parse(response);
                JObject data = (JObject)query["d"];
                int ret;
                Int32.TryParse(data["maxplayers"].ToString(), out ret);
                return ret;
            }
        }

        public ICommand ConnectCommand => new RelayCommand<object>(
            execute: async _ =>
            {
                
                // set focus to the Connect button, if the Enter key is pressed, the value just entered has not yet been posted to the property.
                buttonConnect.Focus();

                var window = RCONWindow.GetRCON(new Lib.RCONParameters()
                {
                    ProfileName = String.Format(_globalizedApplication.GetResourceString("OpenRCON_WindowTitle"), ServerIP, RCONPort),
                    ServerIP = ServerIP,
                    RCONPort = RCONPort,
                    AdminPassword = Password,
                    InstallDirectory = String.Empty,
                    RCONWindowExtents = Rect.Empty,
                    MaxPlayers = await GetMaxPlayers()
                });
                window.Owner = this.Owner;
                window.Show();

                SaveDefaults();
                this.Close();
            },
            canExecute: _ => true
        );

        private void LoadDefaults()
        {
            if (!String.IsNullOrWhiteSpace(Config.Default.OpenRCON_ServerIP))
                ServerIP = Config.Default.OpenRCON_ServerIP;
            RCONPort = Config.Default.OpenRCON_RCONPort;
        }
        private void SaveDefaults()
        {
            Config.Default.OpenRCON_ServerIP = ServerIP;
            Config.Default.OpenRCON_RCONPort = RCONPort;
        }
    }
}
