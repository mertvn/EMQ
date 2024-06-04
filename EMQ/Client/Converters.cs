using System.Collections.Generic;
using System.Linq;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Client;

public static class Converters
{
    public static Title GetSingleTitle(IEnumerable<Title> titles, string language1 = "ja", string language2 = "en")
    {
        // todo proper language preferences
        if (ClientState.Preferences.WantsEnglish)
        {
            language1 = "en";
            language2 = "ja";
        }
        // var romanizationPreference = true;

        // not chained together for debugging purposes
        var one = titles.OrderByDescending(x => x.Language == language1);
        var two = one.ThenByDescending(x => x.Language == language2);
        var three = two.ThenByDescending(x => x.IsMainTitle);

        return three.First();
    }
}
