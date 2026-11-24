using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ChipRoyale.Engine.Ai;
using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Economy;
using ChipRoyale.Engine.Evaluation;
using ChipRoyale.Client.Components.Tutorial;
using ChipRoyale.Client.Localization;

namespace ChipRoyale.Client.Tests;

public partial class LocalizationTests
{
    private static readonly CultureInfo De = new("de");
    private static readonly CultureInfo En = new("en");

    private static string ClientDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ChipRoyale.slnx")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "ChipRoyale.Client");
    }

    private static HashSet<string> ResxKeys(string file) =>
        XDocument.Load(Path.Combine(ClientDir(), "Resources", file))
            .Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToHashSet();

    [Fact]
    public void German_and_English_have_the_same_keys()
    {
        var de = ResxKeys("SharedResource.resx");
        var en = ResxKeys("SharedResource.en.resx");
        Assert.Empty(de.Except(en));
        Assert.Empty(en.Except(de));
    }

    [Fact]
    public void Every_key_used_in_the_code_exists()
    {
        var keys = ResxKeys("SharedResource.resx");
        var files = Directory.EnumerateFiles(ClientDir(), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor") || f.EndsWith(".cs"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains("Migrations"));

        var missing = new List<string>();
        foreach (var file in files)
            foreach (Match m in KeyLiteral().Matches(File.ReadAllText(file)))
                if (!keys.Contains(m.Groups[1].Value))
                    missing.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value}");

        Assert.Empty(missing);
    }

    [Fact]
    public void Dynamic_keys_exist_in_both_languages()
    {
        var keys = new List<string>();
        foreach (var r in Enum.GetValues<Rank>())
        {
            keys.Add($"Rank.{r}");
            keys.Add($"Rank.{r}.Plural");
            if (r >= Rank.Jack) keys.Add($"Rank.{r}.Symbol");
        }
        keys.AddRange(Enum.GetValues<HandCategory>().Select(c => $"Hand.{c}"));
        keys.AddRange(Enum.GetValues<Difficulty>().SelectMany(d => new[] { $"Difficulty.{d}", $"Difficulty.{d}.Text" }));
        keys.AddRange(TableStakes.All.Select(s => $"Stake.{s.Id}"));
        keys.AddRange(Enum.GetValues<Position>().Select(p => $"Mini.{p}"));
        keys.AddRange(ShopCatalog.All.Select(i => $"Item.{i.Id}"));
        keys.AddRange(Enum.GetValues<ItemCategory>().Select(c => $"Shop.Tab.{c}"));
        keys.AddRange(Achievements.All.SelectMany(a => new[] { $"Ach.{a.Id}.Title", $"Ach.{a.Id}.Text" }));
        keys.AddRange(ChipRoyale.Engine.Tournament.SitAndGoTiers.All.Select(t => $"Sng.Tier.{t.Id}"));
        foreach (var lesson in Lessons.All)
        {
            keys.Add($"Learn.{lesson.Id}.Title");
            keys.Add($"Learn.{lesson.Id}.Summary");
            foreach (var step in lesson.Steps)
            {
                keys.Add($"Learn.{lesson.Id}.{step.Key}");
                for (var o = 0; o < step.Options; o++)
                    keys.Add($"Learn.{lesson.Id}.{step.Key}.{o}");
                if (step.Kind != StepKind.Info && step.HasExplain)
                    keys.Add($"Learn.{lesson.Id}.{step.Key}.Explain");
            }
        }

        var missing = keys.Where(k => Loc.Lookup(k, De) is null).ToList();
        Assert.Empty(missing);
        Assert.All(keys, k => Assert.False(string.IsNullOrWhiteSpace(Loc.Lookup(k, En)), k));
    }

    [Fact]
    public void English_texts_are_actually_english()
    {
        Assert.Equal("Fold", Loc.Lookup("Table.Fold", En));
        Assert.Equal("Passen", Loc.Lookup("Table.Fold", De));
    }

    [Fact]
    public void Placeholders_match_between_languages()
    {
        var de = XDocument.Load(Path.Combine(ClientDir(), "Resources", "SharedResource.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);
        var en = XDocument.Load(Path.Combine(ClientDir(), "Resources", "SharedResource.en.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

        static string Holes(string s) => string.Join(",", Placeholder().Matches(s).Select(m => m.Groups[1].Value).Distinct().Order());
        var mismatched = de.Keys.Where(k => en.ContainsKey(k) && Holes(de[k]) != Holes(en[k])).ToList();
        Assert.Empty(mismatched);
    }

    [Fact]
    public void Every_shop_item_has_a_style()
    {
        var css = File.ReadAllText(Path.Combine(ClientDir(), "wwwroot", "css", "game.css"));
        var missing = ShopCatalog.All
            .Where(i => i.Category is not (ItemCategory.Avatar or ItemCategory.Charm) || i.Pixel)
            .Where(i => !Regex.IsMatch(css, $@"\.{Regex.Escape(i.Id)}\b"))
            .Select(i => i.Id)
            .ToList();
        Assert.Empty(missing);
    }

    // String literals that look like our resource keys, e.g. "Table.Fold" or "Guest".
    [GeneratedRegex("\"((?:Table|Lobby|Board|Learn|Hand|HandRankings|Over|Tip|Nav|Error|Common|Name|Mini|Action|Rank|Difficulty|Stake|Seat|Account|Sng|Shop|Ach|Item|Validation|IdentityError|Manage|Stats|Bj|Conn|Dev|Rl|Sl|Vp|Game|Casino|Daily|Records)\\.[A-Za-z0-9.]+|Guest|DefaultName)\"")]
    private static partial Regex KeyLiteral();

    [GeneratedRegex(@"\{(\d+)")]
    private static partial Regex Placeholder();
}
