using MediaBrowser.Model.Plugins;

namespace TMDbCollections.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public int MinimumNumberOfMovies { get; set; }
    }
}
