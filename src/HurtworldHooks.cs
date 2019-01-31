using System;
using uMod.Configuration;
using uMod.Libraries.Universal;
using uMod.Plugins;
using UnityEngine;
using NetworkPlayer = uLink.NetworkPlayer;

namespace uMod.Hurtworld
{
    /// <summary>
    /// Game hooks and wrappers for the core Hurtworld plugin
    /// </summary>
    public partial class Hurtworld
    {
        #region Player Hooks

        /// <summary>
        /// Called when the player is attempting to craft
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="recipe"></param>
        /// <returns></returns>
        [HookMethod("ICanCraft")]
        private object ICanCraft(NetworkPlayer netPlayer, ICraftable recipe)
        {
            PlayerSession session = Find(netPlayer);
            return Interface.CallHook("CanCraft", session, recipe);
        }

        /// <summary>
        /// Called when the player is attempting to connect
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerApprove")]
        private object IOnPlayerApprove(PlayerSession session)
        {
            session.Identity.Name = session.Identity.Name ?? "Unnamed";
            string id = session.SteamId.ToString();
            string ip = session.Player.ipAddress;

            Universal.PlayerManager.PlayerJoin(session);

            object loginSpecific = Interface.CallHook("CanClientLogin", session);
            object loginUniversal = Interface.CallHook("CanPlayerLogin", session.Identity.Name, id, ip);
            object loginDeprecated = Interface.CallDeprecatedHook("CanUserLogin", "CanPlayerLogin", new DateTime(2018, 07, 01), session.Identity.Name, id, ip);
            object canLogin = loginSpecific ?? loginUniversal ?? loginDeprecated;

            if (canLogin is string || canLogin is bool && !(bool)canLogin)
            {
                GameManager.Instance.StartCoroutine(GameManager.Instance.DisconnectPlayerSync(session.Player, canLogin is string ? canLogin.ToString() : "Connection was rejected")); // TODO: Localization
                if (GameManager.Instance._playerSessions.ContainsKey(session.Player))
                {
                    GameManager.Instance._playerSessions.Remove(session.Player);
                }
                if (GameManager.Instance._steamIdSession.ContainsKey(session.SteamId))
                {
                    GameManager.Instance._steamIdSession.Remove(session.SteamId);
                }
                return true;
            }

            GameManager.Instance._playerSessions[session.Player] = session;

            object approvedSpecific = Interface.CallHook("OnPlayerApprove", session);
            object approvedUniversal = Interface.CallHook("OnPlayerApproved", session.Identity.Name, id, ip);
            object approvedDeprecated = Interface.CallDeprecatedHook("OnUserApproved", "OnPlayerApproved", new DateTime(2018, 07, 01), session.Identity.Name, id, ip);
            return approvedSpecific ?? approvedUniversal ?? approvedDeprecated;
        }

        /// <summary>
        /// Called when the player sends a chat message
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerChat")]
        private object IOnPlayerChat(PlayerSession session, string message)
        {
            object chatSpecific = Interface.CallHook("OnPlayerChat", session, message);
            object chatUniversal = Interface.CallHook("OnPlayerChat", session.IPlayer, message);
            object chatDeprecated = Interface.CallDeprecatedHook("OnUserChat", "OnPlayerChat", new DateTime(2018, 07, 01), session.IPlayer, message);
            return chatSpecific ?? chatUniversal ?? chatDeprecated;
        }

        /// <summary>
        /// Called when the player atempts to claim territory
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="clan"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerClaimTerritory")]
        private object IOnPlayerClaimTerritory(NetworkPlayer netPlayer, Clan clan, int point)
        {
            return Interface.CallHook("OnPlayerClaimTerritory", Find(netPlayer), clan, point);
        }

        /// <summary>
        /// Called when the player has claimed territory
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="clan"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerClaimedTerritory")]
        private void IOnPlayerClaimedTerritory(NetworkPlayer netPlayer, Clan clan, int point)
        {
            Interface.CallHook("OnPlayerClaimedTerritory", Find(netPlayer), clan, point);
        }

        /// <summary>
        /// Called when the player sends a chat command
        /// </summary>
        /// <param name="session"></param>
        /// <param name="command"></param>
        [HookMethod("IOnPlayerCommand")]
        private object IOnPlayerCommand(PlayerSession session, string command)
        {
            // Get the full command
            string str = command.TrimStart('/');

            // Parse it
            string cmd;
            string[] args;
            ParseCommand(str, out cmd, out args);
            if (cmd == null) return null;

