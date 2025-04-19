using System;
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
    public SongSourceSongTypeMode Mode { get; set; }

    public string SelectedTabArtist { get; set; } = "All";

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
            await LibraryPage.SelectedResultChangedUploader(uploader);
        }
    }

    private async Task Onclick_Year(DateTime year)
    {
        if (LibraryPage != null)
        {
            await LibraryPage.TabsComponent!.SelectTab("TabAutocompleteMst");
            LibraryPage.selectedMusicSourceTitle = null;
            await LibraryPage.SelectedResultChangedYear(year, Mode);
        }
    }

    private async Task Onclick_Difficulty(SongDifficultyLevel difficulty)
    {
        if (LibraryPage != null)
        {
            await LibraryPage.TabsComponent!.SelectTab("TabAutocompleteMst");
            LibraryPage.selectedMusicSourceTitle = null;
            await LibraryPage.SelectedResultChangedDifficulty(difficulty, Mode);
        }
    }

    private async Task Onclick_Warning(MediaAnalyserWarningKind warning)
    {
        if (LibraryPage != null)
        {
            await LibraryPage.TabsComponent!.SelectTab("TabAutocompleteMst");
            LibraryPage.selectedMusicSourceTitle = null;
            await LibraryPage.SelectedResultChangedWarning(warning, Mode);
        }
    }
}
