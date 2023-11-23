using System.Collections.Generic;

namespace EMQ.Server.Db.Imports;

// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
public static class Blacklists
{
    public static List<(string, string)> VndbImporterExistingSongBlacklist { get; } = new()
    {
        ("v236", "POWDER SNOW"),
        ("v12984", "Yuki no Elfin Lied"),
        ("v21901", "Ohime-sama datte XXX Shitai!!"),
        ("v17823", "Houkago Amazing Kiss"),
        ("v3", "Unmei -SADAME-"),
        ("v20", "Heart to Heart"),
        ("v238", "Until"),
        ("v273", "Eien no Negai"),
        ("v273", "Futari Dake no Ongakkai"),
        ("v273", "White Season"),
        ("v318", "Sow"),
        ("v418", "ROSE! ROSE! ROSE!"),
        ("v434", "GRIND"),
        ("v1899", "Futari"),
        ("v1950", "Tomodachi Ijou Koibito Miman"), // todo 1x1 + 2x1
        ("v2438", "Blue Twilight ~Taiyou to Tsuki ga Deau Toki~"),
        ("v2501", "Hide and seek"),
        ("v9409", "Key of Destiny"),
        // ("v10632", "Ageless Love"), // todo 2x1 + 2x1 + 2x1
        ("v11000", "Photograph Memory"),
        ("v14005", "Onaji Hohaba de, Zutto"),
        ("v15395", "Chaleur"),
        ("v15641", "Want more need less"),
        ("v31740", "Journey"),
    };

    public static List<string> EgsImporterBlacklist { get; } = new()
    {
        "https://vndb.org/v12984",
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
    };

