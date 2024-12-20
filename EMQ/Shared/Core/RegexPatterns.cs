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

    public static readonly Dictionary<string, string> AutocompleteStringReplacements = new()
    {
        // only 1 appearance in the DB: Δ, κ, ν, ο, π, Σ, ψ, χ
        { " ", "" },
        { "　", "" },
        { "√", "root" },
        { "α", "a" },
        // { "β", "B" },
        { "βίος", "Bios" },
        { "Λ", "A" },
        { "∀", "A" },
        { "μ", "myu" },
        { "φ", "o" },
        { "Ω", "" },
        { "Я", "R" },
        { "×", "x" },
        { "∃", "E" },
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
}
