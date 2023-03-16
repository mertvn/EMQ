using System;
using System.Collections.Generic;
using System.Linq;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class QuizFilters
{
    public List<CategoryFilter> CategoryFilters { get; set; } = new();

    public Dictionary<SongSourceSongType, bool> SongSourceSongTypeFilters { get; set; } =
        Enum.GetValues<SongSourceSongType>().Where(x => x != SongSourceSongType.Unknown)
            .ToDictionary(x => x, _ => true);


    // todo move all applicable filters here
}
