namespace Sidekick.Apis.Poe.Trade.Requests.Filters
{
    internal class StatFilters
    {
        public string? Id { get; set; }
        public SearchFilterValue? Value { get; set; }
        public bool Disabled => false;
    }
}