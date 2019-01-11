using Steamworks;
using System;
using System.Globalization;
using uMod.Libraries;
using uMod.Libraries.Universal;
using UnityEngine;

namespace uMod.Hurtworld
{
    /// <summary>
    /// Represents a player, either connected or not
    /// </summary>
    public class HurtworldPlayer : IPlayer, IEquatable<IPlayer>
    {
        #region Initialization

        private static Permission libPerms;

        private readonly PlayerSession session;
        private readonly CSteamID cSteamId;
        private readonly ulong steamId;

        internal HurtworldPlayer(ulong id, string name)
        {
            if (libPerms == null)
            {
                libPerms = Interface.uMod.GetLibrary<Permission>();
            }

            steamId = id;
            Name = name.Sanitize();
            Id = id.ToString();
        }

        internal HurtworldPlayer(PlayerSession session) : this(session.SteamId.m_SteamID, session.Identity.Name)
        {
            this.session = session;
            cSteamId = session.SteamId;
        }

        #endregion Initialization

        #region Objects

        /// <summary>
        /// Gets the object that backs the player
        /// </summary>
        public object Object => session;

        /// <summary>
        /// Gets the player's last command type
        /// </summary>
        public CommandType LastCommand { get; set; }

        #endregion Objects

        #region Information

        /// <summary>
        /// Gets/sets the name for the player
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the ID for the player (unique within the current game)
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the player's language
        /// </summary>
        public CultureInfo Language
        {
            get
            {
#if ITEMV2
                return CultureInfo.GetCultureInfo(session.WorldPlayerEntity.PlayerOptions.CurrentConfig.CurrentLanguage);
#else
                return CultureInfo.GetCultureInfo("en");
#endif
            }
        }

        /// <summary>
        /// Gets the player's IP address
        /// </summary>
        public string Address => session.Player.ipAddress;

        /// <summary>
        /// Gets the player's average network ping
        /// </summary>
        public int Ping => session.Player.averagePing;

        /// <summary>
        /// Returns if the player is a server admin
        /// </summary>
        public bool IsAdmin => GameManager.Instance.IsAdmin(cSteamId);

        /// <summary>
        /// Returns if the player is a server moderator
        /// </summary>
        public bool IsModerator => IsAdmin;

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned => BanManager.Instance.IsBanned(steamId);

        /// <summary>
        /// Gets if the player is connected
        /// </summary>
        public bool IsConnected => session?.Player.isConnected ?? false;

        /// <summary>
        /// Returns if the player is sleeping
        /// </summary>
        public bool IsSleeping => session.Identity.Sleeper != null;

        /// <summary>
        /// Returns if the player is the server
        /// </summary>
        public bool IsServer => false;

        #endregion Information

        #region Administration

        /// <summary>
        /// Bans the player for the specified reason and duration
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="duration"></param>
        public void Ban(string reason, TimeSpan duration = default(TimeSpan))
        {
            if (!IsBanned)
            {
                BanManager.Instance.AddBan(session.SteamId.m_SteamID);
                if (session.Player.isConnected)
                {
                    Kick(reason);
                }
            }
        }

        /// <summary>
        /// Gets the amount of time remaining on the player's ban
        /// </summary>
        public TimeSpan BanTimeRemaining => TimeSpan.MaxValue;

        /// <summary>
        /// Heals the player's character by specified amount
        /// </summary>
        /// <param name="amount"></param>
        public void Heal(float amount)
        {
#if ITEMV2
            EntityEffectFluid effect = new EntityEffectFluid(EntityFluidEffectKeyDatabase.Instance.Health, EEntityEffectFluidModifierType.AddValuePure, amount);
#else
            EntityEffectFluid effect = new EntityEffectFluid(EEntityFluidEffectType.Health, EEntityEffectFluidModifierType.AddValuePure, amount);
#endif
            EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
            effect.Apply(stats);
        }

