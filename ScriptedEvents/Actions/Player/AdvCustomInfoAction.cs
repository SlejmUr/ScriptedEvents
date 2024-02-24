﻿namespace ScriptedEvents.Actions
{
    using System;

    using Exiled.API.Extensions;
    using Exiled.API.Features;

    using ScriptedEvents.API.Enums;
    using ScriptedEvents.API.Extensions;
    using ScriptedEvents.API.Interfaces;
    using ScriptedEvents.Structures;
    using ScriptedEvents.Variables;

    public class AdvCustomInfoAction : IScriptAction, IHelpInfo
    {
        /// <inheritdoc/>
        public string Name => "ADVCUSTOMINFO";

        /// <inheritdoc/>
        public string[] Aliases => Array.Empty<string>();

        /// <inheritdoc/>
        public ActionSubgroup Subgroup => ActionSubgroup.Player;

        /// <inheritdoc/>
        public string[] RawArguments { get; set; }

        /// <inheritdoc/>
        public object[] Arguments { get; set; }

        /// <inheritdoc/>
        public string Description => "Sets/clears the custom info of the targeted player(s).";

        /// <inheritdoc/>
        public Argument[] ExpectedArguments { get; } = new[]
        {
            new Argument("mode", typeof(string), "The mode (SET, CLEAR).", true),
            new Argument("players", typeof(Player[]), "The players to affect.", true),
            new Argument("targets", typeof(Player[]), "The players that will see the action taking place. Only used if mode is SET.", false),
            new Argument("text", typeof(string), "The text to set custom info to. Only used if mode is SET.", false),
        };

        /// <inheritdoc/>
        public ActionResponse Execute(Script script)
        {
            PlayerCollection plys = (PlayerCollection)Arguments[1];

            switch (Arguments[0].ToUpper())
            {
                case "SET":
                    if (Arguments.Length < 3) return new(MessageType.InvalidUsage, this, null, (object)ExpectedArguments);

                    string text = VariableSystem.ReplaceVariables(Arguments.JoinMessage(3), script)
                        .Replace("\\n", "\n")
                        .Replace("<br>", "\n");

                    PlayerCollection targetPlys = (PlayerCollection)Arguments[2];
                    foreach (Player ply in plys)
                    {
                        foreach (Player targetPlayer in targetPlys)
                        {
                            targetPlayer.SetPlayerInfoForTargetOnly(ply, text);
                        }
                    }

                    return new(true);

                case "CLEAR":
                    foreach (Player ply in plys)
                    {
                        ply.CustomInfo = null;
                    }

                    return new(true);
            }

            return new(MessageType.InvalidOption, this, "mode", "SET/CLEAR");
        }
    }
}