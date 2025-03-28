using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete.Dto;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;

namespace EMQ.Client.Pages;

// meme thing please ignore
public partial class VNDBHairColorThing
{
    public string InputText { get; set; } = "";

    public List<string> ValidIds { get; set; } = new();

    public ResGetCharsWithSimilarHairColor[] Res { get; set; } = Array.Empty<ResGetCharsWithSimilarHairColor>();

    public string VndbAdvsearchStr { get; set; } = "";

    public bool IsReady = true;

    private async Task Onclick_Search()
    {
        if (IsReady)
        {
            IsReady = false;
            var req = new ReqGetCharsWithSimilarHairColor
            {
                TargetId = InputText.ToVndbId(),
                TopN = 100, // todo configurable
                ValidIds = ValidIds
            };
            var res = await _client.PostAsJsonAsync("Library/GetCharsWithSimilarHairColor", req);
            if (res.IsSuccessStatusCode)
            {
                Res = (await res.Content.ReadFromJsonAsync<ResGetCharsWithSimilarHairColor[]>())!;
            }

            IsReady = true;
        }
    }

    private async Task OnclickButtonFetchByVndbAdvsearchStr()
    {
        if (IsReady)
        {
            IsReady = false;
            VndbAdvsearchStr = VndbAdvsearchStr.SanitizeVndbAdvsearchStr();
            if (!string.IsNullOrWhiteSpace(VndbAdvsearchStr))
            {
                ValidIds = (await VndbMethods.GetCharacterIdsMatchingAdvsearchStr(ClientState.VndbInfo,
                                VndbAdvsearchStr) ??
                            Array.Empty<string>()).ToList();
            }
            else
            {
                ValidIds = new List<string>();
            }

            IsReady = true;
        }
    }
}
