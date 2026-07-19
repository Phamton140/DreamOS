using System.Collections.Generic;
using System.Linq;
using DreamOS.Core.Interfaces;

namespace DreamOS.Infrastructure.Services
{
    public class PluginService
    {
        private readonly IEnumerable<IProjectPlugin> _plugins;

        public PluginService(IEnumerable<IProjectPlugin> plugins)
        {
            _plugins = plugins;
        }

        public IProjectPlugin? GetPluginForProject(string projectPath)
        {
            return _plugins.FirstOrDefault(plugin => plugin.CanHandle(projectPath));
        }

        public IEnumerable<string> GetAvailablePlugins()
        {
            return _plugins.Select(p => p.Name);
        }
    }
}
