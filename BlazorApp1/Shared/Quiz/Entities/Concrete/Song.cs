using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class Song
{
    public string Name { get; set; }

    public string Artist { get; set; }

    public string[] Links { get; set; } // todo multiple links

    [JsonIgnore] public string Data { get; set; }

    // public DateTime Date { get; set; } // todo should this be here, or SongSource, or both

    // todo type

    public SongSource Source { get; set; }
}
