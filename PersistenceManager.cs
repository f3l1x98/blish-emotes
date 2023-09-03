using Blish_HUD.Modules.Managers;
using Blish_HUD;
using Gw2Sharp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Xna.Framework.Content;

namespace felix.BlishEmotes
{
    class PersistenceManager
    {

        private static readonly Logger Logger = Logger.GetLogger<PersistenceManager>();
        private ContentsManager _contentsManager;

        private string _baseDirectoryPath;

        private string _emotesFile => Path.Combine(_baseDirectoryPath, "emotes.json");

        public PersistenceManager(DirectoriesManager directoriesManager, ContentsManager contentsManager)
        {
            _contentsManager = contentsManager;
            IReadOnlyList<string> registeredDirectories = directoriesManager.RegisteredDirectories;
            if (registeredDirectories.Count == 0)
            {
                Logger.Fatal("No directories registered!");
                throw new Exception("Failed to initialize - No directories registered");
            }
            else if (registeredDirectories.Count != 1)
            {
                Logger.Fatal($"Wrong number of registered directories: {registeredDirectories.Count}");
                throw new Exception("Failed to initialize - Wrong number of registered directories");
            }

            _baseDirectoryPath = directoriesManager.GetFullDirectoryPath(registeredDirectories[0]);
        }

        public List<Emote> LoadEmotes()
        {
            try
            {
                var emotes = LoadJson<List<Emote>>(_emotesFile);
                return emotes;
            }
            catch (FileNotFoundException)
            {
                Logger.Debug("Emote file not found - init with resource file.");
                // Read resource file
                string fileContents;
                using (StreamReader reader = new StreamReader(_contentsManager.GetFileStream(@"json/emotes.json")))
                {
                    fileContents = reader.ReadToEnd();
                }
                var emotes = JsonConvert.DeserializeObject<List<Emote>>(fileContents);
                // Save to emote file
                SaveEmotes(emotes);
                return emotes;
            }
            catch (Exception)
            {
                return new List<Emote>();
            }
        }

        public void SaveEmotes(List<Emote> emotes)
        {
            try
            {
                SaveJson(emotes, _emotesFile);
            }
            catch (Exception)
            {
            }
        }

        private void SaveJson<T>(T json, string file)
        {
            try
            {
                string serialized = JsonConvert.SerializeObject(json);
                File.WriteAllText(file, serialized);
                Logger.Debug($"Successfully saved json to {file}");
            }
            catch (JsonException e)
            {
                Logger.Error("Failed to serialize json!");
                Logger.Error(e.Message);
                Logger.Debug(e.StackTrace);
                throw e;
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to write json file due to {e.GetType().FullName}");
                Logger.Error(e.Message);
                Logger.Debug(e.StackTrace);
                throw e;
            }
        }

        private T LoadJson<T>(string file)
        {
            try
            {
                string content = File.ReadAllText(file);
                var json = JsonConvert.DeserializeObject<T>(content);
                Logger.Debug($"Successfully loaded json from {file}");
                return json;
            }
            catch (JsonException e)
            {
                Logger.Error("Failed to deserialize json!");
                Logger.Debug(e.Message);
                Logger.Debug(e.StackTrace);
                throw e;
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to read emotes from {_emotesFile}");
                Logger.Error(e.Message);
                Logger.Debug(e.StackTrace);
                throw e;
            }
        }
    }
}
