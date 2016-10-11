﻿using Newtonsoft.Json;
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
using System.Globalization;

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
                                                                   .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
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
                    string latestVersion = null;

                    if (App.Instance.BetaVersion)
                        latestVersion = await webClient.DownloadStringTaskAsync(Config.Default.LatestASMBetaVersionUrl);
                    else
                        latestVersion = await webClient.DownloadStringTaskAsync(Config.Default.LatestASMVersionUrl);

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
                    string versionString = Convert.ToString(query.SelectToken("server.version"), CultureInfo.GetCultureInfo(StringUtils.DEFAULT_CULTURE_CODE));
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

        public static ServerNetworkInfo GetServerNetworkInfoDirect(IPEndPoint endpoint)
        {
            ServerNetworkInfo result = null;
            QueryMaster.ServerInfo serverInfo = null;
            ReadOnlyCollection<QueryMaster.Player> players = null;

            try
            {
                using (var server = QueryMaster.ServerQuery.GetServerInstance(QueryMaster.EngineType.Source, endpoint))
                {
                    serverInfo = server.GetInfo();
                    players = server.GetPlayers();
                }

                if (serverInfo != null)
                {
                    result = new ServerNetworkInfo();
                    result.Name = serverInfo.Name;
                    result.Map = serverInfo.Map;
                    result.Players = serverInfo.Players;
                    result.MaxPlayers = serverInfo.MaxPlayers;

                    // get the name and version of the server using regular expression.
                    if (!string.IsNullOrWhiteSpace(result.Name))
                    {
                        var match = Regex.Match(result.Name, @" - \(v([0-9]+\.[0-9]*)\)");
                        if (match.Success && match.Groups.Count >= 2)
                        {
                            // remove the version number from the name
                            result.Name = result.Name.Replace(match.Groups[0].Value, "");

                            // get the version number
                            var serverVersion = match.Groups[1].Value;
                            Version ver;
                            if (!String.IsNullOrWhiteSpace(serverVersion) && Version.TryParse(serverVersion, out ver))
                            {
                                result.Version = ver;
                            }
                        }
                    }
                }

                if (players != null)
                {
                    // set the number of players based on the player list, excludes any players in the list without a valid name.
                    result.Players = players.Count(record => !string.IsNullOrWhiteSpace(record.Name));
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
