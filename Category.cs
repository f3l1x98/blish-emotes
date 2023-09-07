using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace felix.BlishEmotes
{
    class Category
    {
        [JsonIgnore] public static readonly string FAVOURITES_CATEGORY_NAME = "Favourites";

        [JsonProperty("id", Required = Required.Always)] public Guid Id { get; set; }
        [JsonProperty("name", Required = Required.Always)] public string Name { get; set; }
        [JsonProperty("emoteIds", Required = Required.Always)] public List<string> EmoteIds { get; set; }
        [JsonProperty("isFavourite", DefaultValueHandling = DefaultValueHandling.Populate)] public bool IsFavourite { get; set; } = false;

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
        public void RemoveEmote(string id)
        {
            EmoteIds.Remove(id);
            Emotes.RemoveAll((emote) => emote.Id == id);
        }
    }
}