            // Is the command blocked?
            object blockedSpecific = Interface.CallHook("OnPlayerCommand", session, cmd, args);
            object blockedUniversal = Interface.CallHook("OnPlayerCommand", session.IPlayer, cmd, args);
            object blockDeprecated = Interface.CallDeprecatedHook("OnUserCommand", "OnPlayerCommand", new DateTime(2018, 07, 01), session.IPlayer, cmd, args);
            if (blockedSpecific != null || blockedUniversal != null || blockDeprecated != null) return true;

            // Is it a universal command?
            if (Universal.CommandSystem.HandleChatMessage(session.IPlayer, command))
            {
                return true;
            }

            // Is it a regular chat command?
            //if (!cmdlib.HandleChatCommand(session, cmd, args))
            //{
            //    session.IPlayer.Reply(string.Format(lang.GetMessage("UnknownCommand", this, session.IPlayer.Id), cmd));
            //}

            return true;
        }

        /// <summary>
        /// Called when the player has connected
        /// </summary>
        /// <param name="session"></param>
        [HookMethod("OnPlayerConnected")]
        private void OnPlayerConnected(PlayerSession session)
        {
            if (session != null)
            {
                string id = session.SteamId.ToString();

                // Update player's permissions group and name
                if (permission.IsLoaded)
                {
                    permission.UpdateNickname(id, session.Identity.Name);
                    uModConfig.DefaultGroups defaultGroups = Interface.uMod.Config.Options.DefaultGroups;
                    if (!permission.UserHasGroup(id, defaultGroups.Players))
                    {
                        permission.AddUserGroup(id, defaultGroups.Players);
                    }

                    if (session.IsAdmin && !permission.UserHasGroup(id, defaultGroups.Administrators))
                    {
                        permission.AddUserGroup(id, defaultGroups.Administrators);
                    }
                }

                // Set default language for player if not set
                if (string.IsNullOrEmpty(lang.GetLanguage(id)))
                {
                    lang.SetLanguage(session.WorldPlayerEntity.PlayerOptions.CurrentConfig.CurrentLanguage, id);
                }

                // Let universal know
                Universal.PlayerManager.PlayerConnected(session);
                IPlayer player = Universal.PlayerManager.FindPlayerById(session.SteamId.ToString());
                if (player != null)
                {
                    session.IPlayer = player;
                    Interface.CallHook("OnPlayerConnected", session.IPlayer);
                    Interface.CallDeprecatedHook("OnUserConnected", "OnPlayerConnected", new DateTime(2018, 07, 01), session.IPlayer);
                }
            }
        }

        /// <summary>
        /// Called when the player has disconnected
        /// </summary>
        /// <param name="session"></param>
        [HookMethod("IOnPlayerDisconnected")]
        private void IOnPlayerDisconnected(PlayerSession session)
        {
            if (session.IsLoaded)
            {
                // Call game-specific hook
                Interface.CallHook("OnPlayerDisconnected", session);

                // Let universal know
                Interface.CallHook("OnPlayerDisconnected", session.IPlayer, "Unknown");
                Interface.CallDeprecatedHook("OnUserDisconnected", "OnPlayerDisconnected", new DateTime(2018, 07, 01), session.IPlayer, "Unknown");
                Universal.PlayerManager.PlayerDisconnected(session);
            }
        }

        /// <summary>
        /// Called when the server receives input from the player
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="input"></param>
        [HookMethod("IOnPlayerInput")]
        private void IOnPlayerInput(NetworkPlayer netPlayer, InputControls input)
        {
            PlayerSession session = Find(netPlayer);
            if (session != null)
            {
                Interface.CallHook("OnPlayerInput", session, input);
            }
        }

        /// <summary>
        /// Called when the player attempts to suicide
        /// </summary>
        /// <param name="netPlayer"></param>
        [HookMethod("IOnPlayerSuicide")]
        private object IOnPlayerSuicide(NetworkPlayer netPlayer)
        {
            PlayerSession session = Find(netPlayer);
            return session != null ? Interface.CallHook("OnPlayerSuicide", session) : null;
        }

        /// <summary>
        /// Called when the player attempts to suicide
        /// </summary>
        /// <param name="netPlayer"></param>
        [HookMethod("IOnPlayerVoice")]
        private object IOnPlayerVoice(NetworkPlayer netPlayer)
        {
            PlayerSession session = Find(netPlayer);
            return session != null ? Interface.CallHook("OnPlayerVoice", session) : null;
        }

        #endregion Player Hooks

        #region Entity Hooks

