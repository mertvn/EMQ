namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class Avatar
{
    public Avatar(string url)
    {
        Url = url;
    }

    public string Url { get; }
}
