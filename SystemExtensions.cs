using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace Carlosfritz
{
  public static class SystemExtensions
  {
    public static List<T> ToList<T>(this string s, char separator = ',')
    {
      var list = new List<T>();
      var converter = TypeDescriptor.GetConverter(typeof(T));

      if (!String.IsNullOrEmpty(s))
      {
        foreach (var e in s.Split(separator))
        {
          list.Add((T)converter.ConvertFromString(e));
        }
      }

      return list;
    }

    public static object Cast(object obj, Type type)
    {
      return Convert.ChangeType(obj, type);
    }

    public static string PadBase64(this string base64)
    {
      var rest = base64.Length % 4;

      return rest == 0 ? base64 : base64.PadRight(base64.Length - rest + 4, '=');
    }

    /*public static string Base64Encode(this string plaintext)
    {
      return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));
    }

    public static string Base64Decode(this string encoded)
    {
      return System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(encoded));
    }*/

    public static void AddCookie(this HttpRequestHeaders headers, Cookie cookie)
    {
      headers.Add("Cookie", cookie.ToString());
    }

    public static string ToPublicString(this object obj)
    {
      Type type = obj.GetType();
      List<string> fields = new List<string>();

      foreach (FieldInfo info in type.GetFields())
      {
        if (info.IsPublic)
        {
          fields.Add(info.Name + ": " + info.GetValue(obj));
        }
      }

      foreach (PropertyInfo info in type.GetProperties())
      {
        if (info.GetGetMethod(true).IsPublic)
        {
          fields.Add(info.Name + ": " + info.GetValue(obj));
        }
      }

      return type.Name + " {" + string.Join(", ", fields) + "}";
    }

    public static string ToKingCase(this string name)
    {
      return char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant();
    }


    public static ulong ReverseBytes(this ulong value)
    {
      return (value & 0x00000000000000FFul) << 56 | (value & 0x000000000000FF00ul) << 40 |
             (value & 0x0000000000FF0000ul) << 24 | (value & 0x00000000FF000000ul) << 8 |
             (value & 0x000000FF00000000ul) >> 8 | (value & 0x0000FF0000000000ul) >> 24 |
             (value & 0x00FF000000000000ul) >> 40 | (value & 0xFF00000000000000ul) >> 56;
    }

    public static ulong RotateLeft(this ulong value, int count)
    {
      return (value << count) | (value >> (64 - count));
    }

    public static ulong RotateRight(this ulong value, int count)
    {
      return (value >> count) | (value << (64 - count));
    }

    public static ulong RotateHalf(this ulong value)
    {
      return (value >> 32) | (value << 32);
    }

    public static uint RotateLeft(this uint value, int count)
    {
      return (value << count) | (value >> (32 - count));
    }

    public static uint RotateRight(this uint value, int count)
    {
      return (value >> count) | (value << (32 - count));
    }

    public static uint RotateHalf(this uint value)
    {
      return (value >> 16) | (value << 16);
    }

    public static uint MirrorX(this uint value)
    {
      return ((value & 0xff) << 8) | ((value & 0xff00) >> 8) | ((value & 0xff0000) << 8) | ((value & 0xff000000) >> 8);
    }

    public static Vector4Int ToVector(this uint value)
    {
      return new Vector4Int((int)(value & 0xff), (int)((value >> 8) & 0xff), (int)((value >> 16) & 0xff), (int)((value >> 24) & 0xff));
    }

    public static Vector4Int ToVector(this ulong value)
    {
      return new Vector4Int((int)(value & 0xfffful), (int)((value >> 16) & 0xfffful), (int)((value >> 32) & 0xfffful), (int)((value >> 48) & 0xfffful));
    }
  }
}