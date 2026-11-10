namespace Orcapafea.Daedalus
{
    /// <summary>Where an Exit leads: nowhere (the win screen), to a scene, or to a scene that can't be found.</summary>
    internal readonly struct ExitLink
    {
        internal enum LinkKind
        {
            None = 0,
            Scene = 1,
            Broken = 2,
        }

        internal static readonly ExitLink None = new ExitLink(LinkKind.None, "");
        internal static readonly ExitLink Broken = new ExitLink(LinkKind.Broken, "");

        internal readonly LinkKind Kind;

        /// <summary>The scene's path, for <see cref="LinkKind.Scene"/>; otherwise empty.</summary>
        internal readonly string Path;

        ExitLink(LinkKind kind, string path)
        {
            Kind = kind;
            Path = path;
        }

        internal static ExitLink ToScene(string path)
        {
            return new ExitLink(LinkKind.Scene, path);
        }
    }
}
