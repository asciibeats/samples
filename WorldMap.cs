using System;
using System.Collections.Generic;
using Carlosfritz;
using Carlosfritz.Maps;
using Creek.Data;
using Creek.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Creek
{
  public class WorldMap : RingMap<WorldMap, WorldMapNode, WorldMapDataSpec, WorldMapData, WorldMapTile, WorldMapTileVariant, WorldMapCost>
  {
    public PrefabPool<Crumb, CrumbVariant> crumbPool;
    public PrefabPool<Marker, MarkerVariant> markerPool;
    public PrefabPool<Entity, EntityVariant> entityPool;
    public PrefabPool<Drop, DropVariant> dropPool;
    public PrefabPool<TextBox> textBoxPool;
    [Range(0f, 0.5f)]
    public float markDuration = 0.05f;
    public UnityEvent<int> Moved;
    public UnityEvent<IEnumerable<int>> Executed;

    WorldMapNode _pathOrigin;
    WorldMapNode _pathDestination;
    Path<WorldMap, WorldMapNode, WorldMapDataSpec, WorldMapData, WorldMapTile, WorldMapTileVariant, WorldMapCost> _path;
    CompositeTile _levelRoot;

    public void Reveal(TileData tile)
    {
      var gridPosition = new Vector2Int(tile.Position % _gridSize.x, tile.Position / _gridSize.y);
      Reveal(gridPosition, new WorldMapData(tile, dataSpecs[tile.Terrain]));
    }

    public void Reveal(List<TileData> tiles)
    {
      foreach (var tile in tiles)
      {
        Reveal(tile);
      }
    }

    public int Compare(float x, float y)
    {
      var result = x.CompareTo(y);
      return result == 0 ? 1 : result;
    }

    public void DeriveDecline()
    {
      var sorted = new SortedList<float, WorldMapNode>(Comparer<float>.Create((x, y) => Compare(y, x)));

      foreach (var node in _nodes.Values)
      {
        var outgoing = 8u;
        var minHeight = float.MaxValue;

        for (uint crumb = 0; crumb < 8; crumb += 2)
        {
          if (node.TryGetAdjacent(crumb, out var adjacent))
          {
            if (adjacent.Data.Height < minHeight)
            {
              minHeight = adjacent.Data.Height;
              outgoing = crumb;
            }
          }
        }

        var decline = minHeight - node.Data.Height;

        node.Data.SetOutgoingFlow(outgoing, decline);
        sorted.Add(node.Data.Height, node);
      }

      foreach (var node in sorted.Values)
      {
        var outgoing = node.Data.Flow.Value.Item2;

        if (outgoing < 8)
        {
          var amount = 1f;

          foreach (var incoming in node.Data.Flow.Value.Item1)
          {
            amount += incoming.Item2;
          }

          if (node.TryGetAdjacent(outgoing, out var adjacent))
          {
            //if (adjacent.Data.Terrain > 1)
            //{
            adjacent.Data.AddIncomingFlow((outgoing + 4) % 8, amount);
            //}
          }
          else
          {
            Debug.LogWarning("Should never happen");
          }
        }
        else
        {
          Debug.LogWarning("Should also never happen!?");
        }
      }

      // create shallow water where amount of flow water is high enough
      /*foreach (var node in sorted.Values)
      {
        var amount = 1f;

        foreach (var incoming in node.Data.Flow.Value.Item1)
        {
          amount += incoming.Item2;
        }

        if (amount > 16)
        {
          Debug.Log("aosidjfoaijsdfoij");
          Debug.Log(node.Data.Flow);
          Swap(node, 0);
          Debug.Log(node.Data.Flow);
        }
      }*/

      // intermediate pass to create shallow water, beaches (cellular automata?)
      // intermediate pass to create forests (cellular automata?)

      var inlets = new List<WorldMapNode>();

      // place cities at lake and sea inlets
      foreach (var node in sorted.Values)
      {
        if (node.IsWater)
        {
          WorldMapNode inlet = null;
          var maxIncoming = 0f;

          foreach (var incoming in node.Data.Flow.Value.Item1)
          {
            if (incoming.Item2 > maxIncoming)
            {
              if (node.TryGetAdjacent(incoming.Item1, out var adjacent) && !adjacent.IsWater)
              {
                inlet = adjacent;
                maxIncoming = incoming.Item2;// move out of scope?
              }
            }
          }

          if (inlet != null)
          {
            inlets.Add(inlet);
          }
        }
      }

      foreach (var node in inlets)
      {
        Swap(node, 5);
      }
    }

    public void Spawn(AvatarData avatar)
    {
      if (TryGetNode(GridPosition(avatar.Position), out var node))
      {
        Jump(node.Position);
        SetEntity(node, avatar.Class);

        _pathOrigin = node;
      }
      else
      {
        Debug.LogWarning("unknown node");
      }
    }

    public Dictionary<int, int> Move()
    {
      var gains = new Dictionary<int, int>();
      var random = new System.Random(42);

      foreach (var step in _path)
      {
        var drops = step.Node.Tile.Variant.drops;

        if (drops.Length > 0)
        {
          var probabilitySum = 0;

          foreach (var drop in drops)
          {
            probabilitySum += drop.probability;
          }

          var value = random.NextDouble();
          var index = -1;
          var indexValue = 0f;

          do
          {
            indexValue += (float)drops[++index].probability / probabilitySum;
          }
          while (value > indexValue);

          step.Node.Data.DropItem(index);
          gains.Increment(index);
        }
      }

      var variant = _path.Origin.Node.Data.Entity.Value;

      _pathOrigin = _path.Destination.Node;
      _pathDestination = null;

      UnsetEntity(_path.Origin.Node);
      SetEntity(_pathOrigin, variant);
      UnsetMarker(_pathOrigin);
      UnsetPath();

      return gains;
    }

    /*public void Exec()
    {
      //_pathOrigin.Tile.Variant.action;
    }*/

    public void Kill()
    {
      UnsetEntity(_pathOrigin);

      if (_pathDestination != null)
      {
        UnsetMarker(_pathDestination);
      }

      if (_path != null)
      {
        UnsetPath();
      }

      _pathOrigin = null;
      _pathDestination = null;

      ConcealAll();
      Terminate();
    }

    public void RunPatternPass(IDictionary<ulong, PatternData> patterns)
    {
      for (int y = 0; y < _gridSize.y; y++)
      {
        for (int x = 0; x < _gridSize.x; x++)
        {
          var position = new Vector2Int(x, y);

          if (TryGetNode(position, out var node))
          {
            MatchPattern(node, patterns);
          }
        }
      }
    }

    void MatchPattern(WorldMapNode node, IDictionary<ulong, PatternData> patterns)
    {
      var mask = node.CreatePatternMask();

      for (var i = 0; i < 4; i++)
      {
        mask = mask.RotateLeft(i * 16);

        if (patterns.TryGetValue(mask, out var pattern))
        {
          Debug.Log($"Found {pattern}@{node.Position}");
          Swap(node, pattern.Terrain);
          return;
        }
      }

      /*mask = mask.ReverseBytes();

      for (var i = 0; i < 4; i++)
      {
        mask = mask.RotateLeft(i * 16);

        if (patterns.TryGetValue(mask, out var pattern))
        {
          Debug.Log($"Found {pattern}@{node.Position}");
          Replace(node, pattern.Terrain);
          return;
        }
      }*/
    }

    /*pub struct Location {
      terrains: &'static [(u16, f32)]
    }

    const LOCATION_SPECS: [Location; 4] = [
      Location {
        terrains: &[(0, 1.0), (1, 0.3), (2, 0.01)]
      },
      Location {
        terrains: &[(2, 1.3), (3, 0.3)]
      },
      Location {
        terrains: &[(3, 0.3), (4, 0.3), (5, 1.0)]
      },
      Location {
        terrains: &[(3, 0.3), (4, 0.3), (5, 1.0)]
      }
    ];*/

    /*pub struct TileType {
      pub cost: u8,
      pub drops: &'static [(u16, f32)],
      pub action: &'static [u32]
    }

    pub const TILE_TYPES: [TileType; 6] = [
      TileType {
        cost: 1,
        drops: &[(1, 0.3), (2, 0.01)],
        action: &[]
      },
      TileType {
        cost: 1,
        drops: &[(1, 0.3), (2, 0.3)],
        action: &[0, 1] //enter location 1
      },
      TileType {
        cost: 1,
        drops: &[(1, 0.3), (2, 0.3)],
        action: &[]
      },
      TileType {
        cost: 1,
        drops: &[(1, 0.3), (2, 0.3)],
        action: &[0, 0] //enter location 2
      },
      TileType {
        cost: 1,
        drops: &[(1, 0.3), (2, 0.3)],
        action: &[]
      },
      TileType {
        cost: 1,
        drops: &[(1, 0.3), (2, 0.3)],
        action: &[]
      }
    ];*/

    public void OnClick(Vector2 worldPosition)
    {
      if (TryGetNode(GridPosition(worldPosition), out var node))
      //if (TryGetNode(new Vector2Int(2, 8), out var node))
      {
        //node.Data.SetText("Hallo?");
        /*var pattern = node.CreatePatternMask();
        Debug.Log(pattern.ToString("X16"));
        Debug.Log(pattern.ReverseBytes().ToString("X16"));
        return;*/

        if (node == _pathOrigin)
        {
          Executed.Invoke(node.Tile.Variant.action);
          //Exec();
        }
        else if (node == _pathDestination)
        {
          Moved.Invoke(ScalarPosition(node.Position));
          //Move();
        }
        else
        {
          var path = FindPath(_pathOrigin, node);

          if (path != null)
          {
            if (_pathDestination != null)
            {
              UnsetMarker(_pathDestination);
            }

            _pathDestination = node;

            SetMarker(_pathDestination, 0);

            if (_path != null)
            {
              UnsetPath();
            }

            SetPath(path, 0);
          }
          else
          {
            Debug.LogWarning("destination is unreachable");
          }
        }
      }
      else
      {
        Debug.LogWarning("tile not found");
      }
    }

    public void OnContext(Vector2 worldPosition)
    {
      if (TryGetNode(GridPosition(worldPosition), out var node))
      {
        Debug.Log(node);
        Debug.Log(node.Data.Flow.Value);
      }
      else
      {
        Debug.LogWarning("tile not found");
      }
    }

    void SetMarker(WorldMapNode node, int variant)
    {
      node.Data.SetMarker(variant);
    }

    void UnsetMarker(WorldMapNode node)
    {
      node.Data.UnsetMarker();
    }

    void SetEntity(WorldMapNode node, int variant)
    {
      node.Data.SetEntity(variant);
    }

    void UnsetEntity(WorldMapNode node)
    {
      node.Data.UnsetEntity();
    }

    void SetPath(Path<WorldMap, WorldMapNode, WorldMapDataSpec, WorldMapData, WorldMapTile, WorldMapTileVariant, WorldMapCost> path, int variant)
    {
      foreach (var step in path)
      {
        step.Node.Data.SetStep(new Tuple<uint, uint>(step.Incoming, step.Outgoing));
      }

      AddGroup(path.Positions);

      _path = path;
    }

    void UnsetPath()
    {
      foreach (var step in _path)
      {
        step.Node.Data.UnsetStep();
      }

      RemoveGroup(_path.Origin.Node.Position);

      _path = null;
    }
  }
}