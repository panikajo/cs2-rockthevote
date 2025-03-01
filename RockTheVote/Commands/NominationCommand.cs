﻿
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;

namespace cs2_rockthevote
{
    public partial class Plugin
    {
        [ConsoleCommand("nominate", "nominate a map to rtv")]
        public void OnNominate(CCSPlayerController? player, CommandInfo command)
        {
            string map = command.GetArg(1).Trim().ToLower();
            _nominationManager.CommandHandler(player!, map);
        }

        [GameEventHandler(HookMode.Pre)]
        public HookResult EventPlayerDisconnectNominate(EventPlayerDisconnect @event, GameEventInfo @eventInfo)
        {
            var player = @event.Userid;
            _nominationManager.PlayerDisconnected(player);
            return HookResult.Continue;
        }
    }

    public class NominationCommand : IPluginDependency<Plugin, Config>
    {
        Dictionary<int, List<string>> Nominations = new();
        ChatMenu? nominationMenu = null;
        private RtvConfig _config = new();
        private GameRules _gamerules;
        private StringLocalizer _localizer;
        private PluginState _pluginState;
        private MapLister _mapLister;

        public NominationCommand(MapLister mapLister, GameRules gamerules, StringLocalizer localizer, PluginState pluginState)
        {
            _mapLister = mapLister;
            _mapLister.EventMapsLoaded += OnMapsLoaded;
            _gamerules = gamerules;
            _localizer = localizer;
            _pluginState = pluginState;
        }


        public void OnMapStart(string map)
        {
            Nominations.Clear();
        }

        public void OnConfigParsed(Config config)
        {
            _config = config.Rtv;
        }


        public void OnMapsLoaded(object? sender, string[] maps)
        {
            nominationMenu = new("Nomination");
            foreach (var map in _mapLister.Maps!.Where(x => x != Server.MapName))
            {
                nominationMenu.AddMenuOption(map, (CCSPlayerController player, ChatMenuOption option) =>
                {
                    Nominate(player, option.Text);
                });
            }
        }

        public void CommandHandler(CCSPlayerController player, string map)
        {
            map = map.ToLower().Trim();
            if (_pluginState.DisableCommands || !_config.Enabled)
            {
                player.PrintToChat(_localizer.LocalizeWithPrefix("general.validation.disabled"));
                return;
            }

            if (!_config.EnabledInWarmup && _gamerules.WarmupRunning)
            {
                player.PrintToChat(_localizer.LocalizeWithPrefix("general.validation.warmup"));
                return;
            }

            if (ServerManager.ValidPlayerCount() < _config!.MinPlayers)
            {
                player.PrintToChat(_localizer.LocalizeWithPrefix("general.validation.minimum-players", _config!.MinPlayers));
                return;
            }

            if (_config.MinRounds > 0 && _config.MinRounds > _gamerules.TotalRoundsPlayed)
            {
                player!.PrintToChat(_localizer.LocalizeWithPrefix("general.validation.minimum-rounds", _config.MinRounds));
                return;
            }

            if (string.IsNullOrEmpty(map))
            {
                OpenNominationMenu(player!);
            }
            else
            {
                Nominate(player, map);
            }
        }

        public void OpenNominationMenu(CCSPlayerController player)
        {
            ChatMenus.OpenMenu(player!, nominationMenu!);
        }

        void Nominate(CCSPlayerController player, string map)
        {
            if (map == Server.MapName)
            {
                player!.PrintToChat(_localizer.LocalizeWithPrefix("general.validation.current-map"));
                return;
            }

            if (_mapLister.Maps!.FirstOrDefault(x => x.ToLower() == map) is null)
            {
                player!.PrintToChat(_localizer.LocalizeWithPrefix("general.invalid-map"));
                return;

            }

            var userId = player.UserId!.Value;
            if (!Nominations.ContainsKey(userId))
                Nominations[userId] = new();

            bool alreadyVoted = Nominations[userId].IndexOf(map) != -1;
            if (!alreadyVoted)
                Nominations[userId].Add(map);

            var totalVotes = Nominations.Select(x => x.Value.Where(y => y == map).Count())
                .Sum();

            if (!alreadyVoted)
                Server.PrintToChatAll(_localizer.LocalizeWithPrefix("nominate.nominated", player.PlayerName, map, totalVotes));
            else
                player.PrintToChat(_localizer.LocalizeWithPrefix("nominate.already-nominated", map, totalVotes));
        }

        public List<string> NominationWinners()
        {
            if (Nominations.Count == 0)
                return new List<string>();

            var rawNominations = Nominations
                .Select(x => x.Value)
                .Aggregate((acc, x) => acc.Concat(x).ToList());

            return rawNominations
                .Distinct()
                .Select(map => new KeyValuePair<string, int>(map, rawNominations.Count(x => x == map)))
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .ToList();
        }

        public void PlayerDisconnected(CCSPlayerController player)
        {
            int userId = player.UserId!.Value;
            if (!Nominations.ContainsKey(userId))
                Nominations.Remove(userId);
        }
    }
}
