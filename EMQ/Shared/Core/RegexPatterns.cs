using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Core;

public static class RegexPatterns
{
    public const string CatboxRegex = @"^https:\/\/files\.catbox\.moe\/[a-zA-Z0-9]+\.(ogg|mp3|mp4|webm)$";

    public const string VndbIdRegex = @"^u[0-9]{1,99}$";

    // do not start allowing @ in usernames without modifying usernameOrEmail code
    public const string UsernameRegex = @"^[a-zA-Z0-9_-]{2,16}$";

    public static readonly Regex UsernameRegexCompiled =
        new(UsernameRegex, RegexOptions.Compiled, TimeSpan.FromMilliseconds(420));

    public const string EmailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    public static readonly Regex EmailRegexCompiled =
        new(EmailRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(690));

    public static readonly Regex AnisonJavascriptLinkRegex =
        new(@"^javascript:link\('(.+)','([0-9]+)'\)$", RegexOptions.Compiled);

    public static readonly Regex EmojiRegex = new(@":(.+?):", RegexOptions.Compiled);

    public static readonly Regex HairRegex =
        new(@"([0-9]+)\. ([0-9]+)\.jpg \(Distance: (.+?)\)", RegexOptions.Compiled);

    // https://github.com/rampaa/JL/blob/398f65097203517293c001eb052877a88bd42b5e/JL.Core/Utilities/JapaneseUtils.cs#L12
    public static readonly Regex JapaneseRegex = new(
        @"[\u00D7\u2000-\u206F\u25A0-\u25FF\u2E80-\u2FDF\u2FF0-\u30FF\u3190-\u319F\u31C0-\u31FF\u3220-\u325F\u3280-\u4DBF\u4E00-\u9FFF\uF900-\uFAFF\uFE30-\uFE4F\uFF00-\uFF9F\uFFE0-\uFFEF]|\uD82C[\uDC00-\uDD6F]|\uD83C[\uDE00-\uDEFF]|\uD840[\uDC00-\uDFFF]|[\uD841-\uD868][\uDC00-\uDFFF]|\uD869[\uDC00-\uDEDF]|\uD869[\uDF00-\uDFFF]|[\uD86A-\uD87A][\uDC00-\uDFFF]|\uD87B[\uDC00-\uDE5F]|\uD87E[\uDC00-\uDE1F]|\uD880[\uDC00-\uDFFF]|[\uD881-\uD887][\uDC00-\uDFFF]|\uD888[\uDC00-\uDFAF]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly Regex ColorHexStringRegex = new(@"^#(?:[0-9a-fA-F]{3,4}){1,2}$", RegexOptions.Compiled);

    public static readonly Dictionary<string, string> AutocompleteStringReplacements = new()
    {
        // only 1 appearance in the DB: Δ, κ, ν, ο, π, Σ, ψ, χ
        { " ", "" },
        { "　", "" },
        { "√", "root" },
        { "α", "a" },
        // { "β", "B" },
        { "βίος", "Bios" },
        { "Rë∀˥", "Real" },
        { "Λ", "A" },
        { "∀", "A" },
        { "μ", "myu" },
        { "φ", "o" },
        { "Ω", "" },
        { "Я", "R" },
        { "×", "x" },
        { "∃", "E" },
        { "γ", "y" },
    };

    public static readonly Dictionary<SongArtistLinkType, Regex> ArtistLinkRegex = new()
    {
        { SongArtistLinkType.VNDBStaff, new Regex(@"^https://vndb\.org/s[0-9]+/$") },
        { SongArtistLinkType.MusicBrainzArtist, new Regex(@"^https://musicbrainz\.org/artist/[a-f0-9-]{36}/$") },
        { SongArtistLinkType.VGMdbArtist, new Regex(@"^https://vgmdb\.net/artist/[0-9]+/$") },
        {
            SongArtistLinkType.ErogameScapeCreater,
            new Regex(@"^https://erogamescape\.dyndns\.org/~ap2/ero/toukei_kaiseki/creater\.php\?creater=[0-9]+/$")
        },
        { SongArtistLinkType.AnisonInfoPerson, new Regex(@"^http://anison\.info/data/person/[0-9]+\.html$") },
        { SongArtistLinkType.WikidataItem, new Regex(@"^https://www\.wikidata\.org/wiki/Q[0-9]+/$") },
        { SongArtistLinkType.AniDBCreator, new Regex(@"^https://anidb\.net/creator/[0-9]+/$") },
    };

    public static readonly Dictionary<SongLinkType, Regex> SongLinkRegex = new()
    {
        { SongLinkType.MusicBrainzRecording, new Regex(@"^https://musicbrainz\.org/recording/[a-f0-9-]{36}/$") },
        {
            SongLinkType.ErogameScapeMusic,
            new Regex(@"^https://erogamescape\.dyndns\.org/~ap2/ero/toukei_kaiseki/music\.php\?music=[0-9]+/$")
        },
        { SongLinkType.AnisonInfoSong, new Regex(@"^http://anison\.info/data/song/[0-9]+\.html$") },
        { SongLinkType.WikidataItem, new Regex(@"^https://www\.wikidata\.org/wiki/Q[0-9]+/$") },
        { SongLinkType.AniDBSong, new Regex(@"^https://anidb\.net/song/[0-9]+/$") },
    };

    public static readonly Dictionary<SongSourceLinkType, Regex> SourceLinkRegex = new()
    {
        { SongSourceLinkType.VNDB, new Regex(@"^https://vndb\.org/v[0-9]+/$") },
        { SongSourceLinkType.ErogamescapeGame, new Regex(@"^https://erogamescape\.dyndns\.org/~ap2/ero/toukei_kaiseki/game\.php\?game=[0-9]+/$") },
        { SongSourceLinkType.MyAnimeListAnime, new Regex(@"^https://myanimelist\.net/anime/[0-9]+/$") },
        { SongSourceLinkType.AniListAnime, new Regex(@"^https://anilist\.co/anime/[0-9]+/$") },
        { SongSourceLinkType.AniDBAnime, new Regex(@"^https://anidb\.net/anime/[0-9]+/$") },
        { SongSourceLinkType.WikidataItem, new Regex(@"^https://www\.wikidata\.org/wiki/Q[0-9]+/$") },
    };
}
