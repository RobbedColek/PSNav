using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace PSNav.Infrastructure.Database;

public sealed record EntitlementOverride(string TitleId, string MatchType, string? PackageType, string? Type);

public static class EntitlementOverrides
{
    // Bump whenever Definitions change, so the EntitlementOverrides table gets rebuilt on next startup
    private const int Version = 1;
    private const string VersionSettingKey = "EntitlementOverrides:Version";

    // Overrides are applied only when the stored Type matches MatchType
    private static readonly IReadOnlyList<EntitlementOverride> Definitions =
    [
	    // PS4 applications such as Youtube or Spotify have their type set to `PS4GD`.
		// To make these applications show up as Media applications on Entitlements view
		// we're manually correcting this.
		//
	    // Youtube
        new("CUSA01015_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01034_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01065_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01116_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA06021_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA15375_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA18680_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Spotify
        new("CUSA01780_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Netflix
        new("CUSA00127_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00129_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01149_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01160_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA02988_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// 9Now
        new("CUSA06299_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// AnimeLab
        new("CUSA03389_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Crackle
        new("CUSA00059_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA09314_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA43433_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA43434_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// FilmBox Live
        new("CUSA00281_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Funimation
        new("CUSA01881_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA02598_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA24752_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Lightbox
        new("CUSA01693_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Pop
        new("CUSA13535_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Popcornflix
        new("CUSA01851_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Redbox Instant
        new("CUSA00145_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// SBS on Demand
        new("CUSA00686_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Shomi
        new("CUSA04223_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// SnagFilms
        new("CUSA01206_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// VidZone
        new("CUSA00235_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00237_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// VRV
        new("CUSA06551_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Watchever
        new("CUSA00075_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// UFC Fight Pass
        new("CUSA35488_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA35489_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA39356_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA40221_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA40222_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// 7plus
        new("CUSA01287_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA35181_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// ABC iview
        new("CUSA01626_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Amazon Prime Video
        new("CUSA00126_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00130_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01888_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA03099_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA03413_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Animax
        new("CUSA01115_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA02181_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Apple TV
        new("CUSA24186_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA24386_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA24387_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA24388_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// BBC iPlayer
        new("CUSA00122_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// BBC News
        new("CUSA00273_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// BBC Sport
        new("CUSA00116_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// BBC Sport VR: 2018 FIFA World Cup Russia
        new("CUSA12250_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// beIN CONNECT
        new("CUSA08612_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA08722_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// beIN Sports
        new("CUSA01258_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA05351_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Blockbuster
        new("CUSA01669_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// BT Sport
        new("CUSA14890_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Clan en Rtve.es
        new("CUSA00797_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Claro video
        new("CUSA04859_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA04945_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA05019_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA07829_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Crunchyroll
        new("CUSA00095_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA02644_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA39533_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA39534_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Dailymotion
        new("CUSA01161_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Dansk Filmskat
        new("CUSA03206_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// DAZN
        new("CUSA04225_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA04681_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA05372_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA05769_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA09505_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA12348_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA12373_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA13444_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA13445_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Disney+
        new("CUSA17211_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA17204_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA15362_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA15607_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA23459_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA41720_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA33445_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA33443_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA26917_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// DMM.com
        new("CUSA03302_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// DRTV
        new("CUSA02676_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// du View
        new("CUSA02063_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// ESPN
        new("CUSA05214_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA20270_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Sky Fan Pass
        new("CUSA06710_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Film1
        new("CUSA04481_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Filmin
        new("CUSA05126_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// francetv sport
        new("CUSA05696_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Game One
        new("CUSA00294_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Gamereactor
        new("CUSA01710_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// GoPro Channel
        new("CUSA04180_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// HBO Max
        new("CUSA05211_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA05212_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA05213_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA05215_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA05219_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA31016_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA31280_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA31344_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA46979_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// HKTV Television
        new("CUSA01980_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Hulu
        new("CUSA00399_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA07728_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA25618_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA25660_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Hulu Plus
        new("CUSA00131_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Icflix
        new("CUSA01462_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// IGN
        new("CUSA00238_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00268_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00420_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// iHeartRadio
        new("CUSA01896_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Infinity
        new("CUSA00376_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// ITVX
        new("CUSA46706_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA46902_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Jook Video
        new("CUSA00295_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Joysound.TV Plus
        new("CUSA01039_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01040_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Kayo Sports
        new("CUSA18595_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Maxdome
        new("CUSA00115_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Mediaset Infinity
        new("CUSA41147_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// MEO VideoClube
        new("CUSA00280_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Min Bio
        new("CUSA03207_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// MLB.TV
        new("CUSA00881_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01974_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// MLB.TV Premium		
        new("CUSA00529_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Movistar+		
        new("CUSA01142_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// MUBI
        new("CUSA01196_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01500_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA03459_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Multiplayer.it
        new("CUSA00454_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// myCANAL
        new("CUSA26941_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// NBA
        new("CUSA06996_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA06566_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// NHL.tv
        new("CUSA24952_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// NOS
        new("CUSA02993_00", "PS4GD", "PSMEDIA", "PSMEDIA"),		
		// NOS TV
        new("CUSA16977_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// NOS Sport
        new("CUSA00120_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Now TV
        new("CUSA07306_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01935_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA10420_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00117_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA12336_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00519_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// NRK Super
        new("CUSA01144_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// NRK TV
        new("CUSA01145_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// OCS 100% cinéma séries
        new("CUSA01916_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Okko Movies HD
        new("CUSA03794_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Paramount+
        new("CUSA27830_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Pathé Thuis
        new("CUSA01312_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Player
        new("CUSA00358_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// PlayStation Access
        new("CUSA02464_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Plex
        new("CUSA01703_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01850_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Plush
        new("CUSA01468_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Pluto TV
        new("CUSA04688_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA25431_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Qello HD Concerts
        new("CUSA00489_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00696_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Red Bull TV
        new("CUSA03460_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA08391_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA09119_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA09246_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA09418_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// RTBF
        new("CUSA00121_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA44299_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// RTVE +tdp
        new("CUSA03741_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Screambox
        new("CUSA03846_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Showmax
        new("CUSA04891_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA47296_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA47661_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA47662_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA47663_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA47321_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// SiriusXM
        new("CUSA05068_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Skai
        new("CUSA00449_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Sky Online
        new("CUSA00278_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Sportsnet
        new("CUSA11301_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Stan
        new("CUSA03580_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// StarzPlay
        new("CUSA05439_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// TV3
        new("CUSA01346_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// TV from Sky
        new("CUSA01245_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01246_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA14603_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Tubi TV
        new("CUSA08686_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Tvigle
        new("CUSA00604_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// TVNZ OnDemand
        new("CUSA02578_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Twitch
        new("CUSA03285_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA03398_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA03408_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// U-Next
        new("CUSA08077_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Viaplay
        new("CUSA00124_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01579_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA47836_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Videoland
        new("CUSA01763_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA32490_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Vudu
        new("CUSA00096_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// WeatherNation
        new("CUSA03976_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Rakuten TV
        new("CUSA00123_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA11244_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA07145_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Peacock
        new("CUSA20557_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA20558_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA20559_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA44568_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA50221_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA50222_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA50223_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// WWE Network
        new("CUSA37377_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA12199_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00882_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA01092_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00429_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// YLE Areena
        new("CUSA03061_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA44434_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// YuppTV
        new("CUSA00097_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA03244_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Vevo
        new("CUSA01692_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA02573_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Aqua TV
        new("CUSA06950_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA09672_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Bravia TV Player
        new("CUSA02106_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA03878_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA02215_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// discovery+
        new("CUSA40527_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Grada1 TV
        new("CUSA07838_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// IMDb TV
        new("CUSA25926_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA26048_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Magine TV
        new("CUSA06035_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// ROGERS ANYPLACE TV 
        new("CUSA05366_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// Tennis TV
        new("CUSA15736_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA07415_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
		// TV 2 Sumo
        new("CUSA00780_00", "PS4GD", "PSMEDIA", "PSMEDIA"),
        new("CUSA00334_00", "PS4GD", "PSMEDIA", "PSMEDIA")
    ];

    public static void Synchronize(SqliteConnection connection)
    {
        if (GetStoredVersion(connection) == Version) return;

        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM EntitlementOverrides;";
                command.ExecuteNonQuery();
            }

            foreach (EntitlementOverride entitlementOverride in Definitions)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO EntitlementOverrides (TitleID, MatchType, PackageType, Type)
                    VALUES ($titleId, $matchType, $packageType, $type);
                    """;
                command.Parameters.AddWithValue("$titleId", entitlementOverride.TitleId);
                command.Parameters.AddWithValue("$matchType", entitlementOverride.MatchType);
                command.Parameters.AddWithValue("$packageType", (object?)entitlementOverride.PackageType ?? DBNull.Value);
                command.Parameters.AddWithValue("$type", (object?)entitlementOverride.Type ?? DBNull.Value);
                command.ExecuteNonQuery();
            }

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO Settings (Key, Value, UpdatedAt)
                    VALUES ($key, $value, unixepoch())
                    ON CONFLICT (Key) DO UPDATE SET
                        Value = excluded.Value,
                        UpdatedAt = excluded.UpdatedAt;
                    """;
                command.Parameters.AddWithValue("$key", VersionSettingKey);
                command.Parameters.AddWithValue("$value", Version.ToString());
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static int? GetStoredVersion(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", VersionSettingKey);

        return int.TryParse(command.ExecuteScalar() as string, out int version) ? version : null;
    }
}