        /// <summary>
        /// Gets/sets the player's health
        /// </summary>
        public float Health
        {
            get
            {
                EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
#if ITEMV2
                return stats.GetFluidEffect(EntityFluidEffectKeyDatabase.Instance.Health).GetValue();
#else
                return stats.GetFluidEffect(EEntityFluidEffectType.Health).GetValue();
#endif
            }
            set
            {
                EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
#if ITEMV2
                StandardEntityFluidEffect effect = stats.GetFluidEffect(EntityFluidEffectKeyDatabase.Instance.Health) as StandardEntityFluidEffect;
#else
                StandardEntityFluidEffect effect = stats.GetFluidEffect(EEntityFluidEffectType.Health) as StandardEntityFluidEffect;
                effect?.SetValue(value);
#endif
            }
        }

        /// <summary>
        /// Damages the player's character by specified amount
        /// </summary>
        /// <param name="amount"></param>
        public void Hurt(float amount)
        {
#if ITEMV2
            EntityEffectFluid effect = new EntityEffectFluid(EntityFluidEffectKeyDatabase.Instance.Damage, EEntityEffectFluidModifierType.AddValuePure, -amount);
#else
            EntityEffectFluid effect = new EntityEffectFluid(EEntityFluidEffectType.Damage, EEntityEffectFluidModifierType.AddValuePure, -amount);
#endif
            EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
            effect.Apply(stats);
        }

        /// <summary>
        /// Kicks the player from the game
        /// </summary>
        /// <param name="reason"></param>
        public void Kick(string reason)
        {
            GameManager.Instance.KickPlayer(session, reason);
        }

        /// <summary>
        /// Causes the player's character to die
        /// </summary>
        public void Kill()
        {
            EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
            EntityEffectSourceData entityEffectSourceDatum = new EntityEffectSourceData { SourceDescriptionKey = "EntityStats/Sources/Suicide" };
#if ITEMV2
            stats.HandleEvent(new EntityEventDataRaiseEvent { EventType = EEntityEventType.Die }, entityEffectSourceDatum);
#else
            stats.HandleEvent(new EntityEventData { EventType = EEntityEventType.Die }, entityEffectSourceDatum);
#endif
        }

        /// <summary>
        /// Gets/sets the player's maximum health
        /// </summary>
        public float MaxHealth
        {
            get
            {
                EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
#if ITEMV2
                return stats.GetFluidEffect(EntityFluidEffectKeyDatabase.Instance.Health).GetMaxValue();
#else
                return stats.GetFluidEffect(EEntityFluidEffectType.Health).GetMaxValue();
#endif
            }
            set
            {
                EntityStats stats = session.WorldPlayerEntity.GetComponent<EntityStats>();
#if ITEMV2
                StandardEntityFluidEffect effect = stats.GetFluidEffect(EntityFluidEffectKeyDatabase.Instance.Health) as StandardEntityFluidEffect;
                if (effect != null)
                {
                    effect.MaxValue = value;
                }
#else
                StandardEntityFluidEffect effect = stats.GetFluidEffect(EEntityFluidEffectType.Health) as StandardEntityFluidEffect;
                effect?.MaxValue(value);
#endif
            }
        }

        /// <summary>
        /// Renames the player to specified name
        /// <param name="name"></param>
        /// </summary>
        public void Rename(string name)
        {
            //name = name.Substring(0, 32);
            name = ChatManagerServer.CleanupGeneral(name);
            if (string.IsNullOrEmpty(name.Trim()))
            {
                name = "Unnamed";
            }

            // Chat/display name
#if !ITEMV2
            session.Name = name;
#endif
            session.Identity.Name = name;
            session.WorldPlayerEntity.GetComponent<HurtMonoBehavior>().RPC("UpdateName", uLink.RPCMode.All, name);
            SteamGameServer.BUpdateUserData(session.SteamId, name, 0);

            // Overhead name // TODO: Implement when possible
            //var displayProxyName = session.WorldPlayerEntity.GetComponent<DisplayProxyName>();
            //displayProxyName.UpdateName(name);

            session.IPlayer.Name = name;
            libPerms.UpdateNickname(session.Identity.SteamId.ToString(), name);
        }

        /// <summary>
        /// Teleports the player's character to the specified position
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Teleport(float x, float y, float z)
        {
            session.WorldPlayerEntity.transform.position = new Vector3(x, y, z);
        }

