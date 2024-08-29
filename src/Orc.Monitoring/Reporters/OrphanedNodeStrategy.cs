namespace Orc.Monitoring.Reporters;

public enum OrphanedNodeStrategy
{
    RemoveOrphans, // should remove orphaned nodes recursively
    AttachToRoot,
    AttachToNearestAncestor
}
