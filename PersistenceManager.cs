using Blish_HUD.Modules.Managers;
using Blish_HUD;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace felix.BlishEmotes
{
    class JsonVersionWrapper<T>
    {
        [JsonProperty("version", Required = Required.Always)] public string Version { get; set; }
        [JsonProperty("data", Required = Required.Always)] public T Data { get; set; }

        public JsonVersionWrapper(string version, T data)
        {
            this.Version = version;
            this.Data = data;
        }
    }
    class PersistenceManager
    {

        private static readonly Logger Logger = Logger.GetLogger<PersistenceManager>();

        private string _baseDirectoryPath;
        private string _categoriesFile => Path.Combine(_baseDirectoryPath, "categories.json");

        public PersistenceManager(DirectoriesManager directoriesManager)
        {
            IReadOnlyList<string> registeredDirectories = directoriesManager.RegisteredDirectories;
            _baseDirectoryPath = directoriesManager.GetFullDirectoryPath(registeredDirectories[0]);
        }

        public List<Category> LoadCategories()
        {
            try
            {
                var categories = LoadJson<List<Category>>(_categoriesFile);
                return categories;
            }
            catch (FileNotFoundException e)
            {
                Logger.Debug("Category file not found.");
                // Re-throw because CategoriesManager will now have to setup default categories
                throw e;
            }
            catch (Exception)
            {
                return new List<Category>();
            }
        }

        public void SaveCategories(List<Category> categories)
        {
            try
            {
                SaveJson(categories, Category.VERSION, _categoriesFile);
            }
            catch (Exception)
            {
            }
        }

        private void SaveJson<T>(T json, string version, string file)
        {
            try
            {
                string serialized = JsonConvert.SerializeObject(new JsonVersionWrapper<T>(version, json));
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
                Logger.Error($"Failed to write json file {file} due to {e.GetType().FullName}");
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
                var json = JsonConvert.DeserializeObject<JsonVersionWrapper<T>>(content);
                Logger.Debug($"Successfully loaded json from {file}");
                return json.Data;
            }
            catch (FileNotFoundException e)
            {
                Logger.Warn($"File {file} not found");
                throw e;
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
                Logger.Error($"Failed to read json file {file} due to {e.GetType().FullName}");
                Logger.Error(e.Message);
                Logger.Debug(e.StackTrace);
                throw e;
            }
        }
    }
}
