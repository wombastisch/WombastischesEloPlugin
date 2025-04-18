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
        public override string ModuleVersion => "1.1.0";
        public override string ModuleAuthor => "wombastisch";  
        public override string ModuleDescription => "!faceit command displays Faceit Elo for all players or detailed stats for a specific player";
        
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
                
                DebugLog($"FetchElo API response: {content}");
                
                var data = JsonConvert.DeserializeObject<FaceitResponse>(content);
                return data?.games?.cs2?.faceit_elo ?? -1;
            }
            catch (Exception ex)
            {
                await Server.NextFrameAsync(() => DebugLog($"API exception: {ex.Message}"));
                return -1;
            }
        }

        private async Task<string> GetFaceitPlayerId(string steamId)
        {
            if (!ValidateApiKeySilent()) return string.Empty;
            
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.FaceitApiKey}");
                client.Timeout = TimeSpan.FromSeconds(5);

                var url = $"https://open.faceit.com/data/v4/players?game=cs2&game_player_id={steamId}";
                DebugLog($"API request to get player ID: {url}");

                using var response = await client.GetAsync(url).ConfigureAwait(false);
                
                await Server.NextFrameAsync(() => { }); 
                
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"API error: {response.StatusCode}");
                    return string.Empty;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                // NEU: Debug-Log der kompletten Antwort
                DebugLog($"GetFaceitPlayerId API response: {content}");
                
                dynamic? data = JsonConvert.DeserializeObject(content);
                return data?.player_id?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                await Server.NextFrameAsync(() => DebugLog($"API exception: {ex.Message}"));
                return string.Empty;
            }
        }

        private async Task<FaceitStatsResponse?> FetchPlayerStats(string faceitPlayerId)
        {
            if (!ValidateApiKeySilent() || string.IsNullOrEmpty(faceitPlayerId)) return null;
            
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.FaceitApiKey}");
                client.Timeout = TimeSpan.FromSeconds(5);

                var url = $"https://open.faceit.com/data/v4/players/{faceitPlayerId}/stats/cs2";
                DebugLog($"API request for player stats: {url}");

                using var response = await client.GetAsync(url).ConfigureAwait(false);
                
                await Server.NextFrameAsync(() => { }); 
                
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"API error: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                DebugLog($"FetchPlayerStats API response: {content}");
                
                return JsonConvert.DeserializeObject<FaceitStatsResponse>(content);
            }
            catch (Exception ex)
            {
                await Server.NextFrameAsync(() => DebugLog($"API exception: {ex.Message}"));
                return null;
            }
        }

        private async Task<FaceitMatchStatsResponse?> FetchPlayerMatchStats(string faceitPlayerId, int limit = 30)
        {
            if (!ValidateApiKeySilent() || string.IsNullOrEmpty(faceitPlayerId)) return null;
            
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.FaceitApiKey}");
                client.Timeout = TimeSpan.FromSeconds(10);

                var url = $"https://open.faceit.com/data/v4/players/{faceitPlayerId}/games/cs2/stats?limit={limit}";
                DebugLog($"API request for player match stats: {url}");

                using var response = await client.GetAsync(url).ConfigureAwait(false);
                
                await Server.NextFrameAsync(() => { }); 
                
                if (!response.IsSuccessStatusCode)
                {
                    DebugLog($"API error: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                DebugLog($"FetchPlayerMatchStats API response: {content}");
                
                return JsonConvert.DeserializeObject<FaceitMatchStatsResponse>(content);
            }
            catch (Exception ex)
            {
                await Server.NextFrameAsync(() => DebugLog($"API exception: {ex.Message}"));
                return null;
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
        
        public class FaceitStatsResponse
        {
            public string player_id { get; set; } = string.Empty;
            public string game_id { get; set; } = string.Empty;
            public Lifetime lifetime { get; set; } = new Lifetime();

            public class Lifetime
            {
                [JsonProperty("Average K/D Ratio")]
                public string AverageKdRatio { get; set; } = "0";

                [JsonProperty("K/D Ratio")]
                public string KdRatio { get; set; } = "0";

                [JsonProperty("Total Matches")]
                public string CS2Matches { get; set; } = "0";

                [JsonProperty("Matches")]
                public string AllMatches { get; set; } = "0";

                [JsonProperty("Win Rate %")]
                public string WinRate { get; set; } = "0";
            
                [JsonProperty("Average Headshots %")]
                public string HeadshotPercentage { get; set; } = "0";

                [JsonProperty("ADR")]
                public string ltADR { get; set; } = "0";

                [JsonProperty("Recent Results")]
                public List<string> RecentResults { get; set; } = new List<string>();
            }
        }
        
        public class FaceitMatchStatsResponse
        {
            public List<MatchStatsItem> items { get; set; } = new List<MatchStatsItem>();

            public class MatchStatsItem
            {
                public MatchStats stats { get; set; } = new MatchStats();

                public class MatchStats
                {
                    [JsonProperty("K/D Ratio")]
                    public string KdRatio { get; set; } = "0";
                    public string Result { get; set; } = "0";

                    [JsonProperty("Headshots %")]
                    public string HeadshotPercentage { get; set; } = "0";
                    public string ADR { get; set; } = "0";
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
            
            if (command.ArgCount >= 2)
            {
                string targetPlayerName = command.ArgByIndex(1);
                DebugLog($"Searching for player: {targetPlayerName}");
                Server.NextFrame(() => HandleFaceitPlayerCommand(player, targetPlayerName));
            }
            else
            {
                Server.NextFrame(() => HandleFaceitCommand(player));
            }
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

        private void HandleFaceitPlayerCommand(CCSPlayerController sender, string targetPlayerName)
        {
            DebugLog($"Processing player specific faceit command for player: {targetPlayerName}");

            if (!ValidateApiKeySilent())
            {
                sender.PrintToChat($" {ChatColors.Red}Plugin configuration error! Check server console.");
                return;
            }

            CCSPlayerController? targetPlayer = FindPlayerByName(targetPlayerName);
            
            if (targetPlayer == null)
            {
                sender.PrintToChat($" {ChatColors.Red}Player {targetPlayerName} not found on server.");
                return;
            }

            string steamId = targetPlayer.SteamID.ToString();
            DebugLog($"Found player {targetPlayer.PlayerName} with SteamID: {steamId}");

            Server.NextFrame(async () =>
            {
                try
                {
                    string faceitPlayerId = await GetFaceitPlayerId(steamId);
                    if (string.IsNullOrEmpty(faceitPlayerId))
                    {
                        await Server.NextFrameAsync(() => 
                            sender.PrintToChat($" {ChatColors.Red}Could not find Faceit account for {targetPlayer.PlayerName}.")
                        );
                        return;
                    }

                    var tasks = new Task[]
                    {
                        Task.Run(async () => await FetchElo(steamId)),
                        Task.Run(async () => await FetchPlayerStats(faceitPlayerId)),
                        Task.Run(async () => await FetchPlayerMatchStats(faceitPlayerId, 30))
                    };

                    await Task.WhenAll(tasks);

                    int elo = (int)((Task<int>)tasks[0]).Result;
                    var stats = ((Task<FaceitStatsResponse?>)tasks[1]).Result;
                    var matchStats = ((Task<FaceitMatchStatsResponse?>)tasks[2]).Result;

                    await Server.NextFrameAsync(() =>
                    {
                        if (!sender.IsValid) return;

                        var targetPlayers = GetTargetPlayersForOutput(sender);
                        
                        foreach (var target in targetPlayers)
                        {
                            target.PrintToChat($" {ChatColors.Red}### FACEIT STATS FOR {targetPlayer.PlayerName} ###{ChatColors.Default}");
                            target.PrintToChat($" {ChatColors.Green}Elo: {GetEloColor(elo)}{(elo == -1 ? "N/A" : elo.ToString())}{ChatColors.Default}");
                            
                            if (stats != null)
                            {
                                int.TryParse(stats.lifetime?.AllMatches, out var allMatches);
                                int.TryParse(stats.lifetime?.CS2Matches, out var cs2Matches);
                                int csgoMatches = allMatches - cs2Matches;

                                target.PrintToChat($" {ChatColors.Green}CSGO Matches: {ChatColors.Default}{(allMatches > 0 ? csgoMatches.ToString() : "N/A")}");
                                target.PrintToChat($" {ChatColors.Green}CS2 Matches: {ChatColors.Default}{stats.lifetime?.CS2Matches ?? "N/A"}");
                                target.PrintToChat($" {ChatColors.Green}Win Rate: {ChatColors.Default}{stats.lifetime?.WinRate ?? "N/A"}%");
                                target.PrintToChat($" {ChatColors.Green}K/D: {ChatColors.Default}{stats.lifetime?.AverageKdRatio ?? "N/A"}");
                                target.PrintToChat($" {ChatColors.Green}HS%: {ChatColors.Default}{stats.lifetime?.HeadshotPercentage ?? "N/A"}%");
                                target.PrintToChat($" {ChatColors.Green}ADR: {ChatColors.Default}{stats.lifetime?.ltADR ?? "N/A"}");
                                
                                if (stats.lifetime?.RecentResults != null && stats.lifetime.RecentResults.Any())
                                {
                                    string results = string.Join("", stats.lifetime.RecentResults.Select(r => 
                                        r == "1" ? $"{ChatColors.Green}W" : $"{ChatColors.Red}L"
                                    ));
                                    target.PrintToChat($" {ChatColors.Green}Last Matches: {ChatColors.Default}{results}");
                                }
                            }
                            
                            if (matchStats != null && matchStats.items.Any())
                            {
                                var matches = matchStats.items.Select(i => i.stats).ToList();
                                
                                double avgKD = matches.Average(m => double.TryParse(m.KdRatio, out var kd) ? kd : 0);
                                double avgHS = matches.Average(m => double.TryParse(m.HeadshotPercentage, out var hs) ? hs : 0);
                                double avgADR = matches.Average(m => double.TryParse(m.ADR, out var adr) ? adr : 0);
                                int wins = matches.Count(m => m.Result == "1");
                                
                                target.PrintToChat($" {ChatColors.Yellow}### LAST 30 MATCHES ###{ChatColors.Default}");
                                target.PrintToChat($" {ChatColors.Green}W/L: {ChatColors.Default}{wins}/{matches.Count - wins} ({(wins * 100.0 / matches.Count):F1}%)");
                                target.PrintToChat($" {ChatColors.Green}K/D: {ChatColors.Default}{avgKD:F2}");
                                target.PrintToChat($" {ChatColors.Green}HS%: {ChatColors.Default}{avgHS:F1}%");
                                target.PrintToChat($" {ChatColors.Green}ADR: {ChatColors.Default}{avgADR:F1}");
                            }
                            else
                            {
                                target.PrintToChat($" {ChatColors.Red}Could not retrieve recent match statistics.");
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{ModuleName}] Error: {ex}");
                    await Server.NextFrameAsync(() => 
                        sender.PrintToChat($" {ChatColors.Red}Error retrieving Faceit stats: {ex.Message}")
                    );
                }
            });
        }
        
        private CCSPlayerController? FindPlayerByName(string name)
        {
            return Utilities.GetPlayers()
                .FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && 
                    (p.PlayerName?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false));
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
            public bool DebugMode { get; set; } = false;

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