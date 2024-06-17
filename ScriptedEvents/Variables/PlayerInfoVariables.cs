﻿namespace ScriptedEvents.Variables.Strings
{
#pragma warning disable SA1402 // File may only contain a single type
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Exiled.API.Features;
    using Exiled.API.Features.Roles;
    using Exiled.CustomItems.API.Features;

    using ScriptedEvents.API.Enums;
    using ScriptedEvents.API.Extensions;
    using ScriptedEvents.API.Features;
    using ScriptedEvents.API.Features.Exceptions;
    using ScriptedEvents.API.Interfaces;
    using ScriptedEvents.Structures;
    using ScriptedEvents.Variables.Interfaces;

    public class PlayerInfoVariables : IVariableGroup
    {
        /// <inheritdoc/>
        public string GroupName => "Player info";

        /// <inheritdoc/>
        public IVariable[] Variables { get; } = new IVariable[]
        {
            new Command(),
            new Show(),
            new GetPlayersByData(),
        };
    }

    public class GetPlayersByData : IFloatVariable, IPlayerVariable, IArgumentVariable
    {
        /// <inheritdoc/>
        public string Name => "{GETPLAYERSBYDATA}";

        /// <inheritdoc/>
        public string Description => "Gets all players from a variable if their playerdata matches the required value.";

        /// <inheritdoc/>
        public Argument[] ExpectedArguments => new[]
        {
            new Argument("players", typeof(PlayerCollection), "The players variable from which the players will be fetched from.", true),
            new Argument("key", typeof(string), "The key from which the value will be fetched and compared with the required value.", true),
            new Argument("requiredValue", typeof(string), "The value that the player key must have in order to get included.", true),
        };

        /// <inheritdoc/>
        public IEnumerable<Player> Players
        {
            get
            {
                foreach (Player plr in ((PlayerCollection)Arguments[0]).GetInnerList())
                {
                    string key = (string)Arguments[1];

                    if (!plr.SessionVariables.TryGetValue(key, out object value))
                        continue;

                    if (value is not string valueString)
                        continue;

                    if (valueString != (string)Arguments[2])
                        continue;

                    yield return plr;
                }
            }
        }

        /// <inheritdoc/>
        public float Value => Players.Count();

        /// <inheritdoc/>
        public string[] RawArguments { get; set; }

        /// <inheritdoc/>
        public object[] Arguments { get; set; }
    }

    public class Command : IStringVariable, IArgumentVariable, INeedSourceVariable
    {
        /// <inheritdoc/>
        public string Name => "{CMDVAR}";

        /// <inheritdoc/>
        public string Description => "Converts a player variable into a format to use with commands.";

        /// <inheritdoc/>
        public string[] RawArguments { get; set; }

        /// <inheritdoc/>
        public object[] Arguments { get; set; }

        /// <inheritdoc/>
        public Argument[] ExpectedArguments { get; } = new[]
        {
            new Argument("name", typeof(PlayerCollection), "The name of the player variable.", true),
        };

        /// <inheritdoc/>
        public Script Source { get; set; }

        /// <inheritdoc/>
        public string Value
        {
            get
            {
                PlayerCollection variable = (PlayerCollection)Arguments[0];

                if (variable is IArgumentVariable)
                {
                    throw new ArgumentException(ErrorGen.Get(ErrorCode.UnsupportedArgumentVariables, Name));
                }

                if (variable.Length == 0)
                    return string.Empty;

                return string.Join(".", variable.GetInnerList().Select(plr => plr.Id.ToString()));
            }
        }
    }

    public class Show : IStringVariable, IArgumentVariable, INeedSourceVariable, ILongDescription
    {
        /// <inheritdoc/>
        public string Name => "{GET}";

        /// <inheritdoc/>
        public string Description => "Gets certain properties about the players in a player variable.";

        /// <inheritdoc/>
        public string[] RawArguments { get; set; }

        /// <inheritdoc/>
        public object[] Arguments { get; set; }

        /// <inheritdoc/>
        public Argument[] ExpectedArguments => new[]
        {
            new Argument("name", typeof(PlayerCollection), "The name of the player variable to show.", true),
            new OptionsArgument("selector", false,
                new("NAME", "Player's name"),
                new("DPNAME", "Player's display name"),
                new("UID", "Player's user id"),
                new("PID", "Player's player id"),
                new("ROLE", "Player's role"),
                new("TEAM", "Player's team"),
                new("ROOM", "Room where player is"),
                new("ZONE", "Zone where player is"),
                new("HP", "Player's health"),
                new("ITEMCOUNT", "Amount of items in player's inventory"),
                new("ITEMS", "Items in player's inventory"),
                new("HELDITEM", "Item which player is holding"),
                new("ISGOD", "Player's god status"),
                new("POSX", "Player's X position"),
                new("POSY", "Player's Y position"),
                new("POSZ", "Player's Z position"),
                new("TIER", "Player's tier as SCP079 (-1 if player is not SCP079)"),
                new("GROUP", "Player's remote admin rank"),
                new("ISCUFFED", "Is player cuffed"),
                new("CUSTOMI", "Player's custom info"),
                new("XSIZE", "Player's size on X axis"),
                new("YSIZE", "Player's size on Y axis"),
                new("ZSIZE", "Player's size on Z axis"),
                new("KILLS", "Player's kills"),
                new("EFFECTS", "Player's current effects"),
                new("USINGNOCLIP", "Is player using noclip"),
                new("CANNOCLIP", "Is player permitted to use noclip"),
                new("STAMINA", "Is player permitted to use noclip")),
        };

        /// <inheritdoc/>
        public Script Source { get; set; } = null;

        /// <inheritdoc/>
        public string Value
        {
            get
            {
                string selector = "NAME";

                if (Arguments.Length > 1)
                    selector = Arguments[1].ToUpper();

                PlayerCollection players = (PlayerCollection)Arguments[0];

                if (players.Length > 1)
                {
                    throw new ScriptedEventsException(ErrorGen.Generate(ErrorCode.ParameterError_TooManyPlayers, Name));
                }
                else if (players.Length == 0)
                {
                    throw new ScriptedEventsException(ErrorGen.Generate(ErrorCode.InvalidPlayerVariable, Name));
                }

                Player ply = players.FirstOrDefault();

                return selector switch
                {
                    "NAME" => ply.Nickname,
                    "DISPLAYNAME" or "DPNAME" => ply.DisplayNickname,
                    "USERID" or "UID" => ply.UserId,
                    "PLAYERID" or "PID" => ply.Id.ToString(),
                    "ROLE" => ply.Role.Type.ToString(),
                    "TEAM" => ply.Role.Team.ToString(),
                    "ROOM" => ply.CurrentRoom.Type.ToString(),
                    "ZONE" => ply.Zone.ToString(),
                    "HP" or "HEALTH" => ply.Health.ToString(),
                    "INVCOUNT" => ply.Items.Count.ToString(),
                    "INV" => string.Join("|", ply.Items.Select(item => CustomItem.TryGet(item, out CustomItem ci) ? ci.Name : item.Type.ToString())),
                    "HELDITEM" => (CustomItem.TryGet(ply.CurrentItem, out CustomItem ci) ? ci.Name : ply.CurrentItem?.Type.ToString()) ?? ItemType.None.ToString(),
                    "GOD" => ply.IsGodModeEnabled.ToUpper(),
                    "POS" => $"{ply.Position.x} {ply.Position.y} {ply.Position.z}",
                    "POSX" => ply.Position.x.ToString(),
                    "POSY" => ply.Position.y.ToString(),
                    "POSZ" => ply.Position.z.ToString(),
                    "TIER" when ply.Role is Scp079Role scp079role => scp079role.Level.ToString(),
                    "TIER" => "-1",
                    "GROUP" => ply.GroupName,
                    "CUFFED" => ply.IsCuffed.ToUpper(),
                    "CUSTOMINFO" or "CINFO" or "CUSTOMI" => ply.CustomInfo.ToString(),
                    "XSIZE" => ply.Scale.x.ToString(),
                    "YSIZE" => ply.Scale.y.ToString(),
                    "ZSIZE" => ply.Scale.z.ToString(),
                    "KILLS" => MainPlugin.Handlers.PlayerKills.TryGetValue(ply, out int v) ? v.ToString() : "0",
                    "EFFECTS" when ply.ActiveEffects.Count() != 0 => string.Join(", ", ply.ActiveEffects.Select(eff => eff.name)),
                    "EFFECTS" => "NONE",
                    "USINGNOCLIP" => ply.Role is FpcRole role ? role.IsNoclipEnabled.ToUpper() : "FALSE",
                    "CANNOCLIP" => ply.IsNoclipPermitted.ToUpper(),
                    "STAMINA" => ply.Stamina.ToString(),
                    "ISSTAFF" => ply.RemoteAdminAccess.ToUpper(),
                    _ => GetByPlayerData(ply, selector),
                };
            }
        }

        /// <inheritdoc/>
        public string LongDescription => @"This variable is designed to only be used with a player variable containing one player.";

        private string GetByPlayerData(Player player, string selector)
        {
            if (player.SessionVariables.ContainsKey(selector))
                return player.SessionVariables[selector].ToString();

            return "NONE";
        }
    }
}
