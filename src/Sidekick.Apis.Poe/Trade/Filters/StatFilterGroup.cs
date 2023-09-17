using System.Text.Json.Serialization;
using Sidekick.Common.Enums;

namespace Sidekick.Apis.Poe.Trade.Filters
{
    internal class StatFilterGroup
    {
        [JsonIgnore]
        public StatType Type { get; set; }

        [JsonPropertyName("type")]
        public string? TypeAsString => Type.GetValueAttribute() ?? StatType.And.GetValueAttribute();

        public List<StatFilter> Filters { get; set; } = new();

        public SearchFilterValue? Value { get; set; }
    }
}
