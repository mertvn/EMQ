namespace EMQ.Shared.Library.Entities.Concrete;

public struct AutocompleteA
{
    public AutocompleteA(int aId, string aaLatinAlias, string aaNonLatinAlias = "")
    {
        this.aId = aId;
        this.aaLatinAlias = aaLatinAlias;
        this.aaNonLatinAlias = aaNonLatinAlias;
    }

    public int aId { get; set; }

    public string aaLatinAlias { get; set; }

    public string aaNonLatinAlias { get; set; }
}
