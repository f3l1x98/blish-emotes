using Blish_HUD;
using Blish_HUD.Modules.Managers;

namespace felix.BlishEmotes.Services.TexturesManagers
{
    public class EmoteTexturesManager : TexturesManager
    {
        private static readonly Logger Logger = Logger.GetLogger<EmoteTexturesManager>();

        public EmoteTexturesManager(DirectoriesManager directoriesManager) : base(directoriesManager)
        {
        }

        public override void LoadTextures()
        {
            if (_textureCache.Count != 0)
            {
                Logger.Info("Skipping LoadTextures due to already loaded.");
                return;
            }
            LoadTexturesFromDirectory(new[] { "*.png" }, "emotes");
        }
    }
}
