namespace Maroik.Core.Domain.FileManagers;

/// <summary>
/// A user's whole drive held in memory, for the questions that need more than one node: a folder's ancestry (a move's cycle check,
/// S5, and the breadcrumb, U4), the item count each folder shows (U3), a node's location path (search results, U5) and everything
/// under a folder (a folder delete or download, F6 / F8). A node of another account is simply not in the tree.
/// </summary>
public sealed class FileManagerTree
{
    /// <summary>Every node, by id.</summary>
    private readonly Dictionary<long, FileManagerNode> _byId;

    /// <summary>The children of each folder (key 0 stands for the root, which has no id).</summary>
    private readonly ILookup<long, FileManagerNode> _children;

    /// <summary>Builds the tree from all of one account's nodes.</summary>
    public FileManagerTree(IEnumerable<FileManagerNode> nodes)
    {
        _byId = nodes.ToDictionary(n => n.Id);
        _children = _byId.Values.ToLookup(n => n.ParentId ?? 0);
    }

    /// <summary>Every node of the drive.</summary>
    public IReadOnlyCollection<FileManagerNode> Nodes => _byId.Values;

    /// <summary>Whether the node is in this drive.</summary>
    public bool Contains(long nodeId) => _byId.ContainsKey(nodeId);

    /// <summary>The node with this id, or null when it is not in this drive.</summary>
    public FileManagerNode? Find(long nodeId) => _byId.GetValueOrDefault(nodeId);

    /// <summary>The direct children of <paramref name="folderId"/> (null = the root).</summary>
    public IEnumerable<FileManagerNode> Children(long? folderId) => _children[folderId ?? 0];

    /// <summary>The folder itself and then each folder above it up to the root; empty for the root.</summary>
    public IReadOnlyList<long> Ancestry(long? folderId)
    {
        List<long> ancestry = [];
        for (long? id = folderId; id is { } current && _byId.TryGetValue(current, out FileManagerNode? node); id = node.ParentId)
        {
            // A cycle cannot be stored (moves are checked), but stop rather than loop forever on corrupt data.
            if (ancestry.Contains(current)) break;
            ancestry.Add(current);
        }
        return ancestry;
    }

    /// <summary>The number of finished items directly in <paramref name="folderId"/> (null = the root); unfinished uploads are not counted.</summary>
    public int ItemCount(long? folderId) => Children(folderId).Count(n => !n.Uploading);

    /// <summary>The names of the folders above the node, from the root down.</summary>
    public IReadOnlyList<string> Location(long nodeId) =>
        _byId.TryGetValue(nodeId, out FileManagerNode? node)
            ? [.. Ancestry(node.ParentId).Reverse().Select(id => _byId[id].Name)]
            : [];

    /// <summary>Every node under <paramref name="folderId"/>, at any depth.</summary>
    public IReadOnlyList<FileManagerNode> Descendants(long folderId)
    {
        List<FileManagerNode> found = [];
        Stack<long> pending = new([folderId]);
        while (pending.TryPop(out long id))
        {
            foreach (FileManagerNode child in _children[id].Where(c => !found.Contains(c)))
            {
                found.Add(child);
                if (child.IsFolder) pending.Push(child.Id);
            }
        }
        return found;
    }
}
