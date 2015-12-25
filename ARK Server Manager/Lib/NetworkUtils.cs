using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ARK_Server_Manager.Lib
{
    public class NetworkAdapterEntry
    {
        public NetworkAdapterEntry(IPAddress address, string description)
        {
            this.IPAddress = address.ToString();
            this.Description = description;
        }

        public NetworkAdapterEntry(string address, string description)
        {
            this.IPAddress = address;
            this.Description = description;
        }

        public string IPAddress
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }
    }

    public static class NetworkUtils
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();
        public static List<NetworkAdapterEntry> GetAvailableIPV4NetworkAdapters()
        {
            List<NetworkAdapterEntry> adapters = new List<NetworkAdapterEntry>();
            
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach(var ifc in interfaces)
            {
                var ipProperties = ifc.GetIPProperties();
                if(ipProperties != null)
                {
                    adapters.AddRange(ipProperties.UnicastAddresses.Select(a => a.Address)
                                                                   .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && 
                                                                               !IPAddress.IsLoopback(a))
                                                                   .Select(a => new NetworkAdapterEntry(a, ifc.Description)));
                }
            }

            return adapters;
        }

        public static async Task<Version> GetLatestASMVersion()
        {
            using (var webClient = new WebClient())
            {
                try
                {
                    var latestVersion = await webClient.DownloadStringTaskAsync(Config.Default.LatestASMVersionUrl);
                    return Version.Parse(latestVersion);
                }
                catch (Exception ex)
                {
                    logger.Debug(String.Format("Exception checking for ASM version: {0}\r\n{1}", ex.Message, ex.StackTrace));
                    return new Version();
                }
            }
        }

        public static NetworkAdapterEntry GetPreferredIP(IEnumerable<NetworkAdapterEntry> adapters)
        {
            //
            // Try for a 192.168. address first
            //
            var preferredIp = adapters.FirstOrDefault(a => a.IPAddress.StartsWith("192.168."));
            if (preferredIp == null)
            {
                //
                // Try a 10.0 address next
                //
                preferredIp = adapters.FirstOrDefault(a => a.IPAddress.StartsWith("10.0."));
                if (preferredIp == null)
                {
                    // 
                    // Sad.  Just take the first.
                    //
                    preferredIp = adapters.FirstOrDefault();
                }
            }

            return preferredIp;
        }

        public static async Task<string> DiscoverPublicIPAsync()
        {
            using (var webClient = new WebClient())
            {
                var publicIP = await webClient.DownloadStringTaskAsync(Config.Default.PublicIPCheckUrl);
                IPAddress address;
                if(IPAddress.TryParse(publicIP, out address))
                {
                    return publicIP;
                }

                return String.Empty;
            }
        }

        public class AvailableVersion
        {
            public AvailableVersion()
            {
                IsValid = false;
                Current = new Version(0, 0);
                Upcoming = new Version(0, 0);
                UpcomingETA = "unknown";
            }

            public bool IsValid
            {
                get;
                set;
            }

            public Version Current
            {
                get;
                set;
            }

            public Version Upcoming
            {
                get;
                set;
            }

            public string UpcomingETA
            {
                get;
                set;
            }
        }

        private static bool ParseArkVersionString(string versionString, out Version ver)
        {
            var versionMatch = new Regex(@"[^\d]*(?<version>\d*(\.\d*)?)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture).Match(versionString);
            if (versionMatch.Success)
            {
                return Version.TryParse(versionMatch.Groups["version"].Value, out ver);
            }

            ver = new Version();
            return false;
        }

        public static async Task<AvailableVersion> GetLatestAvailableVersion()
        {
            AvailableVersion result = new AvailableVersion();
            try
            {
                string jsonString;                
                using (var client = new WebClient())
                {
                    jsonString = await client.DownloadStringTaskAsync(Config.Default.AvailableVersionUrl);
                }
                JObject query = JObject.Parse(jsonString);

                var availableVersion = query.SelectToken("current");
                var upcomingVersion = query.SelectToken("upcoming.version");
                var upcomingStatus = query.SelectToken("upcoming.status");

                if (availableVersion != null)
                {
                    Version ver;
                    string versionString = availableVersion.ToString();
                    if(versionString.IndexOf('.') == -1)
                    {
                        versionString = versionString + ".0";
                    }
                    result.IsValid = Version.TryParse(versionString, out ver);
                    result.Current = ver;
                }

                if (upcomingVersion != null)
                {
                    Version ver;
                    string versionString = upcomingVersion.ToString();
                    if (versionString.IndexOf('.') == -1)
                    {
                        versionString = versionString + ".0";
                    }
                    Version.TryParse(versionString, out ver);
                    result.Upcoming = ver;
                }

                if (upcomingStatus != null)
                {
                    result.UpcomingETA = upcomingStatus.ToString();
                }

                // if successful then exist here and return the result
                return result;
            }
            catch (Exception ex)
            {
                logger.Debug(String.Format("Exception checking for version (method 1): {0}\r\n{1}", ex.Message, ex.StackTrace));                
            }

            try
            {
                string webString;
                using (var client = new WebClient())
                {
                    webString = await client.DownloadStringTaskAsync(Config.Default.AvailableVersionUrl2);
                }

                if (!string.IsNullOrWhiteSpace(webString))
                {
                    Version ver;
                    string versionString = webString.ToString();
                    if (versionString.IndexOf('.') == -1)
                    {
                        versionString = versionString + ".0";
                    }
                    result.IsValid = Version.TryParse(versionString, out ver);
                    result.Current = ver;
                }
            }
            catch (Exception ex)
            {
                logger.Debug(String.Format("Exception checking for version (method 2): {0}\r\n{1}", ex.Message, ex.StackTrace));
            }

            return result;
        }

        public class ServerNetworkInfo
        {
            public ServerNetworkInfo()
            {
                Name = "unknown";
                Version = new Version(0, 0);
                Map = "unknown";
                Players = 0;
                MaxPlayers = 0;
            }

            public string Name
            {
                get;
                set;
            }

            public Version Version
            {
                get;
                set;
            }

            public string Map
            {
                get;
                set;
            }

            public int Players
            {
                get;
                set;
            }

            public int MaxPlayers
            {
                get;
                set;
            }
        }

        public static async Task<ServerNetworkInfo> GetServerNetworkInfo(IPEndPoint endpoint)
        {
            ServerNetworkInfo result = null;
            try
            {
                string jsonString;
                using (var client = new WebClient())
                {
                    jsonString = await client.DownloadStringTaskAsync(String.Format(Config.Default.ServerStatusUrlFormat, endpoint.Address, endpoint.Port));
                }

                if(jsonString == null)
                {
                    logger.Debug(String.Format("Server info request returned null string for {0}:{1}", endpoint.Address, endpoint.Port));
                    return result;
                }

                JObject query = JObject.Parse(jsonString);
                if(query == null)
                {
                    logger.Debug(String.Format("Server info request failed to parse for {0}:{1} - '{2}'", endpoint.Address, endpoint.Port, jsonString));
                    return null;
                }

                var status = query.SelectToken("status");
                if(status == null || !(bool)status)
                {
                    logger.Debug($"Server at {endpoint.Address}:{endpoint.Port} returned no status or a status of false.");
                    return null;
                }
                var server = query.SelectToken("server");
                if (server.Type == JTokenType.String)
                {
                    logger.Debug(String.Format("Server at {0}:{1} returned status {2}", endpoint.Address, endpoint.Port, (string)server));
                }
                else
                {
                    result = new ServerNetworkInfo();
                    result.Name = (string)query.SelectToken("server.name");
                    Version ver;
                    string versionString = (string)query.SelectToken("server.version");
                    if (versionString.IndexOf('.') == -1)
                    {
                        versionString = versionString + ".0";
                    }

                    Version.TryParse(versionString, out ver);
                    result.Version = ver;
                    result.Map = (string)query.SelectToken("server.map");
                    result.Players = Int32.Parse((string)query.SelectToken("server.playerCount"));
                    result.MaxPlayers = Int32.Parse((string)query.SelectToken("server.playerMax"));
                }
            }
            catch (Exception ex)
            {
                logger.Debug(String.Format("Exception checking status for: {0}:{1} {2}\r\n{3}", endpoint.Address, endpoint.Port, ex.Message, ex.StackTrace));
            }

            return result;
        }
    }
}
