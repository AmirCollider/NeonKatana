using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Every piece of text in the game, in every language, in one asset. Create it through
    /// Assets ▸ Create ▸ Neon Katana ▸ Localization Table and hand it to the
    /// <see cref="LocalizationService"/> in the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "Neon Katana/Localization Table")]
    public class LocalizationTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("What the game asks for, e.g. \"menu.resume\". Never shown to the player.")]
            public string key;

            [TextArea] public string english;
            [TextArea] public string persian;
            [TextArea] public string japanese;

            public string For(GameLanguage language) => language switch
            {
                GameLanguage.Persian => persian,
                GameLanguage.Japanese => japanese,
                _ => english,
            };
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        /// <summary>Builds the lookup the service reads from, and complains about duplicate keys.</summary>
        public Dictionary<string, Entry> BuildLookup()
        {
            var lookup = new Dictionary<string, Entry>(entries.Length);

            foreach (Entry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;

                if (lookup.ContainsKey(entry.key))
                {
                    Debug.LogWarning($"'{name}' has more than one entry for the key '{entry.key}'. Keeping the first.", this);
                    continue;
                }

                lookup.Add(entry.key, entry);
            }

            return lookup;
        }
    }
}
