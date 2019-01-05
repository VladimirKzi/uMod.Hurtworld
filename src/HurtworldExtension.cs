using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using uLink;
using uMod.Extensions;
using uMod.Plugins;
using uMod.Unity;
using UnityEngine;

namespace uMod.Hurtworld
{
    /// <summary>
    /// The extension class that represents this extension
    /// </summary>
    public class HurtworldExtension : Extension
    {
        internal static Assembly Assembly = Assembly.GetExecutingAssembly();
        internal static AssemblyName AssemblyName = Assembly.GetName();
        internal static VersionNumber AssemblyVersion = new VersionNumber(AssemblyName.Version.Major, AssemblyName.Version.Minor, AssemblyName.Version.Build);
        internal static string AssemblyAuthors = ((AssemblyCompanyAttribute)Attribute.GetCustomAttribute(Assembly, typeof(AssemblyCompanyAttribute), false)).Company;

        /// <summary>
        /// Gets whether this extension is for a specific game
        /// </summary>
        public override bool IsGameExtension => true;

        /// <summary>
        /// Gets the name of this extension
        /// </summary>
        public override string Name => "Hurtworld";

        /// <summary>
        /// Gets the author of this extension
        /// </summary>
        public override string Author => AssemblyAuthors;

        /// <summary>
        /// Gets the version of this extension
        /// </summary>
        public override VersionNumber Version => AssemblyVersion;

        /// <summary>
        /// Gets the branch of this extension
        /// </summary>
#if ITEMV2
        public override string Branch => "itemv2"; // TODO: Handle this programmatically
#else
        public override string Branch => "public";
#endif

        // Commands that a plugin can't override
        internal static IEnumerable<string> RestrictedCommands => new[]
        {
            "bindip", "host", "queryport"
        };

        /// <summary>
        /// Default game-specific references for use in plugins
        /// </summary>
        public override string[] DefaultReferences => new[]
        {
            "UnityEngine.UI"
        };

        /// <summary>
        /// List of assemblies allowed for use in plugins
        /// </summary>
        public override string[] WhitelistAssemblies => new[]
        {
            "Assembly-CSharp", "mscorlib", "uMod", "System", "System.Core", "UnityEngine", "uLink"
        };

        public override string[] WhitelistNamespaces => new[]
        {
            "System.Collections", "System.Security.Cryptography", "System.Text", "UnityEngine", "uLink"
        };

        public static string[] Filter =
        {
            "Applying hit on",
            "Authorizing player for region",
            "Automove Source item not found",
            "Begin auth session result k_EBeginAuthSessionResultOK",
            "Building proper config for",
            "Built mappings for entity",
            "Client trying to move item in unknown storage:",
            "Deauthorizing player for region",
            "Degenerate triangles might have been generated.",
            "Failed to find hitbox in mappings",
            "Finished writing containers for save, waiting on save thread",
            "Fire went out due to wind",
            "Got validate auth ticket repsonse k_EAuthSessionResponseOK",
            "Hit claim against invalid view",
            "Image Effects are not supported on this platform.",
            "Loading structure with owner",
            "Object out of bounds, destroying",
            "Player denied permission to",
            "Player entity already exists aborting",
            "Player not using",
            "Player requesting spawn.",
            "PointOnEdgeException, perturbating vertices slightly",
            "Riped",
            "Sending structures to client",
            "Setting view NetworkView",
            "Setting view uLinkNetworkView",
            "Source was empty",
            "Syncing tree deltas",
            "System.TypeInitializationException: An exception was thrown by the type initializer for Mono.CSharp.CSharpCodeCompiler",
            "The image effect DefaultCamera",
            "Usually this is not a problem,",
            "Writing to disk completed from background thread"
        };

        /// <summary>
        /// Initializes a new instance of the HurtworldExtension class
        /// </summary>
        /// <param name="manager"></param>
        public HurtworldExtension(ExtensionManager manager) : base(manager)
        {
        }

        /// <summary>
        /// Loads this extension
        /// </summary>
        public override void Load()
        {
            Manager.RegisterPluginLoader(new HurtworldPluginLoader());
        }

        /// <summary>
        /// Loads plugin watchers used by this extension
        /// </summary>
        /// <param name="directory"></param>
        public override void LoadPluginWatchers(string directory)
        {
        }

        /// <summary>
        /// Called when all other extensions have been loaded
        /// </summary>
        public override void OnModLoad()
        {
            Application.logMessageReceivedThreaded += HandleLog;
            CSharpPluginLoader.PluginReferences.UnionWith(DefaultReferences);
        }

        internal static void ServerConsole()
        {
            if (Interface.Oxide.ServerConsole != null && GameManager.Instance.GameState == EGameState.Hosting)
            {
                Interface.Oxide.ServerConsole.Title = () => $"{GameManager.Instance.GetPlayerCount()} | {GameManager.Instance.ServerConfig.GameName}";

                Interface.Oxide.ServerConsole.Status1Left = () => GameManager.Instance.ServerConfig.GameName;
                Interface.Oxide.ServerConsole.Status1Right = () =>
                {
                    TimeSpan time = TimeSpan.FromSeconds(Time.realtimeSinceStartup);
                    string uptime = $"{time.TotalHours:00}h{time.Minutes:00}m{time.Seconds:00}s".TrimStart(' ', 'd', 'h', 'm', 's', '0');
                    return $"{Mathf.RoundToInt(1f / Time.smoothDeltaTime)}fps, {uptime}";
                };

                Interface.Oxide.ServerConsole.Status2Left = () => $"{GameManager.Instance.GetPlayerCount()}/{GameManager.Instance.ServerConfig.MaxPlayers} players";
                Interface.Oxide.ServerConsole.Status2Right = () =>
                {
                    if (!(NetworkTime.serverTime <= 0))
                    {
                        double bytesReceived = 0;
                        double bytesSent = 0;
                        foreach (uLink.NetworkPlayer connection in uLink.Network.connections)
                        {
                            NetworkStatistics stats = connection.statistics;
                            if (stats != null)
                            {
                                bytesReceived += stats.bytesReceivedPerSecond;
                                bytesSent += stats.bytesSentPerSecond;
                            }
                        }
                        return $"{Utility.FormatBytes(bytesReceived)}/s in, {Utility.FormatBytes(bytesSent)}/s out";
                    }

                    return "not connected";
                };

                Interface.Oxide.ServerConsole.Status3Left = () =>
                {
                    if (TimeManager.Instance != null && GameManager.Instance != null)
                    {
                        GameTime time = TimeManager.Instance.GetCurrentGameTime();
                        string gameTime = Convert.ToDateTime($"{time.Hour}:{time.Minute}:{Math.Floor(time.Second)}").ToString("h:mm tt");
                        return $"{gameTime.ToLower()}, {GameManager.Instance.ServerConfig?.Map ?? "Unknown"}";
                    }

                    return string.Empty;
                };
                Interface.Oxide.ServerConsole.Status3Right = () => $"uMod.Hurtworld {AssemblyVersion}";
                Interface.Oxide.ServerConsole.Status3RightColor = ConsoleColor.Yellow;
            }
        }

        internal static void ServerConsoleOnInput(string input)
        {
            input = input.Trim();
            if (!string.IsNullOrEmpty(input))
            {
                ConsoleManager.Instance.ExecuteCommand(input);
            }
        }

        internal static void HandleLog(string message, string stackTrace, LogType logType)
        {
            if (!string.IsNullOrEmpty(message) && !Filter.Any(message.StartsWith))
            {
                Interface.uMod.RootLogger.HandleMessage(message, stackTrace, logType.ToLogType());
            }
        }
    }
}
