using UnityEngine;

namespace Schnitzel.World
{
  [RequireComponent(typeof(LineRenderer))]
  public class Line : MonoBehaviour
  {
    public LineData Data { get; private set; }
    public int Index { get; private set; }

    LineRenderer _renderer;
    float _width;

    public void Init(LineData data, int index)
    {
      Data = data;
      Index = index;

      var points = data.lines[index];
      //var renderer = GetComponent<LineRenderer>();
      _renderer = GetComponent<LineRenderer>();
      _renderer.positionCount = points.Count;

      for (var i = 0; i < points.Count; i++)
      {
        _renderer.SetPosition(i, (Vector2)points[i]);
      }

      if (MapStyle.TryMatchLine(data.tags, out var style))
      {
        _renderer.startColor = style.color;
        _renderer.endColor = style.color;
        _renderer.startWidth = style.width;
        _renderer.endWidth = style.width;
        _renderer.sortingOrder = style.order;

        /*if (style.material != null)
        {
          _renderer.textureMode = LineTextureMode.Tile;
          _renderer.material = style.material;
        }*/
      }
      else
      {
        Debug.LogWarning("No style matched");
      }
    }

    /*void Update()
    {
      if (_renderer.textureMode == LineTextureMode.Tile)
      {
        _renderer.textureScale = Vector2.one / _width / transform.;
      }
    }*/

    /*void Start()
    {
      var renderer = GetComponent<LineRenderer>();
      renderer.textureScale = new Vector2(1f / 0.5f, 1f);
      renderer.startWidth = 0.5f;
      renderer.endWidth = 0.5f;
    }*/
  }
}