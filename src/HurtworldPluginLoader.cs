using System;
using uMod.Plugins;

namespace uMod.Hurtworld
{
    /// <summary>
    /// Responsible for loading core Hurtworld plugins
    /// </summary>
    public class HurtworldPluginLoader : PluginLoader
    {
        public override Type[] CorePlugins => new[] { typeof(Hurtworld) };
    }
}
