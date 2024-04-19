﻿namespace ScriptedEvents.Actions
{
    using System;

    using Exiled.API.Features.Items;
    using ScriptedEvents.API.Enums;
    using ScriptedEvents.API.Interfaces;
    using ScriptedEvents.Structures;
    using UnityEngine;

    public class SpawnGrenadeAction : IScriptAction, IHelpInfo
    {
        /// <inheritdoc/>
        public string Name => "SPAWNGRENADE";

        /// <inheritdoc/>
        public string[] Aliases => Array.Empty<string>();

        /// <inheritdoc/>
        public string[] RawArguments { get; set; }

        /// <inheritdoc/>
        public object[] Arguments { get; set; }

        /// <inheritdoc/>
        public ActionSubgroup Subgroup => ActionSubgroup.Item;

        /// <inheritdoc/>
        public string Description => "Spawns a fused grenade in the provided XYZ coordinates.";

        /// <inheritdoc/>
        public Argument[] ExpectedArguments => new[]
        {
            new Argument("x", typeof(float), "The X coordinate.", true),
            new Argument("y", typeof(float), "The Y coordinate.", true),
            new Argument("z", typeof(float), "The Z coordinate.", true),
            new Argument("fuseTimeSeconds", typeof(float), "Fuse time for the grenade.", true),
        };

        /// <inheritdoc/>
        public ActionResponse Execute(Script script)
        {
            ExplosiveGrenade grenade = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE);
            Vector3 pos = new(
                (float)Arguments[0],
                (float)Arguments[1],
                (float)Arguments[2]);

            grenade.FuseTime = (float)Arguments[3];
            grenade.SpawnActive(pos);

            return new(true);
        }
    }
}