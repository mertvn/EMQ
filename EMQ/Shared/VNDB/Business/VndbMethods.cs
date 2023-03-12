using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Juliet.Model.Filters;
using Juliet.Model.Param;
using Juliet.Model.Response;
using Juliet.Model.VNDBObject;
using Juliet.Model.VNDBObject.Fields;

namespace EMQ.Shared.VNDB.Business;

public static class VndbMethods
{
    public static async Task<List<Label>> GrabPlayerVNsFromVndb(PlayerVndbInfo vndbInfo)
    {
        var ret = new List<Label>();

        if (!string.IsNullOrWhiteSpace(vndbInfo.VndbId))
        {
            if (vndbInfo.Labels != null && vndbInfo.Labels.Any())
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
                            var query = new Predicate(FilterField.Label, FilterOperator.Equal, label.Id);

                            var playerVns = await Juliet.Api.POST_ulist(new ParamPOST_ulist()
                            {
                                User = vndbInfo.VndbId,
                                APIToken = vndbInfo.VndbApiToken,
                                Fields = new List<FieldPOST_ulist>() { FieldPOST_ulist.LabelsId, FieldPOST_ulist.Vote },
                                Exhaust = true,
                                Filters = query,
                            });
                            if (playerVns != null)
                            {
                                var results = playerVns.SelectMany(x => x.Results);
                                foreach (ResPOST_ulist result in results)
                                {
                                    label.VNs[result.Id.ToVndbUrl()] = result.Vote ?? -1;
                                }

                                ret.Add(label);
                                Console.WriteLine(
                                    $"Grabbed {label.VNs.Count} vns for label {label.Id} ({label.Name})");
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

    public static async Task<string[]?> GetVnUrlsMatchingAdvsearchStr(string advsearchStr)
    {
        var res = await Juliet.Api.POST_vn(new ParamPOST_vn()
        {
            Fields = new List<FieldPOST_vn>() { FieldPOST_vn.Id }, RawFilters = advsearchStr
        });

        return res?.SelectMany(x => x.Results.Select(y => y.Id.ToVndbUrl())).Distinct().ToArray();
    }
}
