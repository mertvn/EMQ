namespace EMQ.Shared.Library.Entities.Concrete;

public struct AutocompleteA
{
    public AutocompleteA(int aId, string aaLatinAlias)
    {
        this.aId = aId;
        this.aaLatinAlias = aaLatinAlias;
    }

    public int aId { get; set; }

    public string aaLatinAlias { get; set; }
}
