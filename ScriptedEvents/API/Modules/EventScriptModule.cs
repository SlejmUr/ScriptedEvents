﻿using Exiled.API.Enums;
using PlayerRoles;

namespace ScriptedEvents.API.Modules
{
#nullable enable
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Exiled.API.Features;
    using Exiled.API.Features.Doors;
    using Exiled.API.Features.Items;
    using Exiled.API.Features.Roles;
    using Exiled.Events.EventArgs.Interfaces;
    using Exiled.Events.Features;
    using Exiled.Loader;
    using MapGeneration.Distributors;
    using ScriptedEvents.API.Enums;
    using ScriptedEvents.API.Extensions;
    using ScriptedEvents.API.Features;
    using ScriptedEvents.API.Features.Exceptions;
    using ScriptedEvents.Structures;

    public class EventScriptModule : SEModule
    {
        /// <summary>
        /// Gets an array of Event "Handler" types defined by Exiled.
        /// </summary>
        public static Type[] HandlerTypes { get; private set; } = [];

        public static EventScriptModule? Singleton { get; private set; }

        public override string Name => "EventScriptModule";

        public List<Tuple<PropertyInfo, Delegate>> StoredDelegates { get; } = new();

        public Dictionary<string, List<string>> CurrentEventData { get; set; } = [];

        public Dictionary<string, List<string>> CurrentCustomEventData { get; set; } = [];

        public List<string> DynamicallyConnectedEvents { get; set; } = new();

        // Connection methods
        public static void OnArgumentedEvent<T>(T ev)
            where T : IExiledEvent
        {
            Type evType = typeof(T);
            string evName = evType.Name.Replace("EventArgs", string.Empty);
            Singleton!.OnAnyEvent(evName, ev);
        }

        public static void OnNonArgumentedEvent()
        {
            Singleton!.OnAnyEvent(new StackFrame(2).GetMethod().Name.Replace("EventArgs", string.Empty));
        }

        public override void Init()
        {
            base.Init();
            Singleton = this;

            try
            {
                HandlerTypes = Loader.Plugins.First(plug => plug.Name == "Exiled.Events")
                    .Assembly.GetTypes()
                    .Where(t => t.FullName?.Equals($"Exiled.Events.Handlers.{t.Name}") is true).ToArray();
            }
            catch
            {
                Logger.Error($"Fetching HandlerTypes failed! Exiled.Events does not exist in loaded plugins:\n{string.Join(", ", Loader.Plugins.Select(x => x.Name))}");
            }

            // Events
            Exiled.Events.Handlers.Server.RestartingRound += TerminateConnections;
            Exiled.Events.Handlers.Server.WaitingForPlayers += BeginConnections;
        }

        public override void Kill()
        {
            base.Kill();
            TerminateConnections();

            // Disconnect events
            Exiled.Events.Handlers.Server.RestartingRound -= TerminateConnections;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= BeginConnections;

            Singleton = null;
        }

        // Methods to make and destroy connections
        public void BeginConnections()
        {
            if (CurrentEventData is not null)
                return;

            CurrentEventData = new();
            CurrentCustomEventData = new();

            foreach (Script scr in MainPlugin.ScriptModule.ListScripts())
            {
                if (scr.HasFlag("EVENT", out Flag f))
                {
                    string evName = f.Arguments[0];

                    if (CurrentEventData.ContainsKey(evName))
                    {
                        CurrentEventData[evName].Add(scr.Name);
                    }
                    else
                    {
                        CurrentEventData.Add(evName, new List<string>() { scr.Name });
                    }
                }

                if (scr.HasFlag("CUSTOMEVENT", out Flag cf))
                {
                    string cEvName = cf.Arguments[0];
                    if (CurrentCustomEventData.ContainsKey(cEvName))
                    {
                        CurrentCustomEventData[cEvName].Add(scr.Name);
                    }
                    else
                    {
                        CurrentCustomEventData.Add(cEvName, new List<string>() { scr.Name });
                    }
                }

                scr.Dispose();
            }

            foreach (KeyValuePair<string, List<string>> ev in CurrentEventData)
            {
                Logger.Debug("Setting up new 'on' event");
                Logger.Debug($"Event: {ev.Key}");
                Logger.Debug($"Scripts: {string.Join(", ", ev.Value)}");

                ConnectDynamicExiledEvent(ev.Key);
            }
        }

