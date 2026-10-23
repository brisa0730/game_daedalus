namespace Orcapafea.Daedalus.Sim
{
    /// <summary>What is directly under a box's bottom: nothing, at least one solid cell, or one-way cells only.</summary>
    public enum SupportKind
    {
        None,
        Solid,
        OneWayOnly,
    }
}
