using System;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using UnityEngine;

namespace Schnitzel.World
{
  public class TileData
  {
    public readonly List<SpatialData> geometries;
    static ulong Id;

    public TileData(int zoom, Vector2Int position, VectorTile tile)
    {
      geometries = new List<SpatialData>();

      foreach (var layer in tile.Layers)
      {
        Debug.Assert(layer.Version == 2);
        Debug.Assert(layer.Extent == Tile.VECTOR_EXTENT);
        //Debug.Log($"Name: {layer.Name}");

        foreach (var feature in layer.Features)
        {
          var id = feature.Id;
          Id = id;
          var tags = new Dictionary<string, object>();

          for (var i = 0; i < feature.Tags.Count; i += 2)
          {
            tags.Add(layer.Keys[(int)feature.Tags[i]], GetLayerValue(layer.Values[(int)feature.Tags[i + 1]]));
          }

          switch (feature.Type)
          {
            case VectorTile.Types.GeomType.Point:
              var pointBounds = DecodePointGeometry(feature.Geometry, out var points);
              var pointData = new PointData(id, tags, points);
              pointData.BuildEnvelope(zoom, position, pointBounds);
              this.geometries.Add(pointData);
              break;

            case VectorTile.Types.GeomType.Linestring:
              var lineBounds = DecodeLinestringGeometry(feature.Geometry, out var lines);
              var lineData = new LineData(id, tags, lines);
              lineData.BuildEnvelope(zoom, position, lineBounds);
              this.geometries.Add(lineData);
              break;

            case VectorTile.Types.GeomType.Polygon:
              var areaBounds = DecodePolygonGeometry(feature.Geometry, out var polygons);
              var areaData = new AreaData(id, tags, polygons);
              areaData.BuildEnvelope(zoom, position, areaBounds);
              this.geometries.Add(areaData);
              break;

            case VectorTile.Types.GeomType.Unknown:
              Debug.LogWarning("Unknown geometry type");
              break;
          }
        }
      }
    }

    //enum Command { MoveTo = 1, LineTo = 2, ClosePath = 7 };

    struct VectorTileCommand
    {
      public int id;
      public int count;

      public VectorTileCommand(int id, int count)
      {
        this.id = id;
        this.count = count;
      }
    }

    SpatialBounds DecodePointGeometry(RepeatedField<uint> geometry, out List<Vector2Int> points)
    {
      var bounds = new SpatialBounds();
      points = new List<Vector2Int>();

      for (var index = 0; index < geometry.Count;)
      {
        var command1 = DecodeCommand(geometry, ref index);
        Debug.Assert(command1.id == 1 && command1.count > 0);

        for (var j = 0; j < command1.count; j++)
        {
          DecodeZigZag(geometry, bounds, points, ref index);
        }
      }

      return bounds;
    }

    SpatialBounds DecodeLinestringGeometry(RepeatedField<uint> geometry, out List<List<Vector2Int>> lines)
    {
      var bounds = new SpatialBounds();
      lines = new List<List<Vector2Int>>();

      for (var index = 0; index < geometry.Count;)
      {
        var line = new List<Vector2Int>();
        lines.Add(line);

        var command1 = DecodeCommand(geometry, ref index);
        Debug.Assert(command1.id == 1 && command1.count == 1);

        DecodeZigZag(geometry, bounds, line, ref index);

        var command2 = DecodeCommand(geometry, ref index);
        Debug.Assert(command2.id == 2 && command2.count > 0);

        for (var i = 0; i < command2.count; i++)
        {
          DecodeZigZag(geometry, bounds, line, ref index);
        }
      }

      return bounds;
    }

    SpatialBounds DecodePolygonGeometry(RepeatedField<uint> geometry, out List<AreaDataPolygons> polygons)
    {
      var bounds = new SpatialBounds();
      polygons = new List<AreaDataPolygons>();

      AreaDataPolygons polygon = null;

      for (var index = 0; index < geometry.Count;)
      {
        var ring = DecodeRing(geometry, bounds, ref index);
        var area = CalculateArea(ring);

        if (area >= 0)
        {
          polygon = new AreaDataPolygons();
          polygons.Add(polygon);
        }

        polygon.rings.Add(ring);
        polygon.area += area;
      }

      return bounds;
    }

    VectorTileCommand DecodeCommand(RepeatedField<uint> geometry, ref int index)
    {
      var command = geometry[index++];

      return new VectorTileCommand((int)(command & 0x7), (int)(command >> 3));
    }

    void DecodeZigZag(RepeatedField<uint> geometry, SpatialBounds bounds, List<Vector2Int> points, ref int index)
    {
      var x = geometry[index++];
      var y = geometry[index++];

      var move = new Vector2Int((int)((x >> 1) ^ -(x & 1)), (int)((y >> 1) ^ -(y & 1)));

      bounds.Encapsulate(move);
      points.Add(bounds.cursor);
    }

    List<Vector2Int> DecodeRing(RepeatedField<uint> geometry, SpatialBounds bounds, ref int index)
    {
      var ring = new List<Vector2Int>();

      var command1 = DecodeCommand(geometry, ref index);
      Debug.Assert(command1.id == 1 && command1.count == 1);

      DecodeZigZag(geometry, bounds, ring, ref index);

      var command2 = DecodeCommand(geometry, ref index);
      Debug.Assert(command2.id == 2 && command2.count > 1);

      for (var i = 0; i < command2.count; i++)
      {
        DecodeZigZag(geometry, bounds, ring, ref index);
      }

      var command3 = DecodeCommand(geometry, ref index);
      Debug.Assert(command3.id == 7 && command3.count == 1);

      return ring;
    }

    // https://en.wikipedia.org/wiki/Shoelace_formula
    float CalculateArea(List<Vector2Int> ring)
    {
      var n = ring.Count;
      var a = 0f;

      for (var i = 0; i < n - 1; i++)
      {
        a += ring[i].x * ring[i + 1].y - ring[i + 1].x * ring[i].y;
      }

      return (a + ring[n - 1].x * ring[0].y - ring[0].x * ring[n - 1].y) * 0.5f;
    }

    object GetLayerValue(VectorTile.Types.Value value)
    {
      if (value.HasBoolValue)
      {
        return value.BoolValue;
      }
      else if (value.HasDoubleValue)
      {
        return value.DoubleValue;
      }
      else if (value.HasFloatValue)
      {
        return value.FloatValue;
      }
      else if (value.HasIntValue)
      {
        return value.IntValue;
      }
      else if (value.HasStringValue)
      {
        return value.StringValue;
      }
      else if (value.HasSintValue)
      {
        return value.SintValue;
      }
      else if (value.HasUintValue)
      {
        return value.UintValue;
      }
      else
      {
        throw new Exception("Missing value");
      }
    }
  }
}