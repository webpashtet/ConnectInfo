using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using MaxMind.Db;
using System.Net;
using Newtonsoft.Json;

namespace ConnectInfo
{
    public class ConnectInfoConfig : BasePluginConfig
    {
        [JsonPropertyName("GeoLiteEnglish")] public bool GeoLiteEnglish { get; set; } = false;
        [JsonPropertyName("CityIncluded")] public bool CityIncluded { get; set; } = true;
        [JsonPropertyName("LogPrefix")] public string LogPrefix { get; set; } = "[Connect Info] ";
        [JsonPropertyName("ConnectMessageWithGeo")] public string ConnectMessageWithGeo { get; set; } = "{PURPLE}[INFO] {DEFAULT}Игрок {GRAY}{PLAYERNAME} {DEFAULT} подключается из {GREEN}{GEOINFO} {LIME}[+]";
        [JsonPropertyName("ConnectMessageWithoutGeo")] public string ConnectMessageWithoutGeo { get; set; } = "{PURPLE}[INFO] {DEFAULT}Игрок {GRAY}{PLAYERNAME} {DEFAULT} подключается {LIME}[+]";
        [JsonPropertyName("DisconnectMessage")] public string DisconnectMessage { get; set; } = "{PURPLE}[INFO] {DEFAULT}Игрок {GRAY}{PLAYERNAME} {DEFAULT} вышел с сервера {LIGHTRED}[-]";
        [JsonPropertyName("ConsoleConnectMessageWithGeo")] public string ConsoleConnectMessageWithGeo { get; set; } = "Игрок {PLAYERNAME} подключается из {GEOINFO}";
        [JsonPropertyName("ConsoleConnectMessageWithoutGeo")] public string ConsoleConnectMessageWithoutGeo { get; set; } = "Игрок {PLAYERNAME} подключается";
    }

    [MinimumApiVersion(33)]
    public class ConnectInfo : BasePlugin, IPluginConfig<ConnectInfoConfig>
    {
        public override string ModuleName => "Connect Info";
        public override string ModuleVersion => "v1.0.3";
        public override string ModuleAuthor => "gleb_khlebov";

        public override string ModuleDescription => "Information about the player's location when connecting to chat and console";

        public ConnectInfoConfig Config { get; set; }

        public void OnConfigParsed(ConnectInfoConfig config)
        {
            Config = config;
        }

        public override void Load(bool hotReload)
        {
            Log("Connect Info loaded");
        }

        [GameEventHandler]
        public HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
        {
            var player = new CCSPlayerController(@event.Userid.Handle);
            if (player.PawnBotDifficulty == -1)
            {
                string geoInfo = GetGeoInfo(@event.Address.Split(':')[0]);
                string playerName = @event.Name;

                if (!string.IsNullOrEmpty(geoInfo))
                {
                    string consoleLogMessage =
                        ReplaceMessageTags(Config.ConsoleConnectMessageWithGeo, playerName, geoInfo);
                    Log(consoleLogMessage);
                    string serverChatMessage = ReplaceMessageTags(Config.ConnectMessageWithGeo, playerName, geoInfo);
                    Server.PrintToChatAll($" {serverChatMessage}");
                }
                else
                {
                    string consoleLogMessage =
                        ReplaceMessageTags(Config.ConsoleConnectMessageWithoutGeo, playerName, String.Empty);
                    Log(consoleLogMessage);
                    string serverChatMessage =
                        ReplaceMessageTags(Config.ConnectMessageWithoutGeo, playerName, String.Empty);
                    Server.PrintToChatAll($" {serverChatMessage}");
                }
            }
            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            var player = new CCSPlayerController(@event.Userid.Handle);
            string disconnectMessage = ReplaceMessageTags(Config.DisconnectMessage, player.PlayerName, String.Empty);
            if (player.PawnBotDifficulty == -1)
                Server.PrintToChatAll($" {disconnectMessage}");
            return HookResult.Continue;
        }

        private string GetGeoInfo(string ip)
        {
            try
            {
                var reader = new Reader(ModuleDirectory + "/../../shared/GeoLite2-City.mmdb");
                var parsedIp = IPAddress.Parse(ip);
                var data = reader.Find<Dictionary<string, object>>(parsedIp);
                var json = JsonConvert.SerializeObject(data);
                reader.Dispose();

                var geoInfo = JsonConvert.DeserializeObject<dynamic>(json)!;
                string city;
                string country;
                if (Config.GeoLiteEnglish)
                {
                    country = geoInfo.country.names.en;
                    city = geoInfo.city.names.en;
                }
                else
                {
                    country = geoInfo.country.names.ru;
                    city = geoInfo.city.names.ru;
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
            catch (Exception ex)
            {
                Log(ex.Message);
            }

            return null!;
        }
        
        private string ReplaceMessageTags(string message, string player, string geoinfo)
        {
            var replacedMessage = message
                .Replace("{PLAYERNAME}", player)
                .Replace("{GEOINFO}", geoinfo);

            replacedMessage = ReplaceColorTags(replacedMessage);

            return replacedMessage;
        }
        
        private string ReplaceColorTags(string input)
        {
            string[] colorPatterns =
            {
                "{DEFAULT}", "{RED}", "{LIGHTPURPLE}", "{GREEN}", "{LIME}", "{LIGHTGREEN}", "{LIGHTRED}", "{GRAY}",
                "{LIGHTOLIVE}", "{OLIVE}", "{LIGHTBLUE}", "{BLUE}", "{PURPLE}", "{GRAYBLUE}"
            };
            string[] colorReplacements =
            {
                "\x01", "\x02", "\x03", "\x04", "\x05", "\x06", "\x07", "\x08", "\x09", "\x10", "\x0B", "\x0C", "\x0E",
                "\x0A"
            };

            for (var i = 0; i < colorPatterns.Length; i++)
                input = input.Replace(colorPatterns[i], colorReplacements[i]);

            return input;
        }

        private void Log(string message)
        {
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(Config.LogPrefix + message);
            Console.ResetColor();
        }
    }
}