        /// <summary>
        /// Teleports the player's character to the specified generic position
        /// </summary>
        /// <param name="pos"></param>
        public void Teleport(GenericPosition pos) => Teleport(pos.X, pos.Y, pos.Z);

        /// <summary>
        /// Unbans the player
        /// </summary>
        public void Unban()
        {
            if (IsBanned)
            {
                BanManager.Instance.RemoveBan(session.SteamId.m_SteamID);
            }
        }

        #endregion Administration

        #region Location

        /// <summary>
        /// Gets the position of the player
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Position(out float x, out float y, out float z)
        {
            Vector3 pos = session.WorldPlayerEntity.transform.position;
            x = pos.x;
            y = pos.y;
            z = pos.z;
        }

        /// <summary>
        /// Gets the position of the player
        /// </summary>
        /// <returns></returns>
        public GenericPosition Position()
        {
            Vector3 pos = session.WorldPlayerEntity.transform.position;
            return new GenericPosition(pos.x, pos.y, pos.z);
        }

        #endregion Location

        #region Chat and Commands

        /// <summary>
        /// Sends the specified message and prefix to the player
        /// </summary>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Message(string message, string prefix, params object[] args)
        {
            if (!string.IsNullOrEmpty(message))
            {
                message = args.Length > 0 ? string.Format(Formatter.ToUnity(message), args) : Formatter.ToUnity(message);
                string formatted = prefix != null ? $"{prefix} {message}" : message;
#if ITEMV2
                ChatManagerServer.Instance.SendChatMessage(new ServerChatMessage(formatted, false), session.Player);
#else
                ChatManagerServer.Instance.RPC("RelayChat", session.Player, formatted);
#endif
            }
        }

        /// <summary>
        /// Sends the specified message to the player
        /// </summary>
        /// <param name="message"></param>
        public void Message(string message) => Message(message, null);

        /// <summary>
        /// Replies to the player with the specified message and prefix
        /// </summary>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Reply(string message, string prefix, params object[] args) => Message(message, prefix, args);

        /// <summary>
        /// Replies to the player with the specified message
        /// </summary>
        /// <param name="message"></param>
        public void Reply(string message) => Message(message, null);

        /// <summary>
        /// Runs the specified console command on the player
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        public void Command(string command, params object[] args)
        {
            // TODO: Implement when possible
        }

        #endregion Chat and Commands

        #region Permissions

        /// <summary>
        /// Gets if the player has the specified permission
        /// </summary>
        /// <param name="perm"></param>
        /// <returns></returns>
        public bool HasPermission(string perm) => libPerms.UserHasPermission(Id, perm);

        /// <summary>
        /// Grants the specified permission on this player
        /// </summary>
        /// <param name="perm"></param>
        public void GrantPermission(string perm) => libPerms.GrantUserPermission(Id, perm, null);

        /// <summary>
        /// Strips the specified permission from this player
        /// </summary>
        /// <param name="perm"></param>
        public void RevokePermission(string perm) => libPerms.RevokeUserPermission(Id, perm);

        /// <summary>
        /// Gets if the player belongs to the specified group
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        public bool BelongsToGroup(string group) => libPerms.UserHasGroup(Id, group);

        /// <summary>
        /// Adds the player to the specified group
        /// </summary>
        /// <param name="group"></param>
        public void AddToGroup(string group) => libPerms.AddUserGroup(Id, group);

        /// <summary>
        /// Removes the player from the specified group
        /// </summary>
        /// <param name="group"></param>
        public void RemoveFromGroup(string group) => libPerms.RemoveUserGroup(Id, group);

        #endregion Permissions

        #region Operator Overloads

        /// <summary>
        /// Returns if player's unique ID is equal to another player's unique ID
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(IPlayer other) => Id == other?.Id;

        /// <summary>
        /// Returns if player's object is equal to another player's object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj) => obj is IPlayer && Id == ((IPlayer)obj).Id;

        /// <summary>
        /// Gets the hash code of the player's unique ID
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// Returns a human readable string representation of this IPlayer
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"HurtworldPlayer[{Id}, {Name}]";

        #endregion Operator Overloads
    }
}
