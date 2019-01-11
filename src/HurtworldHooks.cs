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

#if ITEMV2

        /// <summary>
        /// Called when the player is attempting to craft
        /// </summary>
        /// <param name="player"></param>
        /// <param name="recipe"></param>
        /// <returns></returns>
        [HookMethod("ICanCraft")]
        private object ICanCraft(uLink.NetworkPlayer player, ICraftable recipe)
        {
            PlayerSession session = Hurtworld.Find(player);
            return Interface.CallHook("CanCraft", session, recipe);
        }

#endif

        /// <summary>
        /// Called when the player is attempting to connect
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerApprove")]
        private object IOnPlayerApprove(PlayerSession session)
        {
            session.Identity.Name = session.Identity.Name ?? "Unnamed";
#if !ITEMV2
            session.Name = session.Identity.Name;
#endif
            string id = session.SteamId.ToString();
            string ip = session.Player.ipAddress;

            Universal.PlayerManager.PlayerJoin(session);

            object loginSpecific = Interface.CallHook("CanClientLogin", session);
            object loginUniversal = Interface.CallHook("CanUserLogin", session.Identity.Name, id, ip);
            object canLogin = loginSpecific ?? loginUniversal;

            if (canLogin is string || canLogin is bool && !(bool)canLogin)
            {
                GameManager.Instance.StartCoroutine(GameManager.Instance.DisconnectPlayerSync(session.Player, canLogin is string ? canLogin.ToString() : "Connection was rejected")); // TODO: Localization
                Interface.uMod.LogWarning($"_playerSessions contains {session.Player}? " + GameManager.Instance._playerSessions.ContainsKey(session.Player).ToString());
                if (GameManager.Instance._playerSessions.ContainsKey(session.Player))
                {
                    GameManager.Instance._playerSessions.Remove(session.Player);
                }
                Interface.uMod.LogWarning($"_steamIdSession contains {session.SteamId}? " + GameManager.Instance._steamIdSession.ContainsKey(session.SteamId).ToString());
                if (GameManager.Instance._steamIdSession.ContainsKey(session.SteamId))
                {
                    GameManager.Instance._steamIdSession.Remove(session.SteamId);
                }
                return true;
            }

            GameManager.Instance._playerSessions[session.Player] = session;

            object approvedSpecific = Interface.CallHook("OnPlayerApprove", session);
            object approvedUniversal = Interface.CallHook("OnPlayerApproved", session.Identity.Name, id, ip);
            return approvedSpecific ?? approvedUniversal;
        }

#if ITEMV2

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
            return chatSpecific ?? chatUniversal;
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
            object blockedSpecific = Interface.CallHook("OnPlayerCommand", session, cmd, args); // TODO: Deprecate OnChatCommand
            object blockedUniversal = Interface.CallHook("OnPlayerCommand", session.IPlayer, cmd, args);
            if (blockedSpecific != null || blockedUniversal != null) return true;

            // Is it a covalance command?
            if (Universal.CommandSystem.HandleChatMessage(session.IPlayer, command)) return true;

            // Is it a regular chat command?
            //if (!cmdlib.HandleChatCommand(session, cmd, args))
            //    session.IPlayer.Reply(string.Format(lang.GetMessage("UnknownCommand", this, session.IPlayer.Id), cmd));

            return true;
        }

#else

        /// <summary>
        /// Called when the player sends a message
        /// </summary>
        /// <param name="session"></param>
        /// <param name="message"></param>
        [HookMethod("IOnPlayerChat")]
        private object IOnPlayerChat(PlayerSession session, string message)
        {
            if (message.Trim().Length <= 1)
            {
                return true;
            }

            string str = message.Substring(0, 1);

            // Is it a chat command?
            if (!str.Equals("/"))
            {
                object chatSpecific = Interface.CallHook("OnPlayerChat", session, message);
                object chatUniversal = Interface.CallHook("OnPlayerChat", session.IPlayer, message);
                return chatSpecific ?? chatUniversal;
            }

            // Is this a covalence command?
            if (Universal.CommandSystem.HandleChatMessage(session.IPlayer, message))
            {
                return true;
            }

            // Get the command string
            string command = message.Substring(1);

            // Parse it
            string cmd;
            string[] args;
            ParseCommand(command, out cmd, out args);
            if (cmd == null)
            {
                return null;
            }

            // Handle it
            /*if (!cmdlib.HandleChatCommand(session, cmd, args))
            {
                session.IPlayer.Reply(string.Format(lang.GetMessage("UnknownCommand", this, session.SteamId.ToString()), cmd));
                return true;
            }*/

            // Call the game hook
            Interface.CallHook("OnChatCommand", session, command);

            return true;
        }

