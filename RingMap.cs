using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Carlosfritz.Maps
{
  //TODO fixed origin (menu item create)
  //TODO optional scroll boundaries
  //TODO remove group keepalive?

  //[ExecuteAlways]
  [RequireComponent(typeof(RectTransform))]
  [DefaultExecutionOrder((int)Defines.ExecutionOrder.UI)]
  public abstract class RingMap<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> : UIBehaviour, IMap<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> where TMap : RingMap<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> where TNode : RingMapNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> where TDataSpec : RingMapDataSpec<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> where TData : RingMapData<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> where TTile : RingMapTile<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> where TTileVariant : RingMapTileVariant<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> where TCost : IMapCost<TCost>
  {
    /*public class Path : Path<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>
    {
      public Path(Vector2Int position, RingNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> node, TCost cost) : base(position, node, cost) { }
    }*/

    //public int seed;
    public Vector2 tileSize = Vector2.one;
    public Vector2 viewPadding = Vector2.one;
    public Vector2 gridOffset;
    public TDataSpec[] dataSpecs;
    public PrefabPool<TTile, TTileVariant> tilePool;

    protected Vector2 Origin { get { return -(Vector2)_origin.localPosition * _tileSizeInv; } }

    protected readonly Vector2Int[] _directions = { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1), Vector2Int.zero };
    protected readonly Vector2[] _corners = { new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, -0.5f) };

    protected RectTransform _rectTransform;
    protected Transform _origin;
    protected Vector2Int _viewOrigin;
    protected Vector2Int _viewOffset;
    protected Vector2Int _gridSize;
    protected Vector2Int _gridHalf;
    protected Vector2 _gridSizeInv;
    protected Vector2 _tileSizeInv;
    protected Vector2 _worldSize;
    protected Vector2 _worldHalf;

    public Vector2 GetWorldDirection(uint crumb)
    {
      return tileSize * _directions[crumb];
    }

    const float PI4_INV = 4f / Mathf.PI;

    protected Dictionary<Vector2Int, TNode> _nodes;
    Dictionary<Vector2Int, Group<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>> _groups;
    Vector4Int _viewRect;
    Coroutine _panRoutine;

    protected override void Awake()
    {
      _rectTransform = GetComponent<RectTransform>();
      _nodes = new Dictionary<Vector2Int, TNode>();
      _groups = new Dictionary<Vector2Int, Group<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>>();
      _viewRect = new Vector4Int(1, 1, 0, 0);
      _origin = transform.FindOrCreate("Tiles", Vector3.back).FindOrCreate("Origin", Vector3.zero);

      enabled = false;
    }

    protected override void OnEnable()
    {
      _viewOrigin = CalcViewOrgin();
      _viewRect = CalcViewRect();

      ResetView();
      EachTile(_viewRect, ShowTile);
    }

    protected override void OnDisable()
    {
      HideAll();
    }

    protected virtual void Update()
    {
      //_origin.transform.Translate(new Vector3(Time.deltaTime * 50f, Time.deltaTime * -20f, 0f));
      _viewOrigin = CalcViewOrgin();

      /*if (viewOrigin != _viewOrigin)
      {
        ShiftView();
      }*/

      var viewRect = _viewRect;
      _viewRect = CalcViewRect();

      if (viewRect != _viewRect)
      {
        EachTileExcept(viewRect, _viewRect, HideTile);
        EachTileExcept(_viewRect, viewRect, ShowTile);
      }
    }

    protected override void OnRectTransformDimensionsChange()
    {
      if (IsActive())
      {
        ResetView();
      }
    }

    public virtual void Init(int width, int height)
    {
      _gridSize = new Vector2Int(width, height);
      _gridSizeInv = _gridSize.Invert();
      _tileSizeInv = tileSize.Invert();
      _worldSize = _gridSize * tileSize;
      _gridHalf = new Vector2Int(_gridSize.x >> 1, _gridSize.y >> 1);
      _worldHalf = _gridHalf * tileSize;

      enabled = true;
    }

    public void Terminate()
    {
      enabled = false;
    }

    void ResetView()
    {
      _origin.parent.transform.localPosition = Vector3.back - (Vector3)((_rectTransform.pivot - Vector2.one * 0.5f) * _rectTransform.rect.size);
      _viewOffset = CalcViewOffset();

      Debug.Assert(_viewOffset.x < _gridHalf.x && _viewOffset.y < _gridHalf.y);
      //tileSize = _rectTransform.rect.size / gridView;

      //EachTile(_viewRect, (node, patch) => PlaceTile(node.tiles[patch], patch));
    }

    void EachTile(Vector4Int each, Action<TNode, Vector2Int> action)
    {
      var patchPosition = each.xy;

      for (; patchPosition.y <= each.w; patchPosition.y++)
      {
        for (patchPosition.x = each.x; patchPosition.x <= each.z; patchPosition.x++)
        {
          if (TryGetNode(GridPosition(patchPosition), out var node))
          {
            action(node, patchPosition);
          }
        }
      }
    }

    void EachTileExcept(Vector4Int each, Vector4Int except, Action<TNode, Vector2Int> action)
    {
      var patchPosition = each.xy;

      for (; patchPosition.y <= each.w; patchPosition.y++)
      {
        for (patchPosition.x = each.x; patchPosition.x <= each.z; patchPosition.x++)
        {
          if (!except.Contains(patchPosition) && TryGetNode(GridPosition(patchPosition), out var node))
          {
            action(node, patchPosition);
          }
        }
      }
    }

    void EachTileAt(Vector4Int each, TNode node, Action<TNode, Vector2Int> action)
    {
      var gridPosition = node.Position;
      var startPosition = new Vector2Int(gridPosition.x - ((gridPosition.x - each.x) / _gridSize.x) * _gridSize.x, gridPosition.y - ((gridPosition.y - each.y) / _gridSize.y) * _gridSize.y);
      var patchPosition = startPosition;

      for (; patchPosition.y <= each.w; patchPosition.y += _gridSize.y)
      {
        for (patchPosition.x = startPosition.x; patchPosition.x <= each.z; patchPosition.x += _gridSize.x)
        {
          action(node, patchPosition);
        }
      }
    }

    public void Reveal(Vector2Int gridPosition, TData data)
    {
      //var node = new TNode(gridPosition, data);
      var node = (TNode)Activator.CreateInstance(typeof(TNode), new object[] { gridPosition, data });

      AddNode(node);
      EachTileAt(_viewRect, node, ShowTile);
    }

    public void Reveal(Vector2Int gridPosition, int terrainIndex)
    {
      var data = Activator.CreateInstance<TData>();
      data.Load(dataSpecs[terrainIndex]);

      //var node = new TNode(gridPosition, data);
      var node = (TNode)Activator.CreateInstance(typeof(TNode), new object[] { gridPosition, data });

      AddNode(node);
      EachTileAt(_viewRect, node, ShowTile);
    }

    public void Reveal(List<uint> indices)
    {
      for (int y = 0; y < _gridSize.y; y++)
      {
        for (int x = 0; x < _gridSize.x; x++)
        {
          Reveal(new Vector2Int(x, y), (int)indices[y * _gridSize.x + x]);
        }
      }
    }

    public void Replace(Vector2Int gridPosition, int index)
    {
      Conceal(gridPosition);
      Reveal(gridPosition, index);
    }

    protected void Replace(TNode node, int index)
    {
      Conceal(node.Position);
      Reveal(node.Position, index);
    }

    protected void Swap(TNode node, int index)
    {
      node.Data.Load(dataSpecs[index]);
      EachTileAt(_viewRect, node, (TNode node, Vector2Int patchPosition) => SwapTile(node, patchPosition, index));// TODO more flexible each
    }

    void SwapTile(TNode node, Vector2Int patchPosition, int index)
    {
      if (_groups.TryGetValue(node.Position, out var group))
      {
        if (group.DecrementVisibility() && !group.KeepAlive)
        {
          foreach (var member in group.Members)
          {
            if (member.Position == node.Position)
            {
              tilePool.Swap(node.Tile, index);
              member.Tile.Refresh();
            }
          }
        }
      }
      else
      {
        tilePool.Swap(node.Tile, index);
        node.Tile.Refresh();
      }
    }

    public void Conceal(Vector2Int gridPosition)
    {
      if (TryGetNode(gridPosition, out var node))
      {
        Conceal(node);
      }
    }

    protected void Conceal(TNode node)
    {
      EachTileAt(_viewRect, node, HideTile);
      RemoveNode(node);
    }

    public void ConcealAll()
    {
      var gridPositions = new List<Vector2Int>(_nodes.Keys);

      foreach (var gridPosition in gridPositions)
      {
        Conceal(gridPosition);
      }
    }

    public void Fill(int terrain)
    {
      for (int y = 0; y < _gridSize.y; y++)
      {
        for (int x = 0; x < _gridSize.x; x++)
        {
          Reveal(new Vector2Int(x, y), terrain);
        }
      }
    }

    public bool IsVisible(Vector2Int gridPosition)
    {
      return _viewRect.Contains(PatchPosition(gridPosition, _viewRect));
    }

    /*public void RevealSeed(int position)
    {
        UnityEngine.Random.InitState(_seed ^ position);
        int type = UnityEngine.Random.Range(fillRange.x, fillRange.y + 1);
        Reveal(position, (Data)Activator.CreateInstance(typeof(Data), new object[] { type }));
    }*/

    public void Scroll(Vector2 delta)
    {
      _origin.Translate(delta);
    }

    public void Jump(Vector2 gridPosition)
    {
      _origin.localPosition = -LocalPosition(gridPosition);
    }

    public void Pan(Vector2 gridPosition, float duration, AnimationCurve curve = null)
    {
      if (_panRoutine != null)
      {
        StopCoroutine(_panRoutine);
      }

      if (curve == null)
      {
        curve = AnimationCurve.Linear(0, 0, 1, 1);
      }

      _panRoutine = StartCoroutine(PanRoutine(LocalPosition(gridPosition), duration, curve));
    }

    public void Stop()
    {
      if (_panRoutine != null)
      {
        StopCoroutine(_panRoutine);
      }
    }

    public Path<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> FindPath(TNode origin, TNode destination, bool diagonal = false, Func<TCost, TNode, TNode, TNode, uint, TCost> calcCost = null)
    {
      if (origin == destination)
      {
        return new Path<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>(origin, IMapCost<TCost>.Zero);
      }

      var open = new PriorityQueue<TNode, TCost>();
      var closed = new HashSet<TNode>();
      var flow = new Dictionary<TNode, Tuple<TNode, TCost, uint>>();
      var angle = diagonal ? 1u : 2u;

      if (calcCost == null)
      {
        calcCost = DefaultCalcCost;
      }

      open.Enqueue(origin, IMapCost<TCost>.Zero);
      flow.Add(origin, new Tuple<TNode, TCost, uint>(null, IMapCost<TCost>.Zero, 8));

      do
      {
        var current = open.Dequeue();
        closed.Add(current.Key);

        for (uint crumb = 0; crumb < 8; crumb += angle)
        {
          if (!current.Key.TryGetAdjacent(crumb, out var adjacent) || closed.Contains(adjacent))
          {
            continue;
          }

          var cost = calcCost(flow[current.Key].Item2, origin, current.Key, adjacent, crumb);

          if (cost.IsInfinity())
          {
            continue;
          }

          if (adjacent == destination)
          {
            var steps = new List<Tuple<uint, TNode>>();
            var node = current.Key;

            steps.Add(new Tuple<uint, TNode>(crumb, adjacent));

            while (node != origin)
            {
              steps.Add(new Tuple<uint, TNode>(flow[node].Item3, node));
              node = flow[node].Item1;
            }

            steps.Reverse();

            var path = new Path<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>(origin, cost);

            foreach (var step in steps)
            {
              path.Add(step.Item1, step.Item2);
            }

            return path;
          }

          if (!open.Contains(adjacent))
          {
            flow[adjacent] = new Tuple<TNode, TCost, uint>(current.Key, cost, crumb);
            open.Enqueue(adjacent, cost.Add(IMapCost<TCost>.One.Multiply(CalcMinDistance(adjacent.Position, destination.Position))));
          }
          else if (cost.CompareTo(flow[adjacent].Item2) < 0)
          {
            flow[adjacent] = new Tuple<TNode, TCost, uint>(current.Key, cost, crumb);
            open.Update(adjacent, cost.Add(IMapCost<TCost>.One.Multiply(CalcMinDistance(adjacent.Position, destination.Position))));
          }
        }
      }
      while (!open.IsEmpty);

      return null;
    }

    public Area<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> FindArea(TNode origin, TCost range, bool diagonal = false, Func<TCost, TNode, TNode, TNode, uint, TCost> calcCost = null)
    {
      var area = new Area<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>(origin.Position, origin);
      var open = new PriorityQueue<TNode, TCost>();
      var angle = diagonal ? 1u : 2u;

      if (calcCost == null)
      {
        calcCost = DefaultCalcCost;
      }

      open.Enqueue(origin, IMapCost<TCost>.Zero);

      do
      {
        var current = open.Dequeue();

        for (uint crumb = 0; crumb < 8; crumb += angle)
        {
          if (!current.Key.TryGetAdjacent(crumb, out var adjacent))
          {
            continue;
          }

          if (area.Contains(adjacent.Position))
          {
            area.RemoveBorder(adjacent.Position, (crumb + 4) % 8);
            continue;
          }

          var cost = calcCost(current.Value, origin, current.Key, adjacent, crumb);

          if (range.CompareTo(cost) < 0)
          {
            area.AddBorder(current.Key.Position, crumb);
            continue;
          }

          if (!open.Contains(adjacent))
          {
            open.Enqueue(adjacent, cost);
            area.Add(adjacent.Position, adjacent, cost);
          }
          else if (cost.CompareTo(area[adjacent.Position].cost) < 0)
          {
            open.Update(adjacent, cost);
            area.ResetCost(adjacent.Position, cost);
          }
        }
      }
      while (!open.IsEmpty);

      return area;
    }

    /*public Area<Vector2Int, RingNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>, TCost> ExpandArea(Area<Vector2Int, RingNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>, TCost> area, TCost range, bool diagonal = false, Func<RingNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>, RingNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>, RingNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>, int, TCost, TCost> calcCost = null)
    {
      var expansion = new Area<Vector2Int, RingNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>, TCost>();
      var open = new PriorityQueue<RingNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>, TCost>();

      foreach (var field in area)
      {
        if (field.Value.border != 0)
        {
          open.Enqueue(field.Value.data, new TCost());
        }
      }

      int angle = diagonal ? 1 : 2;

      if (calcCost == null)
      {
        calcCost = DefaultCost;
      }

      do
      {
        var current = open.Dequeue();

        for (int crumb = 0; crumb < 8; crumb += angle)
        {
          if (!TryGetAdjacent(current.Key.position, crumb, out var adjacent))
          {
            continue;
          }

          if (area.Contains(adjacent.position))
          {
            continue;
          }

          var cost = calcCost(area.Origin, current.Key, adjacent, crumb);

          if (expansion.TryGetValue(current.Key.position, out var field))
          {
            cost.Add(field.cost);
          }

          if (cost.CompareTo(range) > 0)
          {
            if (expansion.Contains(current.Key.position))
            {
              expansion.AddBorder(current.Key.position, crumb);
            }

            continue;
          }

          if (!open.Contains(adjacent))
          {
            open.Enqueue(adjacent, cost);
            expansion.Add(adjacent.position, adjacent, cost);
          }
          else if (cost.CompareTo(expansion[adjacent.position].cost) < 0)
          {
            open.Update(adjacent, cost);
            expansion.ResetCost(adjacent.position, cost);
          }
        }
      }
      while (!open.IsEmpty);

      return expansion;
    }*/

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2Int GridPosition(Vector2Int patchPosition)
    {
      return new Vector2Int(patchPosition.x % _gridSize.x, patchPosition.y % _gridSize.y).Repeat(_gridSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2Int GridPosition(Vector2 worldPosition)
    {
      return GridPosition(Vector2Int.RoundToInt((worldPosition - (Vector2)_origin.position) * _tileSizeInv));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2Int GridPosition(int scalarPosition)
    {
      return new Vector2Int(scalarPosition % _gridSize.x, scalarPosition / _gridSize.x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2Int PatchPosition(Vector2Int gridPosition, Vector4Int viewRect)
    {
      return new Vector2Int(gridPosition.x - ((gridPosition.x - viewRect.x) / _gridSize.x) * _gridSize.x, gridPosition.y - ((gridPosition.y - viewRect.y) / _gridSize.y) * _gridSize.y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ScalarPosition(Vector2Int gridPosition)
    {
      return ((gridPosition.y + _gridSize.y) % _gridSize.y) * _gridSize.x + ((gridPosition.x + _gridSize.x) % _gridSize.x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 LocalPosition(Vector2 gridPosition)
    {
      return gridPosition * tileSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 NormalPosition(Vector2Int gridPosition)
    {
      return (Vector2)gridPosition * _gridSizeInv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2Int AdjacentPosition(Vector2Int gridPosition, uint crumb)
    {
      return (gridPosition + _directions[crumb]).Repeat(_gridSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNode(Vector2Int gridPosition, out TNode node)
    {
      return _nodes.TryGetValue(gridPosition, out node);
    }

    /*public bool TryGetDistantNode(Vector2Int gridPosition, int crumb, int distance, out RingNode<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost> node)
    {
      if (TryGetNode(gridPosition, out node))
      {
        for (int i = 0; i < distance; i++)
        {
          if (!node.TryGetAdjacent(crumb, out node))
          {
            return false;
          }
        }

        return true;
      }

      node = null;
      return false;
    }*/

    void AddNode(TNode node)
    {
      _nodes.Add(node.Position, node);

      for (uint crumb = 0; crumb < 8; crumb++)
      {
        var gridPosition = AdjacentPosition(node.Position, crumb);

        if (TryGetNode(gridPosition, out var adjacent))
        {
          node.AddAdjacent(crumb, adjacent);
          adjacent.AddAdjacent((crumb + 4) % 8, node);
        }
      }
    }

    void RemoveNode(TNode node)
    {
      if (_nodes.Remove(node.Position))
      {
        for (uint crumb = 0; crumb < 8; crumb++)
        {
          if (node.TryGetAdjacent(crumb, out var adjacent))
          {
            node.RemoveAdjacent(crumb);
            adjacent.RemoveAdjacent((crumb + 4) % 8);
          }
        }
      }
    }

    //todo: fix for case when _origin changes during pan
    /*public IEnumerator PanRoutine(int position, float duration)
    {
      return PanRoutine(LocalPosition(position), duration);
    }*/

    public IEnumerator PanRoutine(Vector2 localPosition, float duration, AnimationCurve curve)
    {
      Vector2 delta = CalcDelta(-_origin.localPosition, localPosition);

      float t = 0f;
      float t1 = 0f;
      float durationInv = 1f / duration;

      while (t < 1f)
      {
        float t2 = curve.Evaluate(t);

        Scroll(delta * (t2 - t1));

        t1 = t2;
        t += Time.deltaTime * durationInv;

        yield return null;
      }

      Scroll(delta * (1f - t1));

      _panRoutine = null;
    }

    protected Vector2 WrapPosition(Vector2 worldPosition)
    {
      if (worldPosition.x > _worldHalf.x)
      {
        worldPosition.x -= _worldSize.x;
      }
      else if (worldPosition.x < -_worldHalf.x)
      {
        worldPosition.x += _worldSize.x;
      }

      if (worldPosition.y > _worldHalf.y)
      {
        worldPosition.y -= _worldSize.y;
      }
      else if (worldPosition.y < -_worldHalf.y)
      {
        worldPosition.y += _worldSize.y;
      }

      return worldPosition;
    }

    protected Vector2Int WrapPosition(Vector2Int gridPosition)
    {
      if (gridPosition.x > _gridHalf.x)
      {
        gridPosition.x -= _gridSize.x;
      }
      else if (gridPosition.x < -_gridHalf.x)
      {
        gridPosition.x += _gridSize.x;
      }

      if (gridPosition.y > _gridHalf.y)
      {
        gridPosition.y -= _gridSize.y;
      }
      else if (gridPosition.y < -_gridHalf.y)
      {
        gridPosition.y += _gridSize.y;
      }

      return gridPosition;
    }

    Vector2Int CalcViewOrgin()
    {
      return Vector2Int.RoundToInt(Origin - gridOffset);
    }

    Vector2Int CalcViewOffset()
    {
      return Vector2Int.RoundToInt((_rectTransform.rect.size * _tileSizeInv) * 0.5f + viewPadding);
    }

    Vector4Int CalcViewRect()
    {
      return new Vector4Int(_viewOrigin.x - _viewOffset.x, _viewOrigin.y - _viewOffset.y, _viewOrigin.x + _viewOffset.x, _viewOrigin.y + _viewOffset.y);
    }

    Vector2 CalcDelta(Vector2 origin, Vector2 destination)
    {
      return WrapPosition(destination - origin);
    }

    void ShiftView()
    {
      if (_origin.localPosition.x > _worldHalf.x)
      {
        if (_origin.localPosition.y > _worldHalf.y)
        {
          ShiftViewBottom();
        }
        else if (_origin.localPosition.y < -_worldHalf.y)
        {
          ShiftViewTop();
        }

        ShiftViewLeft();
      }
      else if (_origin.localPosition.x < -_worldHalf.x)
      {
        if (_origin.localPosition.y < -_worldHalf.y)
        {
          ShiftViewTop();
        }
        else if (_origin.localPosition.y > _worldHalf.y)
        {
          ShiftViewBottom();
        }

        ShiftViewRight();
      }
      else
      {
        if (_origin.localPosition.y > _worldHalf.y)
        {
          ShiftViewBottom();
        }
        else if (_origin.localPosition.y < -_worldHalf.y)
        {
          ShiftViewTop();
        }
      }

      _viewOrigin = CalcViewOrgin();
    }

    void ShiftViewLeft()
    {
      _origin.Translate(-_worldSize.x, 0f, 0f);
      _viewRect.x += _gridSize.x;
      _viewRect.z += _gridSize.x;
    }

    void ShiftViewBottom()
    {
      _origin.Translate(0f, -_worldSize.y, 0f);
      _viewRect.y += _gridSize.y;
      _viewRect.w += _gridSize.y;
    }

    void ShiftViewRight()
    {
      _origin.Translate(_worldSize.x, 0f, 0f);
      _viewRect.x -= _gridSize.x;
      _viewRect.z -= _gridSize.x;
    }

    void ShiftViewTop()
    {
      _origin.Translate(0f, _worldSize.y, 0f);
      _viewRect.y -= _gridSize.y;
      _viewRect.w -= _gridSize.y;
    }

    TCost DefaultCalcCost(TCost cost, TNode origin, TNode current, TNode next, uint crumb)
    {
      return cost.Add(next.Data.Cost);
    }

    protected void ShowTile(TNode node, Vector2Int patchPosition)
    {
      if (_groups.TryGetValue(node.Position, out var group))
      {
        if (group.IncrementVisibility() && !group.KeepAlive)
        {
          foreach (var member in group.Members)
          {
            ShowTile2(member, patchPosition + WrapPosition(member.Position - node.Position));
          }
        }
      }
      else
      {
        ShowTile2(node, patchPosition);
      }
    }

    protected void ShowTile2(TNode node, Vector2Int patchPosition)
    {
      var tile = tilePool.Get(node.Data.Terrain, _origin, true);
      tile.Show((TMap)this, node);
      tile.transform.localPosition = Vector3.back + (Vector3)LocalPosition(patchPosition + gridOffset);
#if UNITY_EDITOR
      tile.gameObject.name = node.Position.ToString();
#endif
      node.Tile = tile;
    }

    protected void HideTile(TNode node, Vector2Int patchPosition)//TODO: HideTile without patchPosition
    {
      if (_groups.TryGetValue(node.Position, out var group))
      {
        if (group.DecrementVisibility() && !group.KeepAlive)
        {
          foreach (var member in group.Members)
          {
            HideTile2(member);
          }
        }
      }
      else
      {
        HideTile2(node);
      }
    }

    protected void HideTile2(TNode node)
    {
      if (node.HasTile)
      {
        node.Tile.Hide();
        tilePool.Release(node.Tile);
        node.Tile = null;
      }
    }

    public void AddGroup(IEnumerable<Vector2Int> gridPositions, bool keepAlive = false)
    {
      var members = new HashSet<TNode>();
      Vector2Int? rootPosition = null;

      foreach (var gridPosition in gridPositions)
      {
        if (_groups.ContainsKey(gridPosition))
        {
          Debug.LogWarning($"Colliding node at {gridPosition}");
        }
        else if (TryGetNode(gridPosition, out var node))
        {
          members.Add(node);

          if (!rootPosition.HasValue && node.HasTile)
          {
            rootPosition = node.Position;
          }
        }
        else
        {
          Debug.LogWarning($"Missing node at {gridPosition}");
        }
      }

      var group = new Group<TMap, TNode, TDataSpec, TData, TTile, TTileVariant, TCost>(keepAlive);

      foreach (var member in members)
      {
        group.AddNode(member, member.HasTile);
        _groups.Add(member.Position, group);
      }

      if (rootPosition.HasValue)
      {
        var patchPosition = PatchPosition(rootPosition.Value, _viewRect);

        foreach (var member in members)
        {
          if (!member.HasTile && (keepAlive || group.IsVisible))
          {
            ShowTile2(member, patchPosition + WrapPosition(member.Position - rootPosition.Value));
          }
        }
      }
      else
      {
        foreach (var member in members)
        {
          if (!member.HasTile && (keepAlive || group.IsVisible))
          {
            ShowTile2(member, PatchPosition(member.Position, _viewRect));
          }
        }
      }
    }

    public void RemoveGroup(Vector2Int gridPosition)
    {
      if (_groups.TryGetValue(gridPosition, out var group))
      {
        foreach (var member in group.Members)
        {
          if (_groups.Remove(member.Position))
          {
            if (member.HasTile && !IsVisible(member.Position))
            {
              HideTile2(member);
            }
          }
          else
          {
            Debug.LogError("no member (should never happen)");
          }
        }
      }
      else
      {
        Debug.LogWarning("no group");
      }
    }

    protected void HideAll()
    {
      EachTile(_viewRect, HideTile);
    }

    protected int CalcCrumb(Vector2 direction)
    {
      return (Mathf.RoundToInt(Mathf.Atan2(direction.x, direction.y) * PI4_INV) + 4) % 8;
    }

    protected int CalcMinDistance(Vector2Int origin, Vector2Int destination)
    {
      int dx = Math.Abs(origin.x - destination.x);
      int dy = Math.Abs(origin.y - destination.y);
      return Math.Min(dx, _gridSize.x - dx) + Math.Min(dy, _gridSize.y - dy);
    }

    /*protected int CalcEdgeSeed(int position, int crumb)
    {
      int x = position % gridSize.x;
      int y = (int)(position * _gridSizeInv.x);
      Vector2Int step = _steps[crumb];
      int ax = (x + step.x + gridSize.x) % gridSize.x;
      int ay = (y + step.y + gridSize.y) % gridSize.y;
      return (position ^ seed) ^ ((ay * gridSize.x + ax) ^ seed);
    }*/
  }
}