        public void ConnectDynamicExiledEvent(string key)
        {
            if (DynamicallyConnectedEvents.Contains(key)) return;

            DynamicallyConnectedEvents.Add(key);

            bool made = false;
            foreach (Type handler in HandlerTypes)
            {
                // Credit to DevTools & Yamato for below code.
                Delegate @delegate;
                PropertyInfo propertyInfo = handler.GetProperty(key);

                if (propertyInfo is null)
                    continue;

                EventInfo eventInfo = propertyInfo.PropertyType.GetEvent("InnerEvent", (BindingFlags)(-1));
                MethodInfo subscribe = propertyInfo.PropertyType.GetMethods().First(x => x.Name is "Subscribe");

                if (propertyInfo.PropertyType == typeof(Event))
                {
                    @delegate = new CustomEventHandler(OnNonArgumentedEvent);
                }
                else if (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Event<>))
                {
                    @delegate = typeof(EventScriptModule)
                        .GetMethod(nameof(OnArgumentedEvent))
                        .MakeGenericMethod(eventInfo.EventHandlerType.GenericTypeArguments)
                        .CreateDelegate(typeof(CustomEventHandler<>)
                        .MakeGenericType(eventInfo.EventHandlerType.GenericTypeArguments));
                }
                else
                {
                    Logger.Warn(propertyInfo.Name);
                    continue;
                }

                subscribe.Invoke(propertyInfo.GetValue(MainPlugin.Handlers), [@delegate]);
                StoredDelegates.Add(new Tuple<PropertyInfo, Delegate>(propertyInfo, @delegate));

                made = true;
            }

            if (made)
                Logger.Debug($"Dynamic event {key} connected successfully");
            else
                Logger.Debug($"Dynamic event {key} failed to be connected");
        }

        public void TerminateConnections()
        {
            foreach (Tuple<PropertyInfo, Delegate> tuple in StoredDelegates)
            {
                PropertyInfo propertyInfo = tuple.Item1;
                Delegate handler = tuple.Item2;

                Logger.Debug($"Removing dynamic connection for event '{propertyInfo.Name}'");

                EventInfo eventInfo = propertyInfo.PropertyType.GetEvent("InnerEvent", (BindingFlags)(-1));
                MethodInfo unSubscribe = propertyInfo.PropertyType.GetMethods().First(x => x.Name is "Unsubscribe");

                unSubscribe.Invoke(propertyInfo.GetValue(MainPlugin.Handlers), [handler]);
                Logger.Debug($"Removed dynamic connection for event '{propertyInfo.Name}'");
            }

            StoredDelegates.Clear();
            CurrentEventData = [];
            CurrentCustomEventData = [];
            DynamicallyConnectedEvents = [];
        }

