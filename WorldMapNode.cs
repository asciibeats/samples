using Carlosfritz;
using Carlosfritz.Maps;
using UnityEngine;

namespace Creek
{
  public class WorldMapNode : RingMapNode<WorldMap, WorldMapNode, WorldMapDataSpec, WorldMapData, WorldMapTile, WorldMapTileVariant, WorldMapCost>
  {
    public bool IsWater { get { return Data.Terrain < 2; } }

    public WorldMapNode(Vector2Int position, WorldMapData data) : base(position, data) {}

    public ulong CreatePatternMask()
    {
      var pattern = 0ul;

      foreach (var child in _adjacents)
      {
        pattern |= ((ulong)child.Value.Data.Terrain).RotateLeft((int)child.Key * 8);
      }

      return pattern;
    }

    public override string ToString()
    {
      return $"WorldMapNode {{ Position: {Position}, Data: {Data} }}";
    }
  }
}