    public static List<string> MusicBrainzImporterReleaseBlacklist { get; } = new()
    {
        "41c0eb47-95f4-409d-8f74-bbb85e376838", // AMBITIOUS MISSION スペシャル サウンドトラック
        "5d51da54-f097-4f94-9271-83fab6cf4ba1", // カタハネ オリジナルサウンドトラックアルバム
        "8ab6e56a-27fc-4b9e-a234-5846e6f9c26d", // ものべの スペシャルサウンドトラック
        "55b580db-3b76-447a-8478-7a9aeafd84ba", // アルカテイル
        "d34ee06f-7afd-4578-b7d1-9658de23d0e2", // アナタヲユルサナイ Mini Sound Track
        "a80e7b99-e770-4f56-b2e7-1b5d3a4e8b9b", // GYAKUTEN SAIBAN 4 MINI SOUNDTRACK CD
        "21e05ca1-9b77-4c87-9cc9-5f5340152a1b", // Remember11 Prophecy Collection Vol.1
        "9652010c-9beb-4fe0-b420-d16a15531952", // Remember11 Prophecy Collection Vol.2
        "4c647eba-f1b8-4a62-8cbd-f46cd38aa0c9", // Remember11 Prophecy Collection Vol.3
        "80bd5b53-b551-4c59-ae77-2246c6dfe23f", // Remember11 Prophecy Collection Vol.4
        "11d06123-b553-4d50-961b-d1e9b93c016e", // Remember11 Prophecy Collection Vol.5
        "1dd04b74-239d-47cc-ae5e-27c50187d2dc", // Remember11 Prophecy Collection Vol.6
        "db38f619-f4d3-4fad-9bde-e16da1e8cf3a", // 朱-Aka- ORIGINAL SOUND TRACKS
        "e812c10f-6d95-4d26-acb2-6c1889af22ef", // Steins;Gate Symphonic Material
        "644aeaa9-13c3-4b7f-98de-410d614b12a3", // クドわふたー Original SoundTrack
        "a0041957-5b1d-40a7-a23b-d0acd23eff2d", // グリザイアの果実 オリジナルサウンドトラック
        "2936a79f-a50b-47ff-a667-3dabb1f4c417", // STEINS;GATE 0 O.S.T 「GATE OF STEINER」
        "fa184c8b-83e7-4db4-9c35-d21fb68e5ec2", // Fate/stay night EMIYA #0 and Out Tracks
        "cafc1cff-ce91-4d28-beb1-b59dd20033d9", // Fate/hollow ataraxia THE BROAD BRIDGE RELIVE and OUT TRACKS
        "5c297b94-1479-424f-be12-e43ca990963f", // Ever17 ~the out of infinity~ Vocal Collection
        "f64c4a30-ece6-46f7-9acd-4fe6d38ead62", // Phantom -PHANTOM OF INFERNO- ORIGINAL SOUNDTRACK
        "d158f5d0-80c7-444c-ac44-3b5618606b46", // 家族計画 サウンドコレクション
        "f990445f-a373-448f-9b46-d3201d7311c0", // Comic Party ORIGINAL SOUND TRACK
        "7083fc5e-5182-4b76-8ded-a8b94c96d876", // 12Riven-the Ψcliminal of integral- Arrange BGM
        "8595cf7a-2713-4ccc-8d73-d595dce6f68b", // Destination for Arcie
        "19e9e324-e700-4c96-9a6c-cf958fb061e2", // Memories Off 2nd ミニアルバムコレクション Vol.1 遠いこの空から
        "b91d21df-4e03-4e85-8c8d-d4e200b4f772", // Root Double -Before Crime * After Days- Xtend Edition Original Soundtrack
        "e0d3abed-2bf5-4d45-8442-a45bb9cdcb7f", // はつゆきさくら Special Soundtrack
        "c7c251bb-fa1e-4f2c-840f-8ae9a93a7501", // フェイト/エクストラ CCC オリジナル・サウンドトラック
        "7716da14-d544-49e5-b651-35fb3d44bae3", // ココロ@ファンクション! ディレクターチョイス サウンドトラックCD
        "c957aff5-f1b7-464d-a8e2-1ea36dd94f41", // ヤキモチ☆ストリーム
        "76a0e96e-1ff4-4a0e-a0a1-6ab147274f22", // 花咲ワークスプリング! SPECIAL SOUND TRACK
        "3d3e0311-3758-4555-a944-afeb63e8babb", // 恋×シンアイ彼女 Original Sound Tracks
        "0957674f-3a6d-4e34-a525-ddc3e079d101", // フローラル・フローラブ SPECIAL SOUND TRACK
        "79e4d22c-1feb-4466-ad64-8574530e4838", // かけぬけ★青春スパーキング! SPECIAL SOUNDTRACK
        "0c359bd3-7886-4eb8-85a1-13a5b5062dd6", // EVER AFTER ~MUSIC FROM "TSUKIHIME"~
        "96653862-0000-4e28-b654-487e54cc54d5", // 「逆転検事」 オーケストラミニアルバム ～逆転の旋律～
        "c1addd9f-6ad4-441f-b487-2e57e865eb74", // 『レイトン教授VS逆転裁判』特製アニメフィルム付き オリジナルサウンドトラック
        "ef0cb613-9d50-48d3-ad16-e7a96e14226b", // Dreamin'
        "42bf289a-cf30-4cde-9c5a-014cbabd630e", // AI: THE SOMNIUM FILES - nirvanA Initiative Soundtrack HARMOniOUS DISCORD
        "4f2a033a-6f05-4804-9c95-fd1a345bd232", // THE HOUSE IN FATA MORGANA ORIGINAL SOUND TRACKS 1&2
        "ab6e477c-4d44-43f1-b6f7-7ec87293afa9", // 11eyes -罪と罰と贖いの少女- オフィシャル通販用 Sound Track
        "afe56ae2-f282-4706-b5c3-0bface8892a7", // Fate/stay night イメージアルバム「Wish」
        "b11fa299-c1cd-4fc4-b71b-f48825daae5f", // Wind -a Breath of Heart- ~Songs~
        "ddcd8d70-3f93-4759-9169-f98bc76702dc", // 水夏～SUIKA～パーフェクトアレンジアルバム
        "e79dff21-f427-4769-85dd-72257fb4f16f", // 3days -満ちてゆく刻の彼方で- パーフェクトアレンジアルバム
        "e03b4e08-fed2-423d-ad14-45d46b868ceb", // 恋がさくころ桜どき キャラクターソングアルバム
        "5e17fbfe-f797-4f2c-8906-ce5d1c1bc984", // ALIA's CARNIVAL! ORIGINAL SOUNDTRACK COLLECTION
        "3918ca2b-a2af-47e3-80eb-67cc652ffc57", // 蒼の彼方のフォーリズム 特典CD
        "5ad91c44-1632-4934-94f9-29dd944d4dd1", // 逆転検事2 オーケストラ・アレンジ楽曲集～奏でられし逆転～
        "a08f1e34-de74-4f86-9e5d-b65c541fa820", // 北へ。~Diamond Dust~Memorial Songs
        "82bfdd22-5ef9-4a80-9b5c-eef5c3156082", // Observer ～Witch who lives～ forうみねこのなく頃に
        "e2c7486a-75ae-4f23-8637-e0fea9673a3f", // うみねこのなく頃に musicbox Red
        "260b06af-3b7e-45e9-89eb-ded9e1fc249c", // サウンド・オブ・ザ・ゴールデンウィッチ
        "70ea019c-6b05-453f-b529-1333e38886fb", // ギャザード ウィル ～ うみねこのなく頃に Special Tracks
        "55ea29c3-52f8-4cde-be66-491c37c115dd", // うみねこのなく頃に xaki 作品集 xwerk
        "4b3a2869-ed9d-4876-812c-7bada069492b", // Lostwing - うみねこのなく頃に image songs
        "92d6a2f0-35dc-4a13-9c97-0ccc48579ed7", // うみねこのなく頃に 散 musicbox Red
        "0d40be62-bcad-41ff-bce0-4143feb50c51", // うみねこのなく頃に散 musicbox -霧のピトス-
    };
}
