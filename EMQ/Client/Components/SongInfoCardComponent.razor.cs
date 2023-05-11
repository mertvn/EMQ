using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;

namespace EMQ.Client.Components;

public partial class SongInfoCardComponent
{
    [Parameter]
    public Song? Song { get; set; }
}
