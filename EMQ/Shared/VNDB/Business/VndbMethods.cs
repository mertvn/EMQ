using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Juliet.Model.Filters;
using Juliet.Model.Param;
using Juliet.Model.VNDBObject;

namespace EMQ.Shared.VNDB.Business;

public static class VndbMethods
{
    // todo: consider rewriting this to grab all vns in a single Juliet.Api.POST_ulist call (~20% faster for my list)
    // would need more post-processing to match returned vns to labels though, so it might not be a big improvement
    public static async Task<List<Label>> GrabPlayerVNsFromVndb(PlayerVndbInfo vndbInfo)
    {
        var ret = new List<Label>();

        if (!string.IsNullOrWhiteSpace(vndbInfo.VndbId))
        {
            if (vndbInfo.Labels != null)
            {
                if (vndbInfo.Labels.Any())
                {
                    // Console.WriteLine("GrabPlayerVNsFromVndb labels: " +
                    //                   JsonSerializer.Serialize(vndbInfo.Labels, Utils.JsoIndented));

                    foreach (var label in vndbInfo.Labels)
                    {
                        if (label.Kind is LabelKind.Include or LabelKind.Exclude)
                        {
                            // The ‘Voted’ label (id=7) is always included even when private.
                            if (!label.IsPrivate || !string.IsNullOrWhiteSpace(vndbInfo.VndbApiToken) || label.Id == 7)
                            {
                                var query = new List<Query>()
                                {
                                    new Predicate(FilterField.Label, FilterOperator.Equal, label.Id)
                                };

                                var playerVns = await Juliet.Api.POST_ulist(new ParamPOST_ulist()
                                {
                                    User = vndbInfo.VndbId,
                                    APIToken = vndbInfo.VndbApiToken,
                                    Exhaust = true,
                                    Filters = new Combinator(CombinatorKind.Or, query),
                                });
                                if (playerVns != null)
                                {
                                    label.VnUrls = playerVns.SelectMany(x => x.Results.Select(y => y.Id.ToVndbUrl()))
                                        .ToList();
                                    ret.Add(label);
                                    Console.WriteLine(
                                        $"Grabbed {label.VnUrls.Count} vns for label {label.Id} ({label.Name})");
                                }
                                else
                                {
                                    Console.WriteLine($"Error grabbing {vndbInfo.VndbId}'s VNs");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Skipping private label {label.Id} ({label.Name})");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"Encountered null vndbInfo.Labels when grabbing vns");
            }
        }
        else
        {
            Console.WriteLine($"Encountered invalid VndbId when grabbing vns");
        }

        return ret;
    }

    public static async Task<VNDBLabel[]> GetLabels(PlayerVndbInfo vndbInfo)
    {
        var res = await Juliet.Api.GET_ulist_labels(new Param()
        {
            User = vndbInfo.VndbId, APIToken = vndbInfo.VndbApiToken
        });

        return res != null ? res.Labels : Array.Empty<VNDBLabel>();
    }
}
