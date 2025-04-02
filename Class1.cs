using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json;
using System.Net.Http;

namespace WombastischesEloPlugin
{
    public class WombastischesEloPlugin : BasePlugin
    {
        public override string ModuleName => "Wombastisches Elo Plugin";
        public override string ModuleVersion => "1.0.1";
        public override string ModuleAuthor => "wombat.";  
        public override string ModuleDescription => "!faceit command displays Faceit Elo for all players on the server";
        
        private PluginConfig Config { get; set; } = new PluginConfig(); 
        private const string ConfigFileName = "WombastischesEloPlugin.json";

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
            AddCommand("css_faceit", "Show Faceit ELO", Command_Faceit);
            DebugLog("Plugin loaded successfully");
        }

        private void LoadConfig()
        {
            var configPath = Path.Combine(ModuleDirectory, ConfigFileName);
            
            if (!File.Exists(configPath))
            {
                Config = new PluginConfig();
                File.WriteAllText(configPath, JsonConvert.SerializeObject(Config, Formatting.Indented));
                DebugLog("Created new config file with default values");
            }
            else
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<PluginConfig>(File.ReadAllText(configPath));
                    Config = config ?? new PluginConfig(); // dumb null check - fix
                    DebugLog("Loaded config file successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading config: {ex.Message}");
                    Config = new PluginConfig();
                }
            }

            if (string.IsNullOrEmpty(Config.FaceitApiKey) || Config.FaceitApiKey == "faceit-api-key-here")
            {
                Console.WriteLine("ERROR: Please set your Faceit API key in the config file!");
            }
        }

        private string GetEloColor(int elo)
        {
            if (elo == -1) return ChatColors.LightPurple.ToString();
            if (elo < 2000) return ChatColors.White.ToString();
            if (elo <= 2750) return ChatColors.Blue.ToString();
            return ChatColors.Red.ToString();
        }

        private void Command_Faceit(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid) return;
            DebugLog($"Faceit command invoked by {player.PlayerName}");
            
            Server.NextFrame(() => HandleFaceitCommand(player));
        }

        private void HandleFaceitCommand(CCSPlayerController player)
        {
            DebugLog("Starting Faceit command processing");

            var allPlayers = Utilities.GetPlayers().Where(p => p != null && p.IsValid).ToList();
            
            var team1Players = allPlayers.Where(p => p.Team == CsTeam.Terrorist).ToList();
            var team2Players = allPlayers.Where(p => p.Team == CsTeam.CounterTerrorist).ToList();
            DebugLog($"Found {team1Players.Count} T players and {team2Players.Count} CT players");

            var team1Data = GetPlayerSteamIdsSafe(team1Players);
            var team2Data = GetPlayerSteamIdsSafe(team2Players);

            Task.Run(async () =>
            {
                var team1Results = await ProcessPlayersParallel(team1Data);
                var team2Results = await ProcessPlayersParallel(team2Data);

                Server.NextFrame(() =>
                {
                    if (!player.IsValid) return;

                    player.PrintToChat($" {ChatColors.Red}### FACEIT ELO RATINGS ###{ChatColors.Default}");
                    PrintTeamResults(player, "TERRORISTS", team1Results);
                    PrintTeamResults(player, "COUNTER-TERRORISTS", team2Results);
                });
            });
        }

        private List<PlayerData> GetPlayerSteamIdsSafe(List<CCSPlayerController> players)
        {
            return players.Where(p => p.IsValid && !p.IsBot)
                .Select(p => new PlayerData(p.PlayerName ?? "Unknown", p.SteamID.ToString())) // dumb null check - fix
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

        private async Task<int> FetchElo(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return -1;
            if (string.IsNullOrEmpty(Config?.FaceitApiKey)) // dumb null check - fix
            {
                DebugLog("API Error: No Faceit API key configured!");
                return -1;
            }

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Config.FaceitApiKey}");
                
                var url = $"https://open.faceit.com/data/v4/players?game=cs2&game_player_id={steamId}";
                DebugLog($"Making API request to: {url}");

                var response = await client.GetStringAsync(url);
                DebugLog($"Received API response: {response.Truncate(200)}");

                dynamic? data = JsonConvert.DeserializeObject(response);
                if (data?.games?.cs2?.faceit_elo == null) return -1;
                
                return (int)data.games.cs2.faceit_elo;
            }
            catch (Exception ex)
            {
                DebugLog($"API Error for {steamId}: {ex.Message}");
                return -1;
            }
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
            public bool DebugMode { get; set; } = true;
            public string FaceitApiKey { get; set; } = "faceit-api-key-here";
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