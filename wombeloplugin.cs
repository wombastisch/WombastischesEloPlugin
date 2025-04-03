using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json;

namespace WombastischesEloPlugin
{
    public class WombastischesEloPlugin : BasePlugin
    {
        public override string ModuleName => "WombastischesEloPlugin";
        public override string ModuleVersion => "1.0.3";
        public override string ModuleAuthor => "wombat.";  
        public override string ModuleDescription => "!faceit command displays Faceit Elo for all players on the server";
        
        private PluginConfig Config { get; set; } = new PluginConfig();
        private const string ConfigFileName = "config.json";
        private void DebugLog(string message)
        {
            if (Config.DebugMode)
            {
                Console.WriteLine($"[{ModuleName}] {DateTime.Now:HH:mm:ss.fff} {message}");
            }
        }

        public override void Load(bool hotReload)
        {
            LoadConfig();
            AddCommand("css_faceit", "Show Faceit ELO", OnFaceitCommand);
            DebugLog("Plugin loaded successfully");
            ValidateApiKey();
        }

        private void LoadConfig()
        {
            var configDir = GetConfigDirectory();
            var configPath = Path.Combine(configDir, ConfigFileName);

            try
            {
                Directory.CreateDirectory(configDir);
                
                if (!File.Exists(configPath))
                {
                    File.WriteAllText(configPath, JsonConvert.SerializeObject(Config, Formatting.Indented));
                    DebugLog($"Created new config file at: {configPath}");
                    NotifyServerAdmin("New config file created! Please configure your Faceit API key.", true);
                }
                else
                {
                    var settings = new JsonSerializerSettings {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    };
                    Config = JsonConvert.DeserializeObject<PluginConfig>(File.ReadAllText(configPath), settings)! 
                        ?? new PluginConfig();
                    DebugLog($"Loaded config from: {configPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
                Config = new PluginConfig();
            }

            ValidateApiKey();
        }

        private void ValidateApiKey()
        {
            if (string.IsNullOrEmpty(Config.FaceitApiKey))
            {
                NotifyServerAdmin("CRITICAL ERROR: Faceit API key is missing in config!", true);
                return;
            }

            if (Config.FaceitApiKey == "faceit-api-key-here")
            {
                NotifyServerAdmin("WARNING: Default Faceit API key detected!", true);
            }
        }

        private void NotifyServerAdmin(string message, bool isError)
        {
            var formattedMessage = $"[{ModuleName}] {(isError ? "ERROR: " : "")}{message}";
            Console.WriteLine(formattedMessage);

            foreach (var admin in Utilities.GetPlayers().Where(p => 
                p != null && 
                p.IsValid && 
                AdminManager.PlayerHasPermissions(p, "@css/admin")))
            {
                admin.PrintToChat($" {ChatColors.Red}{message}");
            }
        }

        private string GetConfigDirectory()
        {
            return Path.Combine(
                Server.GameDirectory, "csgo", "addons", "counterstrikesharp", 
                "configs", "plugins", ModuleName              
            );
        }
        private async Task<int> FetchElo(string steamId)
        {
            if (!ValidateApiKeySilent()) return -1;
            
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.FaceitApiKey}");
                client.Timeout = TimeSpan.FromSeconds(5);

                var url = $"https://open.faceit.com/data/v4/players?game=cs2&game_player_id={steamId}";
                DebugLog($"API request to: {url}");

                using var response = await client.GetAsync(url).ConfigureAwait(false);
        
                await Server.NextFrameAsync(() => { }); 
        
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"API error: {response.StatusCode}");
                    return -1;
                }

