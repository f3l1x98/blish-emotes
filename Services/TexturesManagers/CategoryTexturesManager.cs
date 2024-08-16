using Blish_HUD;
using Blish_HUD.Modules.Managers;

namespace felix.BlishEmotes.Services.TexturesManagers
{
    public class CategoryTexturesManager : TexturesManager
    {
        private static readonly Logger Logger = Logger.GetLogger<CategoryTexturesManager>();

        private ContentsManager ContentsManager;

        public CategoryTexturesManager(ContentsManager contentsManager, DirectoriesManager directoriesManager) : base(directoriesManager)
        {
            this.ContentsManager = contentsManager;
        }

        public override void LoadTextures()
        {
            if (_textureCache.Count != 0)
            {
                Logger.Info("Skipping LoadTextures due to already loaded.");
                return;
            }
            // Load category default texture
            _textureCache.Add(Textures.CategorySettingsIcon.ToString(), ContentsManager.GetTexture(@"textures\156909.png"));
            LoadTexturesFromDirectory(TextureExtensionMasks);
        }
    }
}
