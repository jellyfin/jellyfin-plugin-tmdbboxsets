using System;
using System.Collections.Generic;
using Jellyfin.Plugin.TMDbBoxSets.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TMDbBoxSets
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer)
            : base(appPaths, xmlSerializer)
        {
            Instance = this;
        }

        public override string Name => "TMDb Box Sets";

        public static Plugin Instance { get; private set; }

        public override string Description
            => "Automatically create movie box sets based on TMDb collections";

        public PluginConfiguration PluginConfiguration => Configuration;

        private readonly Guid _id = new Guid("BC4AAD2E-D3D0-4725-A5E2-FD07949E5B42");
        public override Guid Id => _id;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "TMDb Box Sets",
                    EmbeddedResourcePath = GetType().Namespace + ".configurationpage.html"
                }
            };
        }
    }
}