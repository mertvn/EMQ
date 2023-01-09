using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;

namespace EMQ.Client.Pages;

public partial class SongInfoCardComponent
{
    [Parameter]
    public Song? song { get; set; }
}
