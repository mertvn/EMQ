using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Client.Pages;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;

namespace EMQ.Client.Components;

public partial class LibraryStatsComponent
{
    public LibraryStats? LibraryStats { get; set; }

    public string SelectedTab { get; set; } = "TabGeneral";

    [CascadingParameter]
    public LibraryPage? LibraryPage { get; set; }

    [Parameter]
    public int Mode { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await RefreshStats();
    }

    public async Task RefreshStats()
    {
        LibraryStats? res = await _client.GetFromJsonAsync<LibraryStats?>($"Library/GetLibraryStats?mode={Mode}");
        if (res is not null)
        {
            LibraryStats = res;
            StateHasChanged();
        }
    }

    private void OnSelectedTabChanged(string name)
    {
        SelectedTab = name;
    }

    private async Task Onclick_Mst(string mst)
    {
        if (LibraryPage != null)
        {
            await LibraryPage.TabsComponent!.SelectTab("TabAutocompleteMst");
            LibraryPage.selectedMusicSourceTitle = mst;
            await LibraryPage.SelectedResultChangedMst();
        }
    }

    private async Task Onclick_A(int aId)
    {
        if (LibraryPage != null)
        {
            await LibraryPage.TabsComponent!.SelectTab("TabAutocompleteA");
            LibraryPage.selectedArtist = new AutocompleteA(aId, "", "");
            await LibraryPage.SelectedResultChangedA();
        }
    }

    private async Task Onclick_Uploader(string uploader)
    {
        if (LibraryPage != null)
        {
            await LibraryPage.TabsComponent!.SelectTab("TabAutocompleteMst");
            LibraryPage.selectedMusicSourceTitle = null;
            // LibraryPage.OnLibrarySongFilterChanged(new ChangeEventArgs()
            // {
            //     Value = LibrarySongFilterKind.All.ToString()
            // });
            await LibraryPage.SelectedResultChangedUploader(uploader);
        }
    }
}
