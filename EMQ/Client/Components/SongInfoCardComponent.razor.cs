using System;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;

namespace EMQ.Client.Components;

public partial class SongInfoCardComponent
{
    [Parameter]
    public Song? Song { get; set; }

    public SongReportComponent _songReportComponent { get; set; } = null!;

    private int _currentSongId;

    protected override bool ShouldRender()
    {
        if (Song is null || _currentSongId == Song.Id)
        {
            // Console.WriteLine("should not render");
            return false;
        }
        else
        {
            // Console.WriteLine("should render");
            _currentSongId = Song.Id;
            return true;
        }
    }
}
