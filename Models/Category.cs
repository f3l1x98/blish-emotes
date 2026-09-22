using Blish_HUD;
using felix.BlishEmotes.Services;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace felix.BlishEmotes
{
    public class Category : RadialBase
    {
        public static readonly string FAVOURITES_CATEGORY_NAME = "Favourites";

        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<string> EmoteIds { get; set; }
        public bool IsFavourite { get; set; } = false;
        public string TextureFileName { get; set; }

        /// <summary>
        /// Id of the catalog category this category is linked to ("linked category" in CONTEXT.md):
        /// it receives newly fetched emotes tagged with that catalog category. Null = purely
        /// user-owned. Persisted via the V2 categories.json; see CatalogMerge.
        /// </summary>
        public string CatalogId { get; set; }

        public List<Emote> Emotes { get; set; } = new List<Emote>();

        public override string Label {
            get => Name;
        }

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
                CatalogId = this.CatalogId,
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