        /// <summary>
        /// Called when an entity effect is applied
        /// </summary>
        /// <param name="target"></param>
        /// <param name="effect"></param>
        /// <param name="source"></param>
        [HookMethod("IOnEntityEffect")]
        private void IOnEntityEffect(EntityStats target, IEntityFluidEffect effect, EntityEffectSourceData source)
        {
            if (source == null || effect.ResolveTargetType() != EntityFluidEffectKeyDatabase.Instance?.Health)
            {
                return;
            }

            AIEntity entity = target.GetComponent<AIEntity>();
            if (entity != null)
            {
                if (source.Value >= 0)
                {
                    Interface.CallHook("OnEntityTakeDamage", entity, source);
                }
                else
                {
                    Interface.CallHook("OnEntityHeal", entity, source);
                }
                return;
            }

            HNetworkView networkView = target.networkView;
            if (networkView != null)
            {
                PlayerSession session = GameManager.Instance.GetSession(networkView.owner);
                if (session != null)
                {
                    if (source.Value >= 0)
                    {
                        Interface.CallHook("OnPlayerTakeDamage", session, source);
                    }
                    else
                    {
                        Interface.CallHook("OnPlayerHeal", session, source);
                    }
                }
            }
        }

        #endregion Entity Hooks

        #region Structure Hooks

        /// <summary>
        /// Called when a single door is used
        /// </summary>
        /// <param name="door"></param>
        /// <returns></returns>
        [HookMethod("IOnSingleDoorUsed")]
        private void IOnSingleDoorUsed(DoorSingleServer door)
        {
            NetworkPlayer? player = door.LastUsedBy;
            if (player == null)
            {
                return;
            }

            PlayerSession session = Find((NetworkPlayer)player);
            if (session != null)
            {
                Interface.CallHook("OnSingleDoorUsed", door, session);
            }
        }

        /// <summary>
        /// Called when a double door is used
        /// </summary>
        /// <param name="door"></param>
        /// <returns></returns>
        [HookMethod("IOnDoubleDoorUsed")]
        private void IOnDoubleDoorUsed(DoubleDoorServer door)
        {
            NetworkPlayer? player = door.LastUsedBy;
            if (player == null)
            {
                return;
            }

            PlayerSession session = Find((NetworkPlayer)player);
            if (session != null)
            {
                Interface.CallHook("OnDoubleDoorUsed", door, session);
            }
        }

        /// <summary>
        /// Called when a garage door is used
        /// </summary>
        /// <param name="door"></param>
        /// <returns></returns>
        [HookMethod("IOnGarageDoorUsed")]
        private void IOnGarageDoorUsed(GarageDoorServer door)
        {
            NetworkPlayer? netPlayer = door.LastUsedBy;
            if (netPlayer != null)
            {
                PlayerSession session = Find((NetworkPlayer)netPlayer);
                if (session != null)
                {
                    Interface.CallHook("OnGarageDoorUsed", door, session);
                }
            }
        }

        #endregion Structure Hooks

        #region Vehicle Hooks

        /// <summary>
        /// Called when a player tries to enter a vehicle
        /// </summary>
        /// <param name="session"></param>
        /// <param name="go"></param>
        /// <returns></returns>
        [HookMethod("ICanEnterVehicle")]
        private object ICanEnterVehicle(PlayerSession session, GameObject go)
        {
            return Interface.CallHook("CanEnterVehicle", session, go.GetComponent<VehiclePassenger>());
        }

        /// <summary>
        /// Called when a player tries to exit a vehicle
        /// </summary>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("ICanExitVehicle")]
        private object ICanExitVehicle(VehiclePassenger vehicle)
        {
            PlayerSession session = Find(vehicle.networkView.owner);
            return session != null ? Interface.CallHook("CanExitVehicle", session, vehicle) : null;
        }

        /// <summary>
        /// Called when a player enters a vehicle
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("IOnEnterVehicle")]
        private void IOnEnterVehicle(NetworkPlayer netPlayer, VehiclePassenger vehicle)
        {
            PlayerSession session = Find(netPlayer);
            Interface.CallHook("OnEnterVehicle", session, vehicle);
        }

        /// <summary>
        /// Called when a player exits a vehicle
        /// </summary>
        /// <param name="netPlayer"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("IOnExitVehicle")]
        private void IOnExitVehicle(NetworkPlayer netPlayer, VehiclePassenger vehicle)
        {
            PlayerSession session = Find(netPlayer);
            Interface.CallHook("OnExitVehicle", session, vehicle);
        }

        #endregion Vehicle Hooks
    }
}