#endif

        /// <summary>
        /// Called when the player has connected
        /// </summary>
#if ITEMV2

        /// <param name="session"></param>
        [HookMethod("OnPlayerConnected")]
        private void OnPlayerConnected(PlayerSession session)
        {
#else

        /// <param name="name"></param>
        [HookMethod("IOnPlayerConnected")]
        private void IOnPlayerConnected(string name)
        {
            PlayerSession session = Find(name);
#endif
            if (session == null)
            {
                return;
            }

            // Update player's permissions group and name
            if (permission.IsLoaded)
            {
                string id = session.SteamId.ToString();
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

#if !ITEMV2
            // Call game-specific hook
            Interface.CallHook("OnPlayerConnected", session);
#endif

            // Let covalence know
            Universal.PlayerManager.PlayerConnected(session);
            IPlayer iplayer = Universal.PlayerManager.FindPlayerById(session.SteamId.ToString());
            if (iplayer != null)
            {
                session.IPlayer = iplayer;
                Interface.CallHook("OnPlayerConnected", session.IPlayer);
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

                // Let covalence know
                Interface.CallHook("OnPlayerDisconnected", session.IPlayer, "Unknown");
                Universal.PlayerManager.PlayerDisconnected(session);
            }
        }

        /// <summary>
        /// Called when the server receives input from the player
        /// </summary>
        /// <param name="player"></param>
        /// <param name="input"></param>
        [HookMethod("IOnPlayerInput")]
        private void IOnPlayerInput(NetworkPlayer player, InputControls input)
        {
            PlayerSession session = Find(player);
            if (session != null)
            {
                Interface.CallHook("OnPlayerInput", session, input);
            }
        }

        /// <summary>
        /// Called when the player attempts to suicide
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("IOnPlayerSuicide")]
        private object IOnPlayerSuicide(NetworkPlayer player)
        {
            PlayerSession session = Find(player);
            return session != null ? Interface.CallHook("OnPlayerSuicide", session) : null;
        }

        /// <summary>
        /// Called when the player attempts to suicide
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("IOnPlayerVoice")]
        private object IOnPlayerVoice(NetworkPlayer player)
        {
            PlayerSession session = Find(player);
            return session != null ? Interface.CallHook("OnPlayerVoice", session) : null;
        }

        #endregion Player Hooks

        #region Entity Hooks

        /// <summary>
        /// Called when an entity takes damage
        /// </summary>
        /// <param name="effect"></param>
        /// <param name="target"></param>
        /// <param name="source"></param>
        [HookMethod("IOnTakeDamage")]
        private void IOnTakeDamage(EntityEffectFluid effect, EntityStats target, EntityEffectSourceData source)
        {
            if (effect == null || target == null || source == null || source.Value.Equals(0f))
            {
                return;
            }

            AIEntity entity = target.GetComponent<AIEntity>();
            if (entity != null)
            {
                Interface.CallHook("OnEntityTakeDamage", entity, source);
                return;
            }

#if ITEMV2
            HNetworkView networkView = target.networkView;
#else
            uLink.NetworkView networkView = target.uLinkNetworkView();
#endif
            if (networkView != null)
            {
                PlayerSession session = GameManager.Instance.GetSession(networkView.owner);
                if (session != null)
                {
                    Interface.CallHook("OnPlayerTakeDamage", session, source);
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
            NetworkPlayer? player = door.LastUsedBy;
            if (player == null)
            {
                return;
            }

            PlayerSession session = Find((NetworkPlayer)player);
            if (session != null)
            {
                Interface.CallHook("OnGarageDoorUsed", door, session);
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
        /// <param name="player"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("IOnEnterVehicle")]
        private void IOnEnterVehicle(NetworkPlayer player, VehiclePassenger vehicle)
        {
            PlayerSession session = Find(player);
            Interface.CallHook("OnEnterVehicle", session, vehicle);
        }

        /// <summary>
        /// Called when a player exits a vehicle
        /// </summary>
        /// <param name="player"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        [HookMethod("IOnExitVehicle")]
        private void IOnExitVehicle(NetworkPlayer player, VehiclePassenger vehicle)
        {
            PlayerSession session = Find(player);
            Interface.CallHook("OnExitVehicle", session, vehicle);
        }

        #endregion Vehicle Hooks
    }
}
