using Blish_HUD;
using Blish_HUD.Modules.Managers;
using felix.BlishEmotes.Strings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes
{
    class EmotesManager
    {
        private static readonly Logger Logger = Logger.GetLogger<EmotesManager>();
        public bool IsEmoteSynchronized { get; set; } = false;

        public bool IsEmoteTargeted { get; set; } = false;

        private ContentsManager ContentsManager;
        private ModuleSettings Settings;

        // Cache mapping ids to objects
        private Dictionary<string, Emote> emotes;

        public EmotesManager(ContentsManager contentsManager, ModuleSettings settings)
        {
            ContentsManager = contentsManager;
            Settings = settings;
            emotes = new Dictionary<string, Emote>();
        }

        public void Load()
        {
            try
            {
                string fileContents;
                using (StreamReader reader = new StreamReader(ContentsManager.GetFileStream(@"json/emotes.json")))
                {
                    fileContents = reader.ReadToEnd();
                }
                var loadedEmotes = JsonConvert.DeserializeObject<List<Emote>>(fileContents);
                foreach (var emote in loadedEmotes)
                {
                    emote.Texture = ContentsManager.GetTexture(@"textures/emotes/" + emote.TextureRef, ContentsManager.GetTexture(@"textures/missing-texture.png"));
                }
                foreach (var emote in loadedEmotes)
                {
                    emotes.Add(emote.Id, emote);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load emotes.");
                Logger.Error(ex.Message);
                Logger.Debug(ex.StackTrace);
            }
        }

        public void UpdateAll(List<Emote> newEmotes)
        {
            emotes.Clear();
            foreach (var emote in newEmotes)
            {
                emotes.Add(emote.Id, emote);
            }
        }

        public List<Emote> GetAll()
        {
            return new List<Emote>(emotes.Values);
        }

        public List<Emote> GetRadial()
        {
            return emotes.Values.Where(el => this.Settings.EmotesRadialEnabledMap.ContainsKey(el) ? this.Settings.EmotesRadialEnabledMap[el].Value : true).ToList();
        }

        public void Unload()
        {
            // Dispose all Textures
            foreach (var emote in emotes.Values)
            {
                emote.Texture?.Dispose();
            }
        }

        public void SendEmoteCommand(Emote emote)
        {
            if (emote.Locked)
            {
                Logger.Debug("SendEmoteCommand: Emote locked.");
                return;
            }
            // Send emote command to chat if in game and map closed
            if (GameService.GameIntegration.Gw2Instance.IsInGame && !GameService.Gw2Mumble.UI.IsMapOpen)
            {
                string command = emote.Command;
                if (IsEmoteSynchronized)
                {
                    command += " *";
                }
                if (IsEmoteTargeted)
                {
                    command += " @";
                }
                Logger.Debug(command);
                GameService.GameIntegration.Chat.Send(command);
            }
        }
    }
}
