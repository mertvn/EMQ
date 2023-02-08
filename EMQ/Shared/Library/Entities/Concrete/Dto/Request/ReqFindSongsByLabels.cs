using System;
using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;


namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsByLabels
{
    public ReqFindSongsByLabels(List<Label> labels)
    {
        Labels = labels;
    }

    public List<Label> Labels { get; }
}