                var content = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<FaceitResponse>(content);
                return data?.games?.cs2?.faceit_elo ?? -1;
            }
            catch (Exception ex)
            {
                await Server.NextFrameAsync(() => DebugLog($"API exception: {ex.Message}"));
                return -1;
            }
        }
        public class FaceitResponse
        {
            public Games? games { get; set; }
            public class Games
            {
                public CS2? cs2 { get; set; }
                public class CS2
                {
                    public int faceit_elo { get; set; }
                }
            }
        }
        private void OnFaceitCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid) return;

            if (!IsPlayerAdmin(player, Config.RequiredPermissions.ToArray()))
            {
                SendPlayerNotAdminMessage(player);
                return;
            }

            DebugLog($"Faceit command invoked by {player.PlayerName}");
            Server.NextFrame(() => HandleFaceitCommand(player));
        }

        private bool IsPlayerAdmin(CCSPlayerController? player, params string[] permissions)
        {
            if (player == null || !player.IsValid) return false;
            
            if (permissions.All(string.IsNullOrEmpty)) return true;

            return permissions.Any(p => 
                !string.IsNullOrEmpty(p) && 
                AdminManager.PlayerHasPermissions(player, p)
            );
        }

        private void SendPlayerNotAdminMessage(CCSPlayerController? player)
        {
            player?.PrintToChat($" {ChatColors.Red}You don't have permission to use this command!");
        }

        private void HandleFaceitCommand(CCSPlayerController player)
        {
            DebugLog("Starting Faceit command processing");

            if (!ValidateApiKeySilent())
            {
                player.PrintToChat($" {ChatColors.Red}Plugin configuration error! Check server console.");
                return;
            }

            Server.NextFrame(async () =>
            {
                try
                {
                    var allPlayers = Utilities.GetPlayers()
                        .Where(p => p != null && p.IsValid)
                        .ToList();

                    var team1Players = allPlayers.Where(p => p.Team == CsTeam.Terrorist).ToList();
                    var team2Players = allPlayers.Where(p => p.Team == CsTeam.CounterTerrorist).ToList();

                    var team1Data = GetPlayerSteamIdsSafe(team1Players);
                    var team2Data = GetPlayerSteamIdsSafe(team2Players);

                    var results = await Task.WhenAll(
                        ProcessPlayersParallel(team1Data),
                        ProcessPlayersParallel(team2Data)
                    ).ConfigureAwait(false);

                    var team1Results = results[0];
                    var team2Results = results[1];

                    await Server.NextFrameAsync(() =>
                    {
                        if (!player.IsValid) return;

                        var targetPlayers = GetTargetPlayersForOutput(player);

                        foreach (var target in targetPlayers)
                        {
                            target.PrintToChat($" {ChatColors.Red}### FACEIT ELO RATINGS ###{ChatColors.Default}");
                            PrintTeamResults(target, "TERRORISTS", team1Results);
                            PrintTeamResults(target, "COUNTER-TERRORISTS", team2Results);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{ModuleName}] Error: {ex}");
                }
            });
        }

        private List<CCSPlayerController> GetTargetPlayersForOutput(CCSPlayerController sender)
        {
            var allPlayers = Utilities.GetPlayers()
                .Where(p => p != null && p.IsValid)
                .ToList();

            return Config.OutputVisibility switch
            {
                "self" => new List<CCSPlayerController> { sender },
                "admin" => allPlayers.Where(p => IsPlayerAdmin(p, Config.RequiredPermissions.ToArray())).ToList(),
                "all" => allPlayers,
                _ => new List<CCSPlayerController> { sender }
            };
        }


        private bool ValidateApiKeySilent()
        {
            if (string.IsNullOrEmpty(Config.FaceitApiKey) || 
                Config.FaceitApiKey == "faceit-api-key-here")
            {
                Console.WriteLine($"[{ModuleName}] CRITICAL: Invalid API key configuration!");
                return false;
            }
            return true;
        }

        private List<PlayerData> GetPlayerSteamIdsSafe(List<CCSPlayerController> players)
        {
            return players.Where(p => p.IsValid && !p.IsBot)
                .Select(p => new PlayerData(p.PlayerName ?? "Unknown", p.SteamID.ToString()))
                .ToList();
        }

        private async Task<List<EloResult>> ProcessPlayersParallel(List<PlayerData> players)
        {
            var tasks = players.Select(async p =>
            {
                try
                {
                    var elo = await FetchElo(p.SteamId);
                    return new EloResult(
                        p.PlayerName,
                        elo == -1 ? "N/A" : elo.ToString(),
                        GetEloColor(elo)
                    );
                }
                catch
                {
                    return new EloResult(p.PlayerName, "N/A", ChatColors.LightPurple.ToString());
                }
            });

            return (await Task.WhenAll(tasks)).ToList();
        }
        private string GetEloColor(int elo)
        {
            if (elo == -1) return ChatColors.LightPurple.ToString();
            if (elo < 2000) return ChatColors.White.ToString();
            if (elo <= 2750) return ChatColors.Blue.ToString();
            return ChatColors.Red.ToString();
        }

        private void PrintTeamResults(CCSPlayerController player, string teamName, List<EloResult> results)
        {
            player.PrintToChat($" {ChatColors.Orange}=== {teamName} ==={ChatColors.Default}");
            foreach (var result in results)
            {
                player.PrintToChat($"  {ChatColors.Green}{result.PlayerName}" +
                                $"{ChatColors.Default} - " +
                                $"{result.Color}{result.Elo}{ChatColors.Default}");
            }
        }

        private record PlayerData(string PlayerName, string SteamId);
        private record EloResult(string PlayerName, string Elo, string Color);
        
        public class PluginConfig
        {
            [JsonProperty("DebugMode (Set to false to disable DebugMode)")]
            public bool DebugMode { get; set; } = true;

            [JsonProperty("FaceitApiKey (Get Faceit API key: https://developer.faceit.com)")]
            public string FaceitApiKey { get; set; } = "faceit-api-key-here";

            [JsonProperty("RequiredPermissions (Leave empty if everyone on the server should be able to use !faceit command)")]
            public List<string> RequiredPermissions { get; set; } = new List<string> { "@custom/faceit", "@css/admin" };

            [JsonProperty("OutputVisibility (Options: 'self', 'admin', 'all')")]
            public string OutputVisibility { get; set; } = "self";
        }

    }
    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength] + "...";
        }
    }
}