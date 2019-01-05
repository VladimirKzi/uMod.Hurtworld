using System;
using System.Globalization;
using System.Linq;
using System.Net;
using uMod.Libraries.Universal;
using uMod.Logging;

namespace uMod.Hurtworld
{
    /// <summary>
    /// Represents the server hosting the game instance
    /// </summary>
    public class HurtworldServer : IServer
    {
        #region Initialization

        internal static readonly BanManager BanManager = BanManager.Instance;
        internal static readonly ChatManagerServer ChatManager = ChatManagerServer.Instance;
        internal static readonly ConsoleManager ConsoleManager = ConsoleManager.Instance;

        #endregion Initialization

        #region Information

        /// <summary>
        /// Gets/sets the public-facing name of the server
        /// </summary>
        public string Name
        {
            get => GameManager.Instance.ServerConfig.GameName;
            set => GameManager.Instance.ServerConfig.GameName = value;
        }

        private static IPAddress address;
        private static IPAddress localAddress;

        /// <summary>
        /// Gets the public-facing IP address of the server, if known
        /// </summary>
        public IPAddress Address
        {
            get
            {
                try
                {
                    if (address == null)
                    {
                        uint ip;
                        if (Utility.ValidateIPv4(GameManager.Instance.ServerConfig.BoundIP) && !Utility.IsLocalIP(GameManager.Instance.ServerConfig.BoundIP))
                        {
                            IPAddress.TryParse(GameManager.Instance.ServerConfig.BoundIP, out address);
                            Interface.uMod.LogInfo($"IP address from command-line: {address}");
                        }
                        else if ((ip = Steamworks.SteamGameServer.GetPublicIP()) > 0)
                        {
                            string publicIp = string.Concat(ip >> 24 & 255, ".", ip >> 16 & 255, ".", ip >> 8 & 255, ".", ip & 255); // TODO: uint IP address utility method
                            IPAddress.TryParse(publicIp, out address);
                            Interface.uMod.LogInfo($"IP address from Steam query: {address}");
                        }
                        else
                        {
                            WebClient webClient = new WebClient();
                            IPAddress.TryParse(webClient.DownloadString("http://api.ipify.org"), out address);
                            Interface.uMod.LogInfo($"IP address from external API: {address}");
                        }
                    }

                    return address;
                }
                catch (Exception ex)
                {
                    RemoteLogger.Exception("Couldn't get server's public IP address", ex);
                    return IPAddress.Any;
                }
            }
        }

        /// <summary>
        /// Gets the local IP address of the server, if known
        /// </summary>
        public IPAddress LocalAddress
        {
            get
            {
                try
                {
                    return localAddress ?? (localAddress = Utility.GetLocalIP());
                }
                catch (Exception ex)
                {
                    RemoteLogger.Exception("Couldn't get server's local IP address", ex);
                    return IPAddress.Any;
                }
            }
        }

        /// <summary>
        /// Gets the public-facing network port of the server, if known
        /// </summary>
        public ushort Port => (ushort)GameManager.Instance.ServerConfig.Port;

        /// <summary>
        /// Gets the version or build number of the server
        /// </summary>
        public string Version => GameManager.Instance.Version.ToString();

        /// <summary>
        /// Gets the network protocol version of the server
        /// </summary>
        public string Protocol => GameManager.PROTOCOL_VERSION.ToString();

        /// <summary>
        /// Gets the language set by the server
        /// </summary>
        public CultureInfo Language => CultureInfo.InstalledUICulture;

        /// <summary>
        /// Gets the total of players currently on the server
        /// </summary>
        public int Players => GameManager.Instance.GetPlayerCount();

        /// <summary>
        /// Gets/sets the maximum players allowed on the server
        /// </summary>
        public int MaxPlayers
        {
            get => GameManager.Instance.ServerConfig.MaxPlayers;
            set => GameManager.Instance.ServerConfig.MaxPlayers = value;
        }

        /// <summary>
        /// Gets/sets the current in-game time on the server
        /// </summary>
        public DateTime Time
        {
            get
            {
                GameTime time = TimeManager.Instance.GetCurrentGameTime();
                return Convert.ToDateTime($"{time.Hour}:{time.Minute}:{Math.Floor(time.Second)}");
            }
            set
            {
                double currentOffset = TimeManager.Instance.GetCurrentGameTime().offset;
                int daysPassed = TimeManager.Instance.GetCurrentGameTime().Day + 1;
                double newOffset = 86400 * daysPassed - currentOffset + value.TimeOfDay.TotalSeconds;
                TimeManager.Instance.InitialTimeOffset += (float)newOffset;
            }
        }

        /// <summary>
        /// Gets information on the currently loaded save file
        /// </summary>
        public SaveInfo SaveInfo => null;

        #endregion Information

        #region Administration

        /// <summary>
        /// Bans the player for the specified reason and duration
        /// </summary>
        /// <param name="id"></param>
        /// <param name="reason"></param>
        /// <param name="duration"></param>
        public void Ban(string id, string reason, TimeSpan duration = default(TimeSpan))
        {
            if (!IsBanned(id))
            {
                BanManager.AddBan(Convert.ToUInt64(id));

                // TODO: Implement universal ban storage
            }
        }

        /// <summary>
        /// Gets the amount of time remaining on the player's ban
        /// </summary>
        /// <param name="id"></param>
        public TimeSpan BanTimeRemaining(string id) => TimeSpan.MaxValue;

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        /// <param name="id"></param>
        public bool IsBanned(string id) => BanManager.IsBanned(Convert.ToUInt64(id));

        /// <summary>
        /// Saves the server and any related information
        /// </summary>
        public void Save() => Command("saveserver");

        /// <summary>
        /// Unbans the player
        /// </summary>
        /// <param name="id"></param>
        public void Unban(string id)
        {
            if (IsBanned(id))
            {
                BanManager.RemoveBan(Convert.ToUInt64(id));

                // TODO: Implement universal ban storage
            }
        }

        #endregion Administration

        #region Chat and Commands

        /// <summary>
        /// Broadcasts the specified chat message and prefix to all players
        /// </summary>
        /// <param name="message"></param>
        /// <param name="prefix"></param>
        /// <param name="args"></param>
        public void Broadcast(string message, string prefix, params object[] args)
        {
            if (!string.IsNullOrEmpty(message))
            {
                ulong avatarId = args.Length > 0 && args[0].IsSteamId() ? (ulong)args[0] : 0ul;
                message = args.Length > 0 ? string.Format(Formatter.ToUnity(message), avatarId != 0ul ? args.Skip(1) : args) : Formatter.ToUnity(message);
                string formatted = prefix != null ? $"{prefix}: {message}" : message;
#if ITEMV2
                ChatManager.SendChatMessage(new ServerChatMessage(formatted));
#else
                ChatManager.RPC("RelayChat", uLink.RPCMode.Others, formatted);
#endif
                ConsoleManager.SendLog($"[Chat] {message}");
            }
        }

        /// <summary>
        /// Broadcasts the specified chat message to all players
        /// </summary>
        /// <param name="message"></param>
        public void Broadcast(string message) => Broadcast(message, null);

        /// <summary>
        /// Runs the specified server command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        public void Command(string command, params object[] args)
        {
            if (!string.IsNullOrEmpty(command))
            {
                ConsoleManager.ExecuteCommand($"{command} {string.Join(" ", Array.ConvertAll(args, x => x.ToString()))}");
            }
        }

        #endregion Chat and Commands
    }
}