        // Code to run when connected event is executed
        public void OnAnyEvent(string eventName, IExiledEvent? ev = null)
        {
            if (ev == null) return;
            
            Stopwatch stopwatch = new();

            stopwatch.Start();

            if (ev is IDeniableEvent deniable and IPlayerEvent playerEvent)
            {
                bool playerIsNotNone = playerEvent.Player is not null;

                bool isRegisteredRule = MainPlugin.Handlers.GetPlayerDisableEvent(eventName, playerEvent.Player).HasValue;

                if (playerIsNotNone && isRegisteredRule)
                {
                    Log.Debug("Event is disabled.");
                    deniable.IsAllowed = false;
                    return;
                }
            }

            if (CurrentEventData is null || !CurrentEventData.TryGetValue(eventName, out var scriptNames))
            {
                return;
            }

            Log.Debug("Scripts connected to this event:");
            List<Script> scripts = new();

            foreach (string scrName in scriptNames)
            {
                try
                {
                    scripts.Add(MainPlugin.ScriptModule.ReadScript(scrName, null));
                    Log.Debug($"- {scrName}.txt");
                }
                catch (DisabledScriptException)
                {
                    Logger.Warn(ErrorGen.Get(ErrorCode.On_DisabledScript, eventName, scrName));
                }
                catch (FileNotFoundException)
                {
                    Logger.Warn(ErrorGen.Get(ErrorCode.On_NotFoundScript, eventName, scrName));
                }
                catch (Exception ex)
                {
                    Logger.Warn(ErrorGen.Get(ErrorCode.On_UnknownError, eventName) + $": {ex}");
                }
            }

            var properties = (
                from prop in ev.GetType().GetProperties() 
                let value = prop.GetValue(ev) 
                where value is not null 
                select new Tuple<object, string>(value, prop.Name)).ToList();

            IPlayerEvent? reference = ev as IPlayerEvent;
            switch (eventName)
            {
                case "Left":
                case "ChangingRole":
                    LastPlayerState state = new(reference!.Player);
                    properties.Add(new(state, "ShouldNotHappen"));
                    break;
            }
            
            foreach (var (propValue, propName) in properties)
            {
                switch (propValue)
                {
                    case Player player:
                        if (player is Npc) continue;

                        foreach (var script in scripts)
                            script.AddPlayerVariable($"{{EV{propName.ToUpper()}}}", string.Empty, new[] { player });

                        Log.Debug($"Adding variable {{EV{propName.ToUpper()}}} to all scripts above.");
                        break;

                    case Item item:
                        AddVariable(item.Base.ItemSerial.ToString());
                        break;

                    case Door door:
                        AddVariable(door.Type.ToString());
                        break;

                    case Scp079Generator gen:
                        AddVariable(gen.GetInstanceID().ToString());
                        break;
                    
                    case Role role:
                        AddVariable(role.Type.ToString());
                        break;
                    
                    case Enum anyEnum:
                        AddVariable(anyEnum.ToString());
                        break;

                    case bool @bool:
                        AddVariable(@bool.ToUpper());
                        break;
                    
                    case LastPlayerState lastPlayerState:
                        foreach (FieldInfo field in typeof(LastPlayerState).GetFields(BindingFlags.Public | BindingFlags.Instance))
                        {
                            string fieldName = field.Name;
                            object value = field.GetValue(lastPlayerState);
                        
                            foreach (Script script in scripts)
                            {
                                script.AddVariable($"{{EV{fieldName.ToUpper()}}}", string.Empty, value.ToString());
                                Log.Debug($"Adding variable {{EV{fieldName.ToUpper()}}} to all scripts above.");
                            }
                        }
                        break;

                    default:
                        AddVariable(propValue.ToString());
                        break;
                }

                continue;

                void AddVariable(string varValue)
                {
                    foreach (Script script in scripts)
                    {
                        script.AddVariable($"{{EV{propName.ToUpper()}}}", string.Empty, varValue);
                        Log.Debug($"Adding variable {{EV{propName.ToUpper()}}} to all scripts above.");
                    }
                }
            }

            foreach (Script script in scripts)
                script.Execute();

            stopwatch.Stop();
            Log.Debug($"Handling event '{eventName}' cost {stopwatch.ElapsedMilliseconds} ms");
        }

        private class LastPlayerState
        {
            public string LastName;
            public string LastUserId;
            public RoleTypeId LastRole;
            public Team LastTeam;
            public ZoneType LastZone;
            public RoomType LastRoom;

            public LastPlayerState(Player player)
            {
                LastName = player.Nickname;
                LastUserId = player.UserId;
                LastRole = player.Role.Type;
                LastTeam = player.Role.Team;
                LastZone = player.Zone;
                LastRoom = player.CurrentRoom?.Type ?? RoomType.Unknown;
            }
        }
    }
}
