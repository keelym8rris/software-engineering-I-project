namespace BlackjackApp.Models;

public record PremadeAvatar(string Label, string Path);

// Premade player icons players can pick instead of uploading their own.
// To add one: put the image in wwwroot/avatars/ and add a line to the list below.
// Paths are relative to the site root. The hub only accepts paths from this list.
public static class PremadeAvatars
{
    public static readonly IReadOnlyList<PremadeAvatar> All = new List<PremadeAvatar>
    {
        new("Spade",   "avatars/spade.svg"),
        new("Heart",   "avatars/heart.svg"),
        new("Diamond", "avatars/diamond.svg"),
        new("Club",    "avatars/club.svg"),
        new("Crown",   "avatars/crown.svg"),
        new("Chip",    "avatars/chip.svg"),
    };

    public static bool IsPremade(string? path) =>
        path != null && All.Any(a => a.Path == path);
}
