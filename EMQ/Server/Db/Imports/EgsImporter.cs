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

namespace EMQ.Server.Db.Imports;

public static class EgsImporter
{
    public static readonly Dictionary<int, List<string>> createrNameOverrideDict = new()
    {
        // { 12569, new List<string> { "中山♡マミ", "中山マミ" } },
        // { 5819, new List<string> { "Rita" } },
        // { 3360, new List<string> { "榊原ゆい" } },
        // { 12359, new List<string> { "真理絵" } },
        // { 7436, new List<string> { "Riryka" } },
        // { 6048, new List<string> { "計名さや香" } },
        // { 12575, new List<string> { "Uran" } },
        // { 4471, new List<string> { "rino" } },
        // { 8355, new List<string> { "横手久美子" } },
        // { 24170, new List<string> { "綺良雪" } },
        // { 10271, new List<string> { "遊女" } },
        // { 2545, new List<string> { "ave;new" } },
        // { 14839, new List<string> { "KAKO" } },
        // { 14493, new List<string> { "YUMI", "Reset" } },
        // { 9455, new List<string> { "Arisa" } },
        // { 8110, new List<string> { "miru" } },
        // { 23176, new List<string> { "葉月" } },
        // { 4525, new List<string> { "佐藤ひろ美" } },
        // { 9081, new List<string> { "笹島かほる" } },
        // { 24768, new List<string> { "はるか" } },
        // { 4502, new List<string> { "yozuca*", "rino" } },
        // { 13108, new List<string> { "真里歌" } },
        // { 19735, new List<string> { "Ayumi." } },
        // { 12576, new List<string> { "Toyoda Serika" } },
        // { 4994, new List<string> { "YURIA" } },
        // { 2808, new List<string> { "Kiyo" } },
        // { 12649, new List<string> { "Heco" } },
        // { 12712, new List<string> { "Yuiko" } },
        // { 14487, new List<string> { "櫻川めぐ", "桜川めぐ" } },
    };

    public static async Task ImportEgsData()
    {
        string date = "2023-02-12";
        string folder = $"C:\\emq\\egs\\{date}";

        var createrNameOverrideDictFile =
            JsonSerializer.Deserialize<Dictionary<int, List<string>>>(
                await File.ReadAllTextAsync("C:\\emq\\egs\\createrNameOverrideDict.json"), Utils.JsoIndented)!;
        foreach ((int key, List<string>? value) in createrNameOverrideDictFile)
        {
            if (!createrNameOverrideDict.TryAdd(key, value))
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
                // todo
                // Console.WriteLine("Skipping row with Unknown GameMusicCategory");
                continue;
            }

            string gameVndbUrl = split[6];
            if (string.IsNullOrWhiteSpace(gameVndbUrl))
            {
                // todo
                // Console.WriteLine("Skipping row with no GameVndbUrl");
                continue;
            }

            int createrId = Convert.ToInt32(split[13]);

            List<string> createrName = new() { split[14] };
            if (createrNameOverrideDict.ContainsKey(createrId))
            {
                createrName = createrNameOverrideDict[createrId];
            }

            gameVndbUrl = gameVndbUrl.ToVndbUrl();

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

        // Dictionary<int, List<string>> createrNameOverrideDicttemp = new();
        // foreach (EgsData egsData in egsDataList)
        // {
        //     bool shouldOverride = false;
        //     for (int index = 0; index < egsData.CreaterNames.Count; index++)
        //     {
        //         string egsDataCreaterName = egsData.CreaterNames[index];
        //
        //         int indexOf = egsDataCreaterName.IndexOf('(');
        //         if (indexOf > 0)
        //         {
        //             egsData.CreaterNames[index] = egsDataCreaterName[..(indexOf)];
        //             shouldOverride = true;
        //         }
        //     }
        //
        //     if (shouldOverride)
        //     {
        //         createrNameOverrideDicttemp.TryAdd(egsData.CreaterId, egsData.CreaterNames);
        //     }
        // }
        // await File.WriteAllTextAsync("createrNameOverrideDictt.json", JsonSerializer.Serialize(createrNameOverrideDict, Utils.JsoIndented));
        // return;

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
                List<string> createrNames = egsData.CreaterNames.ToList();
                foreach (string createrName in egsData.CreaterNames)
                {
                    for (int index = 1; index < createrName.Length; index++)
                    {
                        string name = createrName;
                        name = name.Insert(index, " ");
                        createrNames.Add(name);
                    }
                }

                HashSet<int> aIds = new();
                foreach (string createrName in createrNames)
                {
                    var song = new Song
                    {
                        Artists = new List<SongArtist>
                        {
                            new() { Titles = new List<Title> { new() { LatinTitle = createrName } } }
                        }
                    };
                    var artist = (await DbManager.SelectArtist(connection, song, true)).SingleOrDefault();
                    if (artist != null)
                    {
                        aIds.Add(artist.Id);
                    }

                    song.Artists.Single().Titles.Single().LatinTitle = "";
                    song.Artists.Single().Titles.Single().NonLatinTitle = createrName;
                    var artist2 = (await DbManager.SelectArtist(connection, song, true)).SingleOrDefault();
                    if (artist2 != null)
                    {
                        aIds.Add(artist2.Id);
                    }
                }

                if (!aIds.Any())
                {
                    if (createrNameOverrideDict.ContainsKey(egsData.CreaterId))
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
            bool needsPrint = false;
            switch (egsImporterInnerResult.ResultKind)
            {
                case EgsImporterInnerResultKind.Matched:
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

                // todo? insert song lengths
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
    Matched,
    MultipleMids,
    NoMids,
    NoAids,
}
