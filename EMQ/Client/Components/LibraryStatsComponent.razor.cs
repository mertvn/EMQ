using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Library.Entities.Concrete;

namespace EMQ.Client.Components;

public partial class LibraryStatsComponent
{
    public LibraryStats? LibraryStats { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await RefreshStats();
    }

    public async Task RefreshStats()
    {
        LibraryStats? res = await _client.GetFromJsonAsync<LibraryStats?>("Library/GetLibraryStats");
        if (res is not null)
        {
            LibraryStats = res;
            StateHasChanged();
        }
    }
}
