using Blish_HUD;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace felix.BlishEmotes
{
    class Category
    {
        [JsonIgnore] public static readonly string FAVOURITES_CATEGORY_NAME = "Favourites";
        [JsonIgnore] public static readonly string VERSION = "V2";

        [JsonProperty("id", Required = Required.Always)] public Guid Id { get; set; }
        [JsonProperty("name", Required = Required.Always)] public string Name { get; set; }
        [JsonProperty("emoteIds", Required = Required.Always)] public List<string> EmoteIds { get; set; }
        [JsonProperty("isFavourite", DefaultValueHandling = DefaultValueHandling.Populate)] public bool IsFavourite { get; set; } = false;
        // TODO THIS WILL REQUIRE A MIGRATION (just set to this.Emotes[0].Texture if this.Emotes.Count > 0, otherwise to EmotesManager.GetAll()[0].Texture)
        [JsonProperty("textureFileName", Required = Required.Always)] public string TextureFileName { get; set; }

        [JsonIgnore] public Texture2D Texture { get; set; }
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