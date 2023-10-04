using Blish_HUD;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace felix.BlishEmotes
{
    public class Category : RadialBase
    {
        [JsonIgnore] public static readonly string FAVOURITES_CATEGORY_NAME = "Favourites";
        [JsonIgnore] public static readonly string VERSION = "V1";

        [JsonProperty("id", Required = Required.Always)] public Guid Id { get; set; }
        [JsonProperty("name", Required = Required.Always)] public string Name { get; set; }
        [JsonProperty("emoteIds", Required = Required.Always)] public List<string> EmoteIds { get; set; }
        [JsonProperty("isFavourite", DefaultValueHandling = DefaultValueHandling.Populate)] public bool IsFavourite { get; set; } = false;
        [JsonProperty("textureFileName", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(CategoriesManager.DEFAULT_TEXTURE_FILE_NAME)]
        public string TextureFileName { get; set; }

        [JsonIgnore] public List<Emote> Emotes { get; set; } = new List<Emote>();

        public Category Clone()
        {
            return new Category()
            { 
                Id = this.Id, 
                Name = this.Name, 
                EmoteIds = new List<string>(this.EmoteIds), 
                Emotes = new List<Emote>(this.Emotes), 
                IsFavourite = this.IsFavourite, 
                TextureFileName = this.TextureFileName,
                Texture = this.Texture,
            };
        }

        public void AddEmote(Emote emote)
        {
            // Check if already in list
            if (EmoteIds.Contains(emote.Id))
            {
                return;
            }

            EmoteIds.Add(emote.Id);
            Emotes.Add(emote);
        }
        public void RemoveEmote(Emote emote)
        {
            EmoteIds.Remove(emote.Id);
            Emotes.RemoveAll((e) => e.Id == emote.Id);
        }
    }
}