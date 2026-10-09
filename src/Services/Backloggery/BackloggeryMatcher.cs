using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PSNav.Models.Backloggery;
using PSNav.Models.Entitlements;

namespace PSNav.Services.Backloggery;

public static partial class BackloggeryMatcher
{
    private const int MinimumPrefixLength = 4;

    // PS4 -> PS5 upgrade licences and trials
    private static readonly HashSet<int> ExcludedFeatureTypes = [5, 7];

    public static bool IsEligible(Entitlement entitlement)
    {
        return !string.IsNullOrWhiteSpace(entitlement.Name)
               && entitlement.Platform is BackloggeryPlatforms.Ps4 or BackloggeryPlatforms.Ps5
               && entitlement.TypeDisplayString == "Game"
               && entitlement.Membership == "NONE"
               && entitlement.IsActive != false
               && (entitlement.FeatureType is null || !ExcludedFeatureTypes.Contains(entitlement.FeatureType.Value));
    }

    public static IReadOnlyList<BackloggerySyncItem> BuildPlan(
        IEnumerable<Entitlement> entitlements,
        IReadOnlyDictionary<string, BackloggerySyncRecord> synced,
        IReadOnlyCollection<BackloggeryGame> library,
        BackloggeryCrossPlatformMode mode)
    {
        LibraryIndex index = new(library);
        List<BackloggerySyncItem> items = [];

        List<Entitlement> eligible = entitlements.Where(IsEligible).ToList();

        bool IsAdded(Entitlement entitlement) =>
            synced.TryGetValue(entitlement.EntitlementId, out BackloggerySyncRecord? record)
            && record.Status == BackloggerySyncStatus.Added;

        bool IsIgnored(string entitlementId) =>
            synced.TryGetValue(entitlementId, out BackloggerySyncRecord? record)
            && record.Status == BackloggerySyncStatus.Ignored;

        items.AddRange(CreateSyncedItems(eligible.Where(IsAdded), synced, library));

        // Ignored entitlements are matched like new ones, so the match is known if they are un-ignored
        var groups = eligible
            .Where(entitlement => !IsAdded(entitlement))
            .GroupBy(entitlement => Normalize(entitlement.Name!));

        foreach (var group in groups)
        {
            var byPlatform = group
                .GroupBy(entitlement => entitlement.Platform!)
                .ToDictionary(platformGroup => platformGroup.Key, platformGroup => platformGroup.ToList());

            if (byPlatform.Count == 2 && mode != BackloggeryCrossPlatformMode.Both)
            {
                string preferred = mode == BackloggeryCrossPlatformMode.Ps5Only
                    ? BackloggeryPlatforms.Ps5
                    : BackloggeryPlatforms.Ps4;
                string other = preferred == BackloggeryPlatforms.Ps5
                    ? BackloggeryPlatforms.Ps4
                    : BackloggeryPlatforms.Ps5;

                items.Add(CreateItem(group.Key, byPlatform, [preferred, other], false, index));
                continue;
            }

            foreach (string platform in byPlatform.Keys)
            {
                items.Add(CreateItem(group.Key,
                    new Dictionary<string, List<Entitlement>> { [platform] = byPlatform[platform] },
                    [platform], byPlatform.Count == 2, index));
            }
        }

        foreach (BackloggerySyncItem item in items.Where(item => !item.IsSynced))
        {
            if (item.AllEntitlementIds.All(IsIgnored))
                item.MarkIgnored();
        }

        return items
            .OrderByDescending(item => item.IsSelected)
            .ThenBy(item => item.IsIgnored)
            .ThenBy(item => item.IsSynced)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Platform)
            .ToList();
    }

    private static IEnumerable<BackloggerySyncItem> CreateSyncedItems(
        IEnumerable<Entitlement> entitlements,
        IReadOnlyDictionary<string, BackloggerySyncRecord> synced,
        IReadOnlyCollection<BackloggeryGame> library)
    {
        Dictionary<long, BackloggeryGame> libraryById = library
            .GroupBy(game => game.GameId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var group in entitlements.GroupBy(entitlement =>
                     synced[entitlement.EntitlementId].BackloggeryGameId?.ToString() ?? entitlement.EntitlementId))
        {
            List<Entitlement> groupEntitlements = group.ToList();
            Entitlement first = groupEntitlements[0];
            BackloggerySyncRecord record = synced[first.EntitlementId];
            string platform = record.Platform ?? first.Platform!;

            BackloggeryGame? game = record.BackloggeryGameId is not { } gameId
                ? null
                : libraryById.GetValueOrDefault(gameId)
                  ?? new BackloggeryGame(gameId, SuggestTitle(first.Name!),
                      BackloggeryPlatforms.GetPlatformId(platform) ?? 0, BackloggeryPlatforms.DigitalFormatId);

            yield return new BackloggerySyncItem
            {
                Name = first.Name!,
                Title = game?.Title ?? SuggestTitle(first.Name!),
                Image = groupEntitlements.FirstOrDefault(entitlement => entitlement.Image is not null)?.Image,
                EntitlementIds = groupEntitlements
                    .GroupBy(entitlement => entitlement.Platform!)
                    .ToDictionary(
                        platformGroup => platformGroup.Key,
                        IReadOnlyList<string> (platformGroup) => platformGroup
                            .Select(entitlement => entitlement.EntitlementId)
                            .Distinct()
                            .ToList()),
                Platform = platform,
                MatchedGame = game,
                MatchLevel = game is null ? BackloggeryMatchLevel.None : BackloggeryMatchLevel.Exact,
                IsSynced = true
            };
        }
    }

    private static BackloggerySyncItem CreateItem(
        string normalizedName,
        Dictionary<string, List<Entitlement>> byPlatform,
        IReadOnlyList<string> platforms,
        bool showPlatform,
        LibraryIndex index)
    {
        List<Entitlement> ordered = platforms.SelectMany(platform => byPlatform[platform]).ToList();
        (BackloggeryGame? match, BackloggeryMatchLevel level) = index.FindMatch(normalizedName, platforms);
        string suggestedTitle = SuggestTitle(ordered[0].Name!);

        return new BackloggerySyncItem
        {
            Name = ordered[0].Name!,
            Title = suggestedTitle,
            OriginalTitle = suggestedTitle,
            Image = ordered.FirstOrDefault(entitlement => entitlement.Image is not null)?.Image,
            EntitlementIds = platforms.ToDictionary(
                platform => platform,
                IReadOnlyList<string> (platform) => byPlatform[platform]
                    .Select(entitlement => entitlement.EntitlementId)
                    .Distinct()
                    .ToList()),
            Platform = platforms[0],
            ShowPlatform = showPlatform,
            MatchedGame = match,
            MatchLevel = level,
            IsSelected = level == BackloggeryMatchLevel.None
        };
    }

    public static string SuggestTitle(string name)
    {
        string title = name.Replace("™", "").Replace("®", "").Replace("©", "");
        return WhitespaceRegex().Replace(title, " ").Trim();
    }

    public static string Normalize(string name)
    {
        string text = name.Replace("™", "").Replace("®", "").Replace("©", "");
        text = text.Normalize(NormalizationForm.FormKD).ToLowerInvariant().Replace("&", " and ");

        StringBuilder builder = new(text.Length);

        foreach (char c in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c is '\'' or '’' or '`')
                continue;

            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        string normalized = WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
        string withoutPlatform = PlatformSuffixRegex().Replace(normalized, "").Trim();

        return withoutPlatform.Length > 0 ? withoutPlatform : normalized;
    }

    public static string NormalizeLoose(string normalizedName)
    {
        string result = normalizedName;
        string previous;

        do
        {
            previous = result;
            result = EditionSuffixRegex().Replace(result, "").Trim();
        } while (result != previous && result.Length > 0);

        return result.Length > 0 ? result : normalizedName;
    }

    private static bool IsWordPrefix(string shorter, string longer)
    {
        return shorter.Length >= MinimumPrefixLength
               && longer.Length > shorter.Length
               && longer.StartsWith(shorter, StringComparison.Ordinal)
               && longer[shorter.Length] == ' ';
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    // "PS4", "PS4 and PS5", "PlayStation 5 Version", "PlayStation4" at the end of the name
    [GeneratedRegex(@"\s+(ps[45]|playstation\s?[45])(\s+(and|ps[45]|playstation\s?[45]))*(\s+version)?$")]
    private static partial Regex PlatformSuffixRegex();

    [GeneratedRegex(
        @"(\s+(standard|digital|deluxe|gold|ultimate|complete|definitive|premium|special|collectors|launch|anniversary|day one|game of the year|goty))+(\s+edition)?$|\s+edition$")]
    private static partial Regex EditionSuffixRegex();

    private sealed class LibraryIndex
    {
        private readonly List<Entry> _digitalEntries;
        private readonly Dictionary<string, List<Entry>> _byName;
        private readonly Dictionary<string, List<Entry>> _byLooseName;

        public LibraryIndex(IEnumerable<BackloggeryGame> library)
        {
            List<Entry> entries = library
                .Select(game => (Game: game, Platform: BackloggeryPlatforms.GetPlatform(game.PlatformId)))
                .Where(pair => pair.Platform is not null)
                .Select(pair =>
                {
                    string name = Normalize(pair.Game.Title);
                    return new Entry(pair.Game, pair.Platform!, name, NormalizeLoose(name),
                        pair.Game.FormatId == BackloggeryPlatforms.DigitalFormatId);
                })
                .ToList();

            _digitalEntries = entries.Where(entry => entry.IsDigital).ToList();
            _byName = entries.GroupBy(entry => entry.Name).ToDictionary(group => group.Key, group => group.ToList());
            _byLooseName = entries.GroupBy(entry => entry.LooseName)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        // Exact: a digital copy with the same normalized name on one of the given platforms.
        // Possible: a digital copy with the same name on another platform, the same name after removing edition
        // words, or one name being a word prefix of the other; or a physical copy with the same name (with or
        // without edition words). Physical copies are never duplicates, they are only reported for review.
        // The best candidate is reported: given platforms first, then same name, then digital, then closest length.
        public (BackloggeryGame? Game, BackloggeryMatchLevel Level) FindMatch(string name,
            IReadOnlyList<string> platforms)
        {
            List<Entry> sameName = _byName.GetValueOrDefault(name) ?? [];
            Entry? exact = sameName.FirstOrDefault(entry => entry.IsDigital && platforms.Contains(entry.Platform));

            if (exact is not null)
                return (exact.Game, BackloggeryMatchLevel.Exact);

            string looseName = NormalizeLoose(name);

            Entry? possible = sameName
                .Concat(_byLooseName.GetValueOrDefault(looseName) ?? [])
                .Concat(_digitalEntries.Where(entry =>
                    IsWordPrefix(entry.Name, name) || IsWordPrefix(name, entry.Name)))
                .OrderByDescending(entry => platforms.Contains(entry.Platform))
                .ThenByDescending(entry => entry.Name == name)
                .ThenByDescending(entry => entry.IsDigital)
                .ThenBy(entry => Math.Abs(entry.Name.Length - name.Length))
                .FirstOrDefault();

            return possible is null
                ? (null, BackloggeryMatchLevel.None)
                : (possible.Game, BackloggeryMatchLevel.Possible);
        }

        private sealed record Entry(BackloggeryGame Game, string Platform, string Name, string LooseName,
            bool IsDigital);
    }
}
