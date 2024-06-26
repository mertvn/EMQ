﻿using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Imports.SongMatching.Common;

public struct Uploadable
{
    public string Path { get; set; }

    public int MId { get; init; }

    public string? ResultUrl { get; set; }

    // todo broken since persistent mId changes
    public SongLite SongLite { get; set; }

    public string? MusicBrainzRecording { get; init; }
}
