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
    public enum DebugLevel
    {
        None,
        Light,
        Full
    }

    public class WombastischesEloPlugin : BasePlugin
    {
        public override string ModuleName => "WombastischesEloPlugin";
        public override string ModuleVersion => "1.0.3";
        public override string ModuleAuthor => "wombat.";
        public override string ModuleDescription => "!faceit and !stats commands display Faceit Elo and 90-day stats for players";

        public PluginConfig Config { get; set; } = new PluginConfig();
        private const string ConfigFileName = "config.json";

        public void DebugLog(string message, DebugLevel level = DebugLevel.Light)
        {
            if (Config.DebugLevel == DebugLevel.None) return;

            if ((int)level <= (int)Config.DebugLevel)
            {
                Console.WriteLine($"[{ModuleName}] {DateTime.Now:HH:mm:ss.fff} {message}");
            }
        }

        private StatsHandler? _statsHandler;

        public override void Load(bool hotReload)
        {
            LoadConfig();
            AddCommand("css_faceit", "Show Faceit ELO", OnFaceitCommand);
            AddCommand("css_stats", "Show player 90-day stats", OnStatsCommand);
            DebugLog("Plugin loaded successfully", DebugLevel.Light);
            ValidateApiKey();

            _statsHandler = new StatsHandler(this);  
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
                    DebugLog($"Created new config file at: {configPath}", DebugLevel.Light);
                    NotifyServerAdmin("New config file created! Please configure your Faceit API key.", true);
                }
                else
                {
                    var settings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    };
                    Config = JsonConvert.DeserializeObject<PluginConfig>(File.ReadAllText(configPath), settings) ?? new PluginConfig();
                    DebugLog($"Loaded config from: {configPath}", DebugLevel.Light);
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
                DebugLogIfFull($"FetchElo: API request to: {url}");

                using var response = await client.GetAsync(url).ConfigureAwait(false);

                await Server.NextFrameAsync(() => { });

                string content = await response.Content.ReadAsStringAsync();
                DebugLogIfFull($"FetchElo: API Response Code: {response.StatusCode}");
                DebugLogIfFull($"FetchElo: API Response Content: {content}");

                if (!response.IsSuccessStatusCode)
                {
                    DebugLogIfFull($"FetchElo: API error {response.StatusCode}. Full response: {content}");
                    return -1;
                }

                var data = JsonConvert.DeserializeObject<FaceitResponse>(content);
                int elo = data?.games?.cs2?.faceit_elo ?? -1;
                DebugLogIfFull($"FetchElo: Fetched Elo = {elo}");

                return elo;
            }
            catch (Exception ex)
            {
                await Server.NextFrameAsync(() => DebugLogIfFull($"FetchElo: API exception: {ex.Message}"));
                return -1;
            }
        }

        private void DebugLogIfFull(string message)
        {
            if (Config.DebugLevel == DebugLevel.Full)
            {
                DebugLog(message, DebugLevel.Full);
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

            DebugLog($"Faceit command invoked by {player.PlayerName}", DebugLevel.Light);
            Server.NextFrame(() => HandleFaceitCommand(player));
        }

        private async void OnStatsCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid) return;
            if (!IsPlayerAdmin(player, Config.RequiredPermissions.ToArray()))
            {
                SendPlayerNotAdminMessage(player);
                return;
            }

            DebugLog($"Stats command invoked by {player.PlayerName}", DebugLevel.Light);
            if (_statsHandler != null)
            {
                await _statsHandler.HandleStatsCommand(player, command);
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
            DebugLog("Starting Faceit command processing", DebugLevel.Light);

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
            return !string.IsNullOrEmpty(Config.FaceitApiKey);
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
            public DebugLevel DebugLevel { get; set; } = DebugLevel.Light;
            public string FaceitApiKey { get; set; } = "";
            public List<string> RequiredPermissions { get; set; } = new List<string> { "@custom/faceit", "@css/admin" };
            public string OutputVisibility { get; set; } = "self";
        }
    }

        public class StatsHandler : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly WombastischesEloPlugin _plugin;

        public StatsHandler(WombastischesEloPlugin plugin)
        {
            _plugin = plugin;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                
            if (!string.IsNullOrEmpty(_plugin.Config.FaceitApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_plugin.Config.FaceitApiKey}");
            }
            else
            {
                _plugin.DebugLog("WARNING: Faceit API key not configured - stats will not work!", DebugLevel.Light);
            }
        }

        public async Task HandleStatsCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid) return;

            if (string.IsNullOrEmpty(_plugin.Config.FaceitApiKey))
            {
                Server.NextFrame(() => player.PrintToChat($"{ChatColors.Red}Faceit API key not configured! Check server console."));
                _plugin.DebugLog("Stats command blocked - no Faceit API key configured", DebugLevel.Light);
                return;
            }

            try
            {
                var targetName = command.ArgByIndex(1);
                var targetPlayer = Utilities.GetPlayers()
                    .FirstOrDefault(p => p.IsValid &&
                                        !p.IsBot &&
                                        p.PlayerName.Equals(targetName, StringComparison.OrdinalIgnoreCase));

                if (targetPlayer == null)
                {
                    Server.NextFrame(() => player.PrintToChat($"{ChatColors.Red}Player not found!"));
                    return;
                }

                _plugin.DebugLog($"[StatsHandler] Fetching stats for SteamID: {targetPlayer.SteamID}", DebugLevel.Light);
                var stats = await FetchPlayerStats(targetPlayer.SteamID.ToString());

                Server.NextFrame(() =>
                {
                    if (player.IsValid && stats != null)
                    {
                        // Ausgabe der aggregierten Statistiken
                        player.PrintToChat($" {ChatColors.LightRed}» {ChatColors.Orange}{targetPlayer.PlayerName}'s 90-Day Stats");
                        player.PrintToChat($" {ChatColors.Default}Matches: {ChatColors.Green}{stats.MatchesPlayed}");
                        player.PrintToChat($" {ChatColors.Default}Winrate: {ChatColors.Green}{stats.WinRate:F2}%");
                        player.PrintToChat($" {ChatColors.Default}K/D: {ChatColors.Green}{stats.AverageKd:F2}");
                        player.PrintToChat($" {ChatColors.Default}K/R: {ChatColors.Green}{stats.AverageKills:F2}");
                        player.PrintToChat($" {ChatColors.Default}HS%: {ChatColors.Green}{stats.HeadshotPercent:F2}%");
                        player.PrintToChat($" {ChatColors.Default}AvgADR: {ChatColors.Green}{stats.AvgADR:F2}");
                    }
                    else
                    {
                        player.PrintToChat($" {ChatColors.Red}Error fetching stats! Check server console.");
                    }
                });
            }
            catch (Exception ex)
            {
                _plugin.DebugLog($"Stats Error: {ex}", DebugLevel.Light);
                Server.NextFrame(() => player.PrintToChat($"{ChatColors.Red}Error fetching stats!"));
            }
        }

        private async Task<PlayerStats?> FetchPlayerStats(string steamId)
        {
            if (string.IsNullOrEmpty(_plugin.Config.FaceitApiKey))
            {
                _plugin.DebugLog("Cannot fetch stats - no Faceit API key configured", DebugLevel.Light);
                return null;
            }

            try
            {
                var gameId = "cs2";
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var ninetyDaysAgo = DateTimeOffset.UtcNow.AddDays(-90).ToUnixTimeMilliseconds();

                var playerIdUrl = $"https://open.faceit.com/data/v4/players?game_player_id={steamId}&game={gameId}";
                _plugin.DebugLog($"Fetching Faceit Player ID from: {playerIdUrl}", DebugLevel.Light);

                var playerIdResponse = await _httpClient.GetAsync(playerIdUrl);
                playerIdResponse.EnsureSuccessStatusCode();

                var playerIdContent = await playerIdResponse.Content.ReadAsStringAsync();
                dynamic? playerData = JsonConvert.DeserializeObject(playerIdContent);
                string? playerId = playerData?.player_id;

                if (string.IsNullOrEmpty(playerId))
                {
                    _plugin.DebugLog("Could not find Faceit Player ID", DebugLevel.Light);
                    return null;
                }

                var statsUrl = $"https://open.faceit.com/data/v4/players/{playerId}/games/{gameId}/stats?from={ninetyDaysAgo}&to={now}&limit=100";
                _plugin.DebugLog($"Fetching stats from: {statsUrl}", DebugLevel.Light);

                var statsResponse = await _httpClient.GetAsync(statsUrl);
                statsResponse.EnsureSuccessStatusCode();

                var statsContent = await statsResponse.Content.ReadAsStringAsync();
                var statsParsed = JsonConvert.DeserializeObject<PlayerStatsResponse>(statsContent);

                if (statsParsed?.PlayerStats == null)
                {
                    _plugin.DebugLog("Could not load PlayerStats", DebugLevel.Light);
                    return null;
                }

                var aggregatedStats = AggregateStats(statsParsed.PlayerStats);
                return aggregatedStats;
            }
            catch (HttpRequestException ex)
            {
                _plugin.DebugLog($"HTTP Error fetching stats: {ex.Message}", DebugLevel.Light);
                return null;
            }
            catch (Exception ex)
            {
                _plugin.DebugLog($"Unexpected error: {ex}", DebugLevel.Light);
                return null;
            }
        }

        // Aggregiert die Spielerstatistiken und berechnet Durchschnittswerte
        private PlayerStats AggregateStats(PlayerStats stats)
        {
            var aggregatedStats = new PlayerStats
            {
                TotalKills = stats.TotalKills,
                TotalDeaths = stats.TotalDeaths,
                TotalRounds = stats.TotalRounds,
                MatchesPlayed = stats.MatchesPlayed,
                TotalWins = stats.TotalWins,
                TotalHeadshots = stats.TotalHeadshots,
                TotalADR = stats.TotalADR
            };

            // Berechnungen
            aggregatedStats.AverageKd = CalculateKDRatio(aggregatedStats);
            aggregatedStats.AverageKills = CalculateKRRatio(aggregatedStats);
            aggregatedStats.WinRate = CalculateWinRate(aggregatedStats);
            aggregatedStats.HeadshotPercent = (aggregatedStats.TotalHeadshots / (double)aggregatedStats.TotalKills) * 100;
            aggregatedStats.AvgADR = aggregatedStats.TotalADR / (double)aggregatedStats.MatchesPlayed;

            return aggregatedStats;
        }

        // Berechnet das K/D Ratio
        private double CalculateKDRatio(PlayerStats stats)
        {
            if (stats.TotalDeaths == 0)
            {
                return stats.TotalKills; // Wenn keine Tode, dann Kills als K/D-Ratio verwenden
            }
            return (double)stats.TotalKills / stats.TotalDeaths;
        }

        // Berechnet das Kills pro Runde Ratio
        private double CalculateKRRatio(PlayerStats stats)
        {
            if (stats.TotalRounds == 0)
            {
                return 0; // Keine gespielten Runden, also keine Kills pro Runde
            }
            return (double)stats.TotalKills / stats.TotalRounds;
        }

        // Berechnet die Winrate
        private double CalculateWinRate(PlayerStats stats)
        {
            if (stats.MatchesPlayed == 0)
            {
                return 0; // Keine Spiele, also keine Winrate
            }
            return (double)stats.TotalWins / stats.MatchesPlayed * 100;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // Die PlayerStats Klasse ist eine Aggregation aller relevanten Statistiken
    public class PlayerStats
    {
        public int MatchesPlayed { get; set; }
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }
        public int TotalRounds { get; set; }
        public int TotalWins { get; set; }
        public double TotalHeadshots { get; set; }
        public double TotalADR { get; set; }

        public double AverageKd { get; set; }
        public double AverageKills { get; set; }
        public double WinRate { get; set; }
        public double HeadshotPercent { get; set; }
        public double AvgADR { get; set; }
    }

    // Die PlayerStatsResponse Klasse ist die Antwort der API
    public class PlayerStatsResponse
    {
        public PlayerStats PlayerStats { get; set; } = new PlayerStats();
    }
}