using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using DapperQueryBuilder;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using Npgsql;

namespace EMQ.Server.Db.Imports.EGS;

public static class EgsImporter
{
    public static readonly Dictionary<int, List<string>> CreaterNameOverrideDict = new();

    public static async Task ImportEgsData()
    {
        string date = Constants.ImportDateEgs;
        string folder = $"C:\\emq\\egs\\{date}";

        var createrNameOverrideDictFile =
            JsonSerializer.Deserialize<Dictionary<int, List<string>>>(
                await File.ReadAllTextAsync("C:\\emq\\egs\\createrNameOverrideDict.json"), Utils.JsoIndented)!;
        foreach ((int key, List<string>? value) in createrNameOverrideDictFile)
        {
            if (!CreaterNameOverrideDict.TryAdd(key, value))
            {
                throw new Exception($"creater id {key} is repeated in the override dict");
            }
        }

        var egsDataList = new List<EgsData>();
        string[] rows = await File.ReadAllLinesAsync($"{folder}\\egs.tsv");
        for (int i = 1; i < rows.Length; i++)
        {
            string row = rows[i];
            string[] split = row.Split("\t");

            SongSourceSongType gameMusicCategory;
            if (split[4].Contains("OP") || split[4].Contains("主題歌") || split[4].Contains("テーマ"))
            {
                gameMusicCategory = SongSourceSongType.OP;
            }
            else if (split[4].Contains("ED"))
            {
                gameMusicCategory = SongSourceSongType.ED;
            }
            else if (split[4].Contains("挿入歌") || split[4].Contains("イメージソング") || split[4].Contains("キャラソン") ||
                     split[4].Contains("イメーシーソング") || split[4].Contains("イメージ") || split[4].Contains("イメ－ジソング"))
            {
                gameMusicCategory = SongSourceSongType.Insert;
            }
            else if (split[4].Contains("BGM"))
            {
                gameMusicCategory = SongSourceSongType.BGM;
            }
            else if (split[4].Contains("Vo楽曲") || split[4].Contains("game size") || split[4].Contains("インストゥルメンタル") ||
                     split[4].Contains("アレンジ") || split[4].Contains("収録曲") || split[4].Contains("ボーカルソング") ||
                     split[4].Contains("特別曲") || split[4].Contains("本編未使用曲") || split[4].Contains("スペシャルソング") ||
                     split[4].Contains("関連曲") || split[4].Contains("ファーストソング") || split[4].Contains("歌曲") ||
                     split[4].Contains("ワールドソング") || split[4].Contains("ユアソング") || split[4].Contains("劇中歌") ||
                     split[4].Contains("リミックス") || split[4].Contains("新ミックス") || split[4].Contains("新録") ||
                     split[4].Contains("カバー"))
            {
                gameMusicCategory = SongSourceSongType.Unknown;
            }
            else
            {
                throw new ArgumentOutOfRangeException("", $"mId {Convert.ToInt32(split[0])} " + split[4]);
            }

            if (gameMusicCategory == SongSourceSongType.Unknown)
            {
                // Console.WriteLine("Skipping row with Unknown GameMusicCategory");
                continue;
            }

            string gameVndbUrl = split[6];
            if (string.IsNullOrWhiteSpace(gameVndbUrl))
            {
                // todo try using the EGS links attached to VNDB releases
                // Console.WriteLine("Skipping row with no GameVndbUrl");
                continue;
            }

            gameVndbUrl = gameVndbUrl.ToVndbUrl();

            // todo extract to file
            var blacklist = new List<string>
            {
                "https://vndb.org/v1141",
                "https://vndb.org/v2002",
                "https://vndb.org/v18760",
                "https://vndb.org/v1183",
                "https://vndb.org/v28",
                "https://vndb.org/v273",
                "https://vndb.org/v1708",
                "https://vndb.org/v827",
                "https://vndb.org/v1060",
                "https://vndb.org/v2375",
                "https://vndb.org/v6846",
                "https://vndb.org/v1852",
                "https://vndb.org/v4329",
                "https://vndb.org/v2501",
                "https://vndb.org/v264",
                "https://vndb.org/v542",
                "https://vndb.org/v592",
                "https://vndb.org/v35",
                "https://vndb.org/v231",
                "https://vndb.org/v362",
                "https://vndb.org/v473",
                "https://vndb.org/v10020",
                "https://vndb.org/v22505",
                "https://vndb.org/v12849",
                "https://vndb.org/v67",
                "https://vndb.org/v68",
                "https://vndb.org/v8533",
                "https://vndb.org/v6540",
                "https://vndb.org/v575",
                "https://vndb.org/v804",
                "https://vndb.org/v1967",
                "https://vndb.org/v5939",
                "https://vndb.org/v1899",
                "https://vndb.org/v323",
                "https://vndb.org/v90",
                "https://vndb.org/v15652",
                "https://vndb.org/v2632",
                "https://vndb.org/v20424",
                "https://vndb.org/v1152",
                "https://vndb.org/v1153",
                "https://vndb.org/v1284",
                "https://vndb.org/v646",
                "https://vndb.org/v3074",
                "https://vndb.org/v2790",
                "https://vndb.org/v8435",
                "https://vndb.org/v16974",
                "https://vndb.org/v3699",
                "https://vndb.org/v916",
                "https://vndb.org/v15653",
                "https://vndb.org/v4",
                "https://vndb.org/v3859",
                "https://vndb.org/v13666",
                "https://vndb.org/v18791",
                "https://vndb.org/v15727",
                "https://vndb.org/v12831",
                "https://vndb.org/v23863",
                "https://vndb.org/v19397",
                "https://vndb.org/v17823",
                "https://vndb.org/v23740",
				"https://vndb.org/v1491",
                "https://vndb.org/v5021",
                "https://vndb.org/v421",
                "https://vndb.org/v6173",
                "https://vndb.org/v13774",
                "https://vndb.org/v17147",
                "https://vndb.org/v15658",
                "https://vndb.org/v26888",
                "https://vndb.org/v6242",
                "https://vndb.org/v1359",
                "https://vndb.org/v16032",
                "https://vndb.org/v7771",
                "https://vndb.org/v24803",
                "https://vndb.org/v4506",
                "https://vndb.org/v5834",
                "https://vndb.org/v5",
                "https://vndb.org/v38",
                "https://vndb.org/v85",
                "https://vndb.org/v180",
                "https://vndb.org/v192",
                "https://vndb.org/v200",
                "https://vndb.org/v234",
                "https://vndb.org/v266",
                "https://vndb.org/v337",
                "https://vndb.org/v369",
                "https://vndb.org/v405",
                "https://vndb.org/v515",
                "https://vndb.org/v629",
                "https://vndb.org/v862",
                "https://vndb.org/v865",
                "https://vndb.org/v1180",
                "https://vndb.org/v1280",
                "https://vndb.org/v1337",
                "https://vndb.org/v1338",
                "https://vndb.org/v1362",
                "https://vndb.org/v1399",
                "https://vndb.org/v1492",
                "https://vndb.org/v1545",
                "https://vndb.org/v1552",
                "https://vndb.org/v1646",
                "https://vndb.org/v1884",
                "https://vndb.org/v1972",
                "https://vndb.org/v2082",
                "https://vndb.org/v2205",
                "https://vndb.org/v2301",
                "https://vndb.org/v2517",
                "https://vndb.org/v2622",
                "https://vndb.org/v2654",
                "https://vndb.org/v2782",
                "https://vndb.org/v2959",
                "https://vndb.org/v3370",
                "https://vndb.org/v4308",
                "https://vndb.org/v4494",
                "https://vndb.org/v4693",
                "https://vndb.org/v4822",
                "https://vndb.org/v5097",
                "https://vndb.org/v5121",
                "https://vndb.org/v5247",
                "https://vndb.org/v5668",
                "https://vndb.org/v5957",
                "https://vndb.org/v6700",
                "https://vndb.org/v7557",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
            };
            if (blacklist.Contains(gameVndbUrl))
            {
                continue;
            }

            int createrId = Convert.ToInt32(split[13]);

            List<string> createrName = new() { split[14] };
            if (CreaterNameOverrideDict.ContainsKey(createrId))
            {
                createrName = CreaterNameOverrideDict[createrId];
            }

            var egsData = new EgsData
            {
                MusicId = Convert.ToInt32(split[0]),
                MusicName = split[1],
                MusicFurigana = split[2],
                MusicPlaytime = split[3],
                GameMusicCategory = gameMusicCategory,
                GameName = split[5],
                GameVndbUrl = gameVndbUrl,
                SingerCharacterName = split[9],
                SingerFeaturing = split[10] == "t",
                CreaterId = createrId,
                CreaterNames = createrName,
                CreaterFurigana = split[15],
            };

            egsDataList.Add(egsData);
        }

        // remove duplicate items caused by EGS storing releases as separate games
        List<EgsData> toRemove = new();
        for (int i = 0; i < egsDataList.Count; i++)
        {
            EgsData egsData = egsDataList[i];

            var match = egsDataList.Skip(i + 1).Where(x =>
                x.MusicName == egsData.MusicName &&
                x.GameVndbUrl == egsData.GameVndbUrl &&
                x.GameMusicCategory == egsData.GameMusicCategory &&
                x.CreaterNames.Any(y => egsData.CreaterNames.Contains(y)));

            toRemove.AddRange(match);
        }

        Console.WriteLine("toRemove count: " + toRemove.Count);
        foreach (EgsData egsData in toRemove)
        {
            egsDataList.Remove(egsData);
        }

        string serialized = JsonSerializer.Serialize(egsDataList, Utils.JsoIndented);
        await File.WriteAllTextAsync($"{folder}\\egsData.json", serialized);
        // Console.WriteLine(serialized);

        // Attempt to match EGS songs with VNDB songs
        var egsImporterInnerResults = new ConcurrentBag<EgsImporterInnerResult>();
        await Parallel.ForEachAsync(egsDataList, async (egsData, _) =>
        {
            var innerResult = new EgsImporterInnerResult { EgsData = egsData };
            egsImporterInnerResults.Add(innerResult);

            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
            {
                var queryMusic = connection
                    .QueryBuilder($@"SELECT m.id
FROM music m
LEFT JOIN music_source_music msm ON msm.music_id = m.id
LEFT JOIN music_source ms ON ms.id = msm.music_source_id
LEFT JOIN music_source_external_link msel ON msel.music_source_id = ms.id
LEFT JOIN artist_music am ON am.music_id = m.id
LEFT JOIN artist_alias aa ON aa.id = am.artist_alias_id
LEFT JOIN artist a ON a.id = aa.artist_id
/**where**/");

                // egs stores names without spaces, vndb stores them with spaces; brute-force conversion
                var aIds = await DbManager.FindArtistIdsByArtistNames(egsData.CreaterNames);

                if (!aIds.Any())
                {
                    if (CreaterNameOverrideDict.ContainsKey(egsData.CreaterId))
                    {
                        Console.WriteLine(
                            $"ERROR: invalid creater name override for {egsData.CreaterId}{JsonSerializer.Serialize(egsData.CreaterNames, Utils.Jso)}");
                    }

                    innerResult.ResultKind = EgsImporterInnerResultKind.NoAids;
                    return;
                }

                innerResult.aIds = aIds.ToList();

                queryMusic.Where($"msel.url={egsData.GameVndbUrl}");
                queryMusic.Where($"msm.type={(int)egsData.GameMusicCategory}");
                queryMusic.Where($"a.id = ANY({aIds.ToList()})");

                var mIds = (await queryMusic.QueryAsync<int?>()).ToList();
                if (mIds.Count > 1)
                {
                    innerResult.ResultKind = EgsImporterInnerResultKind.MultipleMids;
                    foreach (int? mid1 in mIds)
                    {
                        if (mid1 != null)
                        {
                            innerResult.mIds.Add(mid1.Value);
                        }
                    }

                    // todo try automatically romanizing the title somehow and matching by it
                    return;
                }

                var mid = mIds.SingleOrDefault();
                if (mid != null)
                {
                    innerResult.ResultKind = EgsImporterInnerResultKind.Matched;
                    innerResult.mIds.Add(mid.Value);
                }
                else
                {
                    innerResult.ResultKind = EgsImporterInnerResultKind.NoMids;
                    return;
                }
            }
        });

        foreach (EgsImporterInnerResult egsImporterInnerResult in egsImporterInnerResults)
        {
            bool needsPrint;
            switch (egsImporterInnerResult.ResultKind)
            {
                case EgsImporterInnerResultKind.Matched:
                    // Console.WriteLine("matched: ");
                    needsPrint = false;
                    break;
                case EgsImporterInnerResultKind.MultipleMids:
                    Console.WriteLine("multiple music matches: ");
                    needsPrint = true;
                    break;
                case EgsImporterInnerResultKind.NoMids:
                    Console.WriteLine("no music matches: ");
                    needsPrint = true;
                    break;
                case EgsImporterInnerResultKind.NoAids:
                    Console.WriteLine("no artist id matches: ");
                    needsPrint = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (needsPrint)
            {
                Console.WriteLine(JsonSerializer.Serialize(egsImporterInnerResult.EgsData.MusicName, Utils.Jso));
                Console.WriteLine(egsImporterInnerResult.EgsData.CreaterId +
                                  JsonSerializer.Serialize(egsImporterInnerResult.EgsData.CreaterNames, Utils.Jso));
                Console.WriteLine(JsonSerializer.Serialize(egsImporterInnerResult.EgsData.GameVndbUrl,
                    Utils.Jso));
                Console.WriteLine(JsonSerializer.Serialize((int)egsImporterInnerResult.EgsData.GameMusicCategory,
                    Utils.Jso));
                Console.WriteLine("--------------");
            }
        }

        Console.WriteLine("total count: " + egsDataList.Count);
        Console.WriteLine("matchedMids count: " +
                          egsImporterInnerResults.Count(x => x.ResultKind == EgsImporterInnerResultKind.Matched));
        Console.WriteLine("multipleMids count: " +
                          egsImporterInnerResults.Count(x => x.ResultKind == EgsImporterInnerResultKind.MultipleMids));
        Console.WriteLine("noMids count: " +
                          egsImporterInnerResults.Count(x => x.ResultKind == EgsImporterInnerResultKind.NoMids));
        Console.WriteLine("noAids count: " +
                          egsImporterInnerResults.Count(x => x.ResultKind == EgsImporterInnerResultKind.NoAids));

        await File.WriteAllTextAsync($"{folder}\\noMids.json",
            JsonSerializer.Serialize(
                egsImporterInnerResults.Where(x => x.ResultKind == EgsImporterInnerResultKind.NoMids),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{folder}\\noMids_noins.json",
            JsonSerializer.Serialize(
                egsImporterInnerResults.Where(x =>
                    x.ResultKind == EgsImporterInnerResultKind.NoMids &&
                    x.EgsData.GameMusicCategory != SongSourceSongType.Insert),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{folder}\\noAids.json",
            JsonSerializer.Serialize(
                egsImporterInnerResults.Where(x => x.ResultKind == EgsImporterInnerResultKind.NoAids),
                Utils.JsoIndented));

        await File.WriteAllTextAsync($"{folder}\\noAids_noins.json",
            JsonSerializer.Serialize(
                egsImporterInnerResults.Where(x =>
                    x.ResultKind == EgsImporterInnerResultKind.NoAids &&
                    x.EgsData.GameMusicCategory != SongSourceSongType.Insert),
                Utils.JsoIndented));

        // Insert the matched results
        var matchedResults = egsImporterInnerResults.Where(x => x.ResultKind == EgsImporterInnerResultKind.Matched);
        await Parallel.ForEachAsync(matchedResults, async (innerResult, _) =>
        {
            await using (var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString()))
            {
                // todo? don't insert if latin title is the same after normalization
                await connection.ExecuteAsync(
                    "UPDATE music_title mt SET non_latin_title = @mtNonLatinTitle WHERE mt.music_id = @mId AND mt.language='ja' AND mt.is_main_title=true",
                    new { mtNonLatinTitle = innerResult.EgsData.MusicName, mId = innerResult.mIds.Single() });

                // todo? insert egs music & game links
            }
        });
    }
}

public class EgsImporterInnerResult
{
    public EgsData EgsData { get; set; }

    public List<int> aIds { get; set; } = new();

    public List<int> mIds { get; set; } = new();

    public EgsImporterInnerResultKind ResultKind { get; set; }
}

public enum EgsImporterInnerResultKind
{
    NoAids,
    MultipleMids,
    NoMids,
    Matched,
}
