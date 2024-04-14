using System;
using System.Collections.Generic;
using System.Net;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using MaxMind.Db;
using Newtonsoft.Json;

namespace ConnectInfo
{
    public class ConnectInfoConfig : BasePluginConfig
    {
        public string GeoLiteLanguage { get; set; } = "ru";
        public bool CityIncluded { get; set; } = true;
        public List<string> ImmunityFlags { get; set; } = [..new[] { "@css/root", "@css/ban" }];
    }

    [MinimumApiVersion(199)]
    public class ConnectInfo : BasePlugin, IPluginConfig<ConnectInfoConfig>
    {
        public override string ModuleName => "Connect Info";
        public override string ModuleVersion => "v1.0.7";
        public override string ModuleAuthor => "gleb_khlebov";

        public override string ModuleDescription => "Information about the player's location when connecting to chat and console";

        public ConnectInfoConfig Config { get; set; } = new();

        private new static readonly string ModuleDirectory = Server.GameDirectory + "/csgo/addons/counterstrikesharp/plugins/ConnectInfo";

        private readonly Reader _reader = new (ModuleDirectory + "/../../shared/GeoLite2-City.mmdb");

        public void OnConfigParsed(ConnectInfoConfig config)
        {
            Config = config;
        }

        public override void Load(bool hotReload)
        {
            base.Load(hotReload);
            
            Console.WriteLine(" ");
            Console.WriteLine("   _____                            _     _____        __      ");
            Console.WriteLine("  / ____|                          | |   |_   _|      / _|     ");
            Console.WriteLine(" | |     ___  _ __  _ __   ___  ___| |_    | |  _ __ | |_ ___  ");
            Console.WriteLine(" | |    / _ \\| '_ \\| '_ \\ / _ \\/ __| __|   | | | '_ \\|  _/ _ \\ ");
            Console.WriteLine(" | |___| (_) | | | | | | |  __/ (__| |_   _| |_| | | | || (_) |");
            Console.WriteLine("  \\_____\\___/|_| |_|_| |_|\\___|\\___|\\__| |_____|_| |_|_| \\___/ ");
            Console.WriteLine("                                                               ");
            Console.WriteLine("                                                               ");
            Console.WriteLine("			>> Version: " + ModuleVersion);
            Console.WriteLine("			>> Author: " + ModuleAuthor);
            Console.WriteLine("         >> MaxMind DB build date: " + _reader.Metadata.BuildDate);
            Console.WriteLine(" ");
        }

        public override void Unload(bool hotReload)
        {
            _reader.Dispose();
            base.Unload(hotReload);
        }

        [GameEventHandler]
        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;

            if (IsInvalidPlayer(player))
                return HookResult.Continue;

            string? ip = player.IpAddress;
            string playerName = player.PlayerName;

            string consoleLogMessage;
            string serverChatMessage;

            string geoInfo = !string.IsNullOrEmpty(ip) ? GetGeoInfo(ip.Split(':')[0]) : string.Empty;

            if (!string.IsNullOrEmpty(geoInfo))
            {
                consoleLogMessage = ReplaceMessageTags(Localizer["ConsoleConnectMessageWithGeo"], playerName, geoInfo);
                serverChatMessage = ReplaceMessageTags(Localizer["ConnectMessageWithGeo"], playerName, geoInfo);
            }
            else
            {
                consoleLogMessage =
                    ReplaceMessageTags(Localizer["ConsoleConnectMessageWithoutGeo"], playerName, string.Empty);
                serverChatMessage =
                    ReplaceMessageTags(Localizer["ConnectMessageWithoutGeo"], playerName, string.Empty);
            }

            Log(consoleLogMessage);
            Server.NextFrame(() => Server.PrintToChatAll(serverChatMessage));

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;

            if (IsInvalidPlayer(player))
                return HookResult.Continue;

            string disconnectMessage = ReplaceMessageTags(Localizer["DisconnectMessage"], player.PlayerName, string.Empty);
            Server.NextFrame(() => Server.PrintToChatAll(disconnectMessage));

            return HookResult.Continue;
        }

        private string GetGeoInfo(string ip)
        {
            try
            {
                var parsedIp = IPAddress.Parse(ip);
                var data = _reader.Find<Dictionary<string, object>>(parsedIp);
                var json = JsonConvert.SerializeObject(data);

                var geoInfo = JsonConvert.DeserializeObject<dynamic>(json)!;
                string city;
                string country;
                switch (Config.GeoLiteLanguage)
                {
                    case "ru":
                        country = geoInfo.country.names.ru;
                        city = geoInfo.city.names.ru;
                        break;
                    case "de":
                        country = geoInfo.country.names.de;
                        city = geoInfo.city.names.de;
                        break;
                    case "es":
                        country = geoInfo.country.names.es;
                        city = geoInfo.city.names.es;
                        break;
                    case "ja":
                        country = geoInfo.country.names.ja;
                        city = geoInfo.city.names.ja;
                        break;
                    case "fr":
                        country = geoInfo.country.names.fr;
                        city = geoInfo.city.names.fr;
                        break;
                    case "en":
                        country = geoInfo.country.names.en;
                        city = geoInfo.city.names.en;
                        break;
                    default:
                        country = geoInfo.country.names.en;
                        city = geoInfo.city.names.en;
                        break;
                }

                string result = null!;

                if (!string.IsNullOrEmpty(country))
                {
                    result = country;
                }

                if (!string.IsNullOrEmpty(result) && !string.IsNullOrEmpty(city) && Config.CityIncluded)
                {
                    result += ", " + city;
                }

                return result;
            }
            catch (Exception)
            {
                Log($"Could not find data for IP {ip} in the MaxMind database");
            }

            return null!;
        }

        private static string ReplaceMessageTags(string message, string player, string geoInfo)
        {
            var replacedMessage = message
                .Replace("{PLAYERNAME}", player)
                .Replace("{GEOINFO}", geoInfo);

            return replacedMessage;
        }

        private bool IsInvalidPlayer(CCSPlayerController player)
        {
            return Config.ImmunityFlags.Count >= 1 && AdminManager.PlayerHasPermissions(player, Config.ImmunityFlags.ToArray());
        }

        private void Log(string message)
        {
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(Localizer["LogPrefix"] + message);
            Console.ResetColor();
        }
    }
}