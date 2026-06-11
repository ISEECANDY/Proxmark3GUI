using System.IO;
using Newtonsoft.Json;
using Proxmark3GUI.Models;

namespace Proxmark3GUI.Services;

/// <summary>
/// Loads and indexes the Proxmark3 command reference from a <c>commands.json</c>
/// file, providing search, category, and enumeration helpers.
/// </summary>
public sealed class CommandDatabase
{
    // ── JSON contract types (internal) ─────────────────────────────────────────
    // The expected JSON shape:
    // {
    //   "commands": {
    //     "hf mf rdbl": {
    //       "command": "hf mf rdbl",
    //       "description": "Read a MIFARE Classic block",
    //       "notes": ["offline only", "requires key"],
    //       "offline": true,
    //       "options": ["--key", "--blk"],
    //       "usage": "hf mf rdbl --blk 0 A --key fffffffffffe"
    //     },
    //     …
    //   }
    // }

    private class CommandEntryDto
    {
        [JsonProperty("command")]
        public string Command { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("notes")]
        public List<string> Notes { get; set; } = [];

        [JsonProperty("offline")]
        public bool Offline { get; set; }

        [JsonProperty("options")]
        public List<string> Options { get; set; } = [];

        [JsonProperty("usage")]
        public string Usage { get; set; } = string.Empty;
    }

    private class RootDto
    {
        [JsonProperty("commands")]
        public Dictionary<string, CommandEntryDto>? Commands { get; set; }
    }

    // ── State ──────────────────────────────────────────────────────────────────

    /// <summary>Command name → <see cref="Pm3Command"/> lookup.</summary>
    public Dictionary<string, Pm3Command> Commands { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True after at least one successful load.  Useful for guarding UI that
    /// depends on a populated database.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Path of the most recently loaded file, or <c>null</c>.</summary>
    public string? LoadedFilePath { get; private set; }

    /// <summary>All known category values (populated after load).</summary>
    public IReadOnlyList<string> Categories { get; private set; } = [];

    /// <summary>All known top-level categories derived from loaded commands.</summary>
    public IReadOnlyList<string> TopLevelCategories { get; private set; } = [];

    // ── Constructor / load ─────────────────────────────────────────────────────

    /// <summary>
    /// Creates an empty database. Call <see cref="LoadFromFile"/> or
    /// <see cref="LoadFromJson"/> to populate.
    /// </summary>
    public CommandDatabase() { }

    /// <summary>
    /// Creates and populates the database from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to commands.json.</param>
    /// <exception cref="FileNotFoundException">File does not exist.</exception>
    /// <exception cref="JsonException">File is not valid JSON.</exception>
    public CommandDatabase(string filePath) => LoadFromFile(filePath);

    /// <summary>
    /// Reads, deserialises, and indexes commands from <paramref name="filePath"/>.
    /// Clears any previously loaded data.
    /// </summary>
    public void LoadFromFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Commands file not found: {filePath}", filePath);

        var json = File.ReadAllText(filePath);
        LoadFromJson(json);
        LoadedFilePath = filePath;
    }

    /// <summary>
    /// Parses commands from raw JSON text.  Clears any previously loaded data.
    /// </summary>
    public void LoadFromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        Commands.Clear();

        var root = JsonConvert.DeserializeObject<RootDto>(json)
            ?? throw new JsonException("Root object is null.");

        if (root.Commands is null)
        {
            IsLoaded = false;
            Categories = [];
            return;
        }

        foreach (var kvp in root.Commands)
        {
            var cmd = MapToModel(kvp.Key, kvp.Value);
            Commands[cmd.CommandName] = cmd;
        }

