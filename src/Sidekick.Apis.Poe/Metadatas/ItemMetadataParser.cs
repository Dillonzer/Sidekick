using System.Text.RegularExpressions;
using Sidekick.Apis.Poe.Parser;
using Sidekick.Apis.Poe.Parser.Patterns;
using Sidekick.Common.Game.Items;
using Sidekick.Common.Game.Languages;
using Sidekick.Common.Initialization;

namespace Sidekick.Apis.Poe.Metadatas
{
    public class ItemMetadataParser : IItemMetadataParser
    {
        private readonly IGameLanguageProvider gameLanguageProvider;
        private readonly IParserPatterns parserPatterns;
        private readonly IItemMetadataProvider data;

        public ItemMetadataParser(
            IGameLanguageProvider gameLanguageProvider,
            IParserPatterns parserPatterns,
            IItemMetadataProvider data)
        {
            this.gameLanguageProvider = gameLanguageProvider;
            this.parserPatterns = parserPatterns;
            this.data = data;
        }

        private Regex Prefixes { get; set; } = null!;

        /// <inheritdoc/>
        public InitializationPriority Priority => InitializationPriority.Medium;

        private string GetLineWithoutPrefixes(string line) => Prefixes.Replace(line, string.Empty);

        /// <inheritdoc/>
        public Task Initialize()
        {
            if (gameLanguageProvider.Language == null)
            {
                throw new Exception("[Item Metadata] Could not find a valid language.");
            }

            Prefixes = new Regex("^(?:" +
                gameLanguageProvider.Language.PrefixSuperior + " |" +
                gameLanguageProvider.Language.PrefixBlighted + " |" +
                gameLanguageProvider.Language.PrefixBlightRavaged + " |" +
                gameLanguageProvider.Language.PrefixAnomalous + " |" +
                gameLanguageProvider.Language.PrefixDivergent + " |" +
                gameLanguageProvider.Language.PrefixPhantasmal + " )");

            return Task.CompletedTask;
        }

        public ItemMetadata? Parse(ParsingItem parsingItem)
        {
            var parsingBlock = parsingItem.Blocks.First();
            parsingBlock.Parsed = true;

            var itemRarity = GetRarity(parsingBlock);

            var canBeVaalGem = itemRarity == Rarity.Gem && parsingItem.Blocks.Count > 7;
            if (canBeVaalGem && data.NameAndTypeDictionary.TryGetValue(parsingItem.Blocks[5].Lines[0].Text, out var vaalGem))
            {
                return vaalGem.First();
            }

            // Get name and type text
            string? name = null;
            string? type = null;
            if (parsingBlock.Lines.Count >= 4)
            {
                name = parsingBlock.Lines[2].Text;
                type = parsingBlock.Lines[3].Text;
            }
            else if (parsingBlock.Lines.Count == 3)
            {
                name = parsingBlock.Lines[2].Text;
                type = parsingBlock.Lines[2].Text;
            }

            // Rares may have conflicting names, so we don't want to search any unique items that may have that name. Like "Ancient Orb" which can be used by abyss jewels.
            if (itemRarity == Rarity.Rare || itemRarity == Rarity.Magic)
            {
                name = null;
            }

            var result = Parse(name, type);
            if (result == null)
            {
                return null;
            }

            // If we don't have the rarity from the metadata, we set it to the value from the text
            if (result.Rarity == Rarity.Unknown)
            {
                result.Rarity = itemRarity;
            }

            if (result.Category == Category.ItemisedMonster && result.Rarity == Rarity.Unique && string.IsNullOrEmpty(result.Name))
            {
                result.Name = name;
            }

            if (result.Class == Class.Undefined)
            {
                result.Class = GetClass(parsingBlock);
            }

            return result;
        }

        public ItemMetadata? Parse(string? name, string? type)
        {
            // We can find multiple matches while parsing. This will store all of them. We will figure out which result is correct at the end of this method.
            var results = new List<ItemMetadata>();

            // There are some items which have prefixes which we don't want to remove, like the "Blighted Delirium Orb".
            if (!string.IsNullOrEmpty(name) && data.NameAndTypeDictionary.TryGetValue(name, out var itemData))
            {
                results.AddRange(itemData);
            }
            // Here we check without any prefixes
            else if (!string.IsNullOrEmpty(name) && data.NameAndTypeDictionary.TryGetValue(GetLineWithoutPrefixes(name), out itemData))
            {
                results.AddRange(itemData);
            }
            // Now we check the type
            else if (!string.IsNullOrEmpty(type) && data.NameAndTypeDictionary.TryGetValue(type, out itemData))
            {
                results.AddRange(itemData);
            }
            // Finally. if we don't have any matches, we will look into our regex dictionary
            else
            {
                if (!string.IsNullOrEmpty(name))
                {
                    results.AddRange(data.NameAndTypeRegex
                        .Where(pattern => pattern.Regex.IsMatch(name))
                        .Select(x => x.Item));
                }

                if (!string.IsNullOrEmpty(type))
                {
                    results.AddRange(data.NameAndTypeRegex
                        .Where(pattern => pattern.Regex.IsMatch(type))
                        .Select(x => x.Item));
                }
            }

            if (results.Any(x => x.Type == type))
            {
                return results.FirstOrDefault(x => x.Type == type);
            }

            if (results.Any(x => x.Rarity == Rarity.Unique))
            {
                return results.FirstOrDefault(x => x.Rarity == Rarity.Unique);
            }

            if (results.Any(x => x.Rarity == Rarity.Unknown))
            {
                return results.FirstOrDefault(x => x.Rarity == Rarity.Unknown);
            }

            return results.FirstOrDefault();
        }

        private Rarity GetRarity(ParsingBlock parsingBlock)
        {
            foreach (var pattern in parserPatterns.Rarity)
            {
                if (pattern.Value.IsMatch(parsingBlock.Lines[1].Text))
                {
                    return pattern.Key;
                }
            }

            throw new NotSupportedException("Item rarity is unknown.");
        }

        private Class GetClass(ParsingBlock parsingBlock)
        {
            foreach (var pattern in parserPatterns.Classes)
            {
                if (pattern.Value.IsMatch(parsingBlock.Lines[0].Text))
                {
                    return pattern.Key;
                }
            }

            return Class.Undefined;
        }
    }
}