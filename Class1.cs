using System;
using System.Collections.Generic;
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
        public override string ModuleName => "Wombastiches Elo Plugin";
        public override string ModuleVersion => "1.0.0";
        public override string ModuleAuthor => "wombat.";  
        public override string ModuleDescription => "!faceit command displays Faceit Elo for all players on the server";   
        private const string FaceitApiKey = "7f772ef4-c85e-4c1d-8249-cbeaa96e5a4b";
        
        private const bool DebugMode = true;
        
        private void DebugLog(string message)
        {
            if (DebugMode)
            {
                Console.WriteLine($"[WombastischesEloPlugin] {DateTime.Now:HH:mm:ss.fff} {message}");
            }
        }

        public override void Load(bool hotReload)
        {
            AddCommand("css_faceit", "Show Faceit ELO", Command_Faceit);
            DebugLog("Plugin loaded successfully");
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

        private async void HandleFaceitCommand(CCSPlayerController player)
{
    try
    {
        DebugLog("Starting Faceit command processing");
        
        List<CCSPlayerController> allPlayers = Utilities.GetPlayers();
        var team1Players = allPlayers.Where(p => p.Team == CsTeam.Terrorist && p.IsValid).ToList();
        var team2Players = allPlayers.Where(p => p.Team == CsTeam.CounterTerrorist && p.IsValid).ToList();

        DebugLog($"Found {team1Players.Count} T players and {team2Players.Count} CT players");

        await Server.NextFrameAsync(() => 
        {
            if (player.IsValid)
            {
                // Red header with symbols
                player.PrintToChat($" {ChatColors.Red}══════ FACEIT ELO RATINGS ══════{ChatColors.Default}");
            }
        });

        await DisplayTeamElo(team1Players, "TERRORISTS", player);
        await DisplayTeamElo(team2Players, "COUNTER-TERRORISTS", player);
    }
    catch (Exception ex)
    {
        DebugLog($"Error in HandleFaceitCommand: {ex.Message}");
    }
}

private async Task DisplayTeamElo(List<CCSPlayerController> players, string teamName, CCSPlayerController target)
{
    if (!target.IsValid) return;
    
    DebugLog($"Displaying Elo for {teamName}");
    
    await Server.NextFrameAsync(() =>
    {
        if (target.IsValid)
        {
            // Orange team header with symbols
            target.PrintToChat($" {ChatColors.Orange}=== {teamName.ToUpper()} ==={ChatColors.Default}");
        }
    });

    foreach (var player in players)
    {
        if (!player.IsValid || player.IsBot) continue;

        DebugLog($"Processing player: {player.PlayerName}");
        var elo = await GetPlayerElo(player);
        var eloText = elo == -1 ? "N/A" : elo.ToString();
        var eloColor = GetEloColor(elo);

        await Server.NextFrameAsync(() =>
        {
            if (target.IsValid && player.IsValid)
            {
                // Player line with explicit color reset
                target.PrintToChat($"  {ChatColors.Green}{player.PlayerName}" +
                                $"{ChatColors.Default} - " +
                                $"{eloColor}{eloText}{ChatColors.Default}");
            }
        });
    }
}
        private async Task<int> GetPlayerElo(CCSPlayerController player)
        {
            DebugLog($"GetPlayerElo called for {player.PlayerName}");

            string steamId = string.Empty;

            await Server.NextFrameAsync(() =>
            {
                if (player.IsValid && !player.IsBot)
                    steamId = player.SteamID.ToString();
            });

            if (string.IsNullOrEmpty(steamId))
            {
                DebugLog("SteamID retrieval failed - player might be invalid or bot");
                return -1;
            }

            DebugLog($"Using SteamID: {steamId}");

            try
            {
                return await Task.Run(async () =>
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {FaceitApiKey}");
                    
                    var url = $"https://open.faceit.com/data/v4/players?game=cs2&game_player_id={steamId}";
                    DebugLog($"Making API request to: {url}");

                    var response = await client.GetStringAsync(url);
                    DebugLog($"Received API response: {response.Truncate(200)}");

                    dynamic? data = JsonConvert.DeserializeObject(response);
                    int elo = (int?)data?.games?.cs2?.faceit_elo ?? -1;
                    
                    DebugLog($"Parsed Elo value: {elo}");
                    return elo;
                });
            }
            catch (Exception ex)
            {
                DebugLog($"API Error: {ex.Message}");
                return -1;
            }
        }
    }

    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }
    }
}