        RebuildCategories();
        IsLoaded = Commands.Count > 0;
    }

    // ── Query helpers ───────────────────────────────────────────────────────────

    /// <summary>Returns every loaded command.</summary>
    public List<Pm3Command> GetAllCommands() => [.. Commands.Values];

    /// <summary>
    /// Returns commands whose name, description, or tags contain
    /// <paramref name="query"/> (case-insensitive).
    /// </summary>
    public List<Pm3Command> SearchCommands(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAllCommands();

        var lower = query.ToLowerInvariant();

        return Commands.Values
            .Where(c =>
                c.CommandName.ToLowerInvariant().Contains(lower) ||
                c.Description.ToLowerInvariant().Contains(lower) ||
                c.Tags.Any(t => t.ToLowerInvariant().Contains(lower)) ||
                c.Category.ToLowerInvariant().Contains(lower) ||
                c.Notes.Any(n => n.ToLowerInvariant().Contains(lower)) ||
                c.Options.Any(o => o.ToLowerInvariant().Contains(lower)))
            .ToList();
    }

    /// <summary>
    /// Returns commands in the given <paramref name="category"/>.
    /// Categories are derived from the first word(s) of the command name
    /// (e.g. "hf mf" → "hf mf", "lf" → "lf").
    /// </summary>
    public List<Pm3Command> GetByCategory(string category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return Commands.Values
            .Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Maps a JSON DTO to the domain model, inferring <see cref="Pm3Command.Category"/>
    /// from the command prefix.
    /// </summary>
    private static Pm3Command MapToModel(string key, CommandEntryDto dto)
    {
        var commandName = !string.IsNullOrWhiteSpace(dto.Command) ? dto.Command : key;
        var category = InferCategory(commandName);
        var tags = InferTags(commandName, dto);

        return new Pm3Command
        {
            CommandName = commandName,
            Description = dto.Description,
            Usage = dto.Usage,
            Notes = dto.Notes ?? [],
            Options = dto.Options ?? [],
            IsOffline = dto.Offline,
            Category = category,
            Tags = tags,
        };
    }

    /// <summary>
    /// Derives a category from the command prefix.
    /// Strategy: first two words if they form "hf …" or "lf …", else first word.
    /// </summary>
    private static string InferCategory(string commandName)
    {
        var parts = commandName.Split([' '], StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            0 => "Unknown",
            >= 2 => parts[0] switch
            {
                "hf" => $"hf {parts[1]}",
                "lf" => $"lf {parts[1]}",
                _ => parts[0],
            },
            _ => parts[0],
        };
    }

    /// <summary>
    /// Builds a searchable tag set from the command name segments plus
    /// common keyword heuristics.
    /// </summary>
    private static List<string> InferTags(string commandName, CommandEntryDto dto)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add each word of the command as a tag
        foreach (var word in commandName.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries))
        {
            tags.Add(word.ToLowerInvariant());
        }

        // Derive high-level tags from prefix
        if (commandName.StartsWith("hf"))
            tags.Add("hf");
        else if (commandName.StartsWith("lf"))
            tags.Add("lf");

        // Add meaningful words from description
        if (!string.IsNullOrEmpty(dto.Description))
        {
            foreach (var word in dto.Description.Split([' ', '-', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
            {
                var lowered = word.ToLowerInvariant();
                if (lowered.Length > 2)
                    tags.Add(lowered);
            }
        }

        // Offline flag as tag
        if (dto.Offline)
            tags.Add("offline");

        return [.. tags];
    }

    /// <summary>Rebuilds the category lists from current data.</summary>
    private void RebuildCategories()
    {
        var categorySet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var topCategorySet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cmd in Commands.Values)
        {
            categorySet.Add(cmd.Category);
            var top = cmd.Category.Split(' ')[0];
            topCategorySet.Add(top);
        }

        Categories = [.. categorySet];
        TopLevelCategories = [.. topCategorySet];
    }

    /// <summary>Returns the subcategories for the given top-level category.</summary>
    public List<string> GetSubCategories(string topLevelCategory)
    {
        ArgumentNullException.ThrowIfNull(topLevelCategory);

        return Commands.Values
            .Select(c => c.Category)
            .Where(c => c.StartsWith(topLevelCategory + " ", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Split(' ')[1])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }
}
