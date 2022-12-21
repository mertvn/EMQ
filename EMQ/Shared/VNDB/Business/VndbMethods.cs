using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Juliet.Model.Param;

namespace EMQ.Shared.VNDB.Business;

public static class VndbMethods
{
    public static async Task<List<string>> GrabPlayerVNsFromVNDB(PlayerVndbInfo vndbInfo)
    {
        var ret = new List<string>();

        if (!string.IsNullOrWhiteSpace(vndbInfo.VndbId))
        {
            var playerVns = await Juliet.Api.POST_ulist(new ParamPOST_ulist()
            {
                User = vndbInfo.VndbId, APIToken = vndbInfo.VndbApiToken
            });
            if (playerVns != null)
            {
                ret.AddRange(playerVns.SelectMany(x => x.Results.Select(y => "https://vndb.org/" + y.Id))); // todo
                Console.WriteLine($"Grabbed {ret.Count} vns for {vndbInfo.VndbId}");
            }
            else
            {
                Console.WriteLine($"Error grabbing {vndbInfo.VndbId}'s VNs");
            }
        }
        else
        {
            Console.WriteLine($"Encountered invalid VndbId when grabbing vns");
        }

        return ret;
    }
}
