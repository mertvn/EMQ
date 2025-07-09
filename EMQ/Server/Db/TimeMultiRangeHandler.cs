using System;
using System.Data;
using System.Linq;
using Dapper;
using EMQ.Shared.Core;
using NpgsqlTypes;

namespace EMQ.Server.Db;

public class TimeMultiRangeHandler : SqlMapper.TypeHandler<TimeRange[]>
{
    public override TimeRange[] Parse(object? value)
    {
        if (value is null or DBNull)
            return Array.Empty<TimeRange>();

        if (value is NpgsqlRange<DateTime>[] dateTimeRanges)
        {
            return dateTimeRanges.Select(r =>
            {
                // Convert DateTime ranges to seconds-since-midnight
                double startTime = r.LowerBound.TimeOfDay.TotalSeconds;
                double endTime = r.UpperBound.TimeOfDay.TotalSeconds;
                return new TimeRange(startTime, endTime);
            }).ToArray();
        }

        throw new ArgumentException("Expected NpgsqlRange<DateTime>[]");
    }

    public override void SetValue(IDbDataParameter parameter, TimeRange[]? value)
    {
        if (value == null || value.Length == 0)
        {
            parameter.Value = DBNull.Value;
            return;
        }

        var dateTimeRanges = value.Select(r =>
        {
            var start = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(r.Start);
            var end = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(r.End);
            return new NpgsqlRange<DateTime>(start, true, end, false);
        }).ToArray();

        parameter.Value = dateTimeRanges;
    }
}
