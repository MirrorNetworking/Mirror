// MIRROR CHANGE: drop in Codice.Utils HttpUtility subset to not depend on Unity's plastic scm package
// SOURCE: Unity Plastic SCM package

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;

namespace Edgegap.Codice.Utils // MIRROR CHANGE: namespace Edgegap.* to not collide if anyone has Plastic SCM installed already
{
  public sealed class HttpUtility
  {
    private static void WriteCharBytes(IList buf, char ch, Encoding e)
    {
      if (ch > 'ÿ')
      {
        Encoding encoding = e;
        char[] chars = new char[1]{ ch };
        foreach (byte num in encoding.GetBytes(chars))
          buf.Add((object) num);
      }
      else
        buf.Add((object) (byte) ch);
    }

    public static string UrlDecode(string s, Encoding e)
    {
      if (null == s)
        return (string) null;
      if (s.IndexOf('%') == -1 && s.IndexOf('+') == -1)
        return s;
      if (e == null)
        e = Encoding.UTF8;
      long length = (long) s.Length;
      List<byte> buf = new List<byte>();
      for (int index = 0; (long) index < length; ++index)
      {
        char ch = s[index];
        if (ch == '%' && (long) (index + 2) < length && s[index + 1] != '%')
        {
          if (s[index + 1] == 'u' && (long) (index + 5) < length)
          {
            int num = HttpUtility.GetChar(s, index + 2, 4);
            if (num != -1)
            {
              HttpUtility.WriteCharBytes((IList) buf, (char) num, e);
              index += 5;
            }
            else
              HttpUtility.WriteCharBytes((IList) buf, '%', e);
          }
          else
          {
            int num;
            if ((num = HttpUtility.GetChar(s, index + 1, 2)) != -1)
            {
              HttpUtility.WriteCharBytes((IList) buf, (char) num, e);
              index += 2;
            }
            else
              HttpUtility.WriteCharBytes((IList) buf, '%', e);
          }
        }
        else if (ch == '+')
          HttpUtility.WriteCharBytes((IList) buf, ' ', e);
        else
          HttpUtility.WriteCharBytes((IList) buf, ch, e);
      }
      byte[] array = buf.ToArray();
      return e.GetString(array);
    }

    private static int GetInt(byte b)
    {
      char ch = (char) b;
      if (ch >= '0' && ch <= '9')
        return (int) ch - 48;
      if (ch >= 'a' && ch <= 'f')
        return (int) ch - 97 + 10;
      return ch >= 'A' && ch <= 'F' ? (int) ch - 65 + 10 : -1;
    }

    private static int GetChar(string str, int offset, int length)
    {
      int num1 = 0;
      int num2 = length + offset;
      for (int index = offset; index < num2; ++index)
      {
        char b = str[index];
        if (b > '\u007F')
          return -1;
        int num3 = HttpUtility.GetInt((byte) b);
        if (num3 == -1)
          return -1;
        num1 = (num1 << 4) + num3;
      }
      return num1;
    }

    public static string UrlEncode(string str) => HttpUtility.UrlEncode(str, Encoding.UTF8);

    public static string UrlEncode(string s, Encoding Enc)
    {
      if (s == null)
        return (string) null;
      if (s == string.Empty)
        return string.Empty;
      bool flag = false;
      int length = s.Length;
      for (int index = 0; index < length; ++index)
      {
        char c = s[index];
        if ((c < '0' || c < 'A' && c > '9' || c > 'Z' && c < 'a' || c > 'z') && !HttpEncoder.NotEncoded(c))
        {
          flag = true;
          break;
        }
      }
      if (!flag)
        return s;
      byte[] bytes1 = new byte[Enc.GetMaxByteCount(s.Length)];
      int bytes2 = Enc.GetBytes(s, 0, s.Length, bytes1, 0);
      return Encoding.ASCII.GetString(HttpUtility.UrlEncodeToBytes(bytes1, 0, bytes2));
    }

    public static byte[] UrlEncodeToBytes(byte[] bytes, int offset, int count) => bytes == null ? (byte[]) null : HttpEncoder.Current.UrlEncode(bytes, offset, count);

    public static string HtmlDecode(string s)
    {
      if (s == null)
        return (string) null;
      using (StringWriter output = new StringWriter())
      {
        HttpEncoder.Current.HtmlDecode(s, (TextWriter) output);
        return output.ToString();
      }
    }

    public static NameValueCollection ParseQueryString(string query) => HttpUtility.ParseQueryString(query, Encoding.UTF8);

    public static NameValueCollection ParseQueryString(
      string query,
      Encoding encoding)
    {
      if (query == null)
        throw new ArgumentNullException(nameof (query));
      if (encoding == null)
        throw new ArgumentNullException(nameof (encoding));
      if (query.Length == 0 || query.Length == 1 && query[0] == '?')
        return (NameValueCollection) new HttpUtility.HttpQSCollection();
      if (query[0] == '?')
        query = query.Substring(1);
      NameValueCollection result = (NameValueCollection) new HttpUtility.HttpQSCollection();
      HttpUtility.ParseQueryString(query, encoding, result);
      return result;
    }

    internal static void ParseQueryString(
      string query,
      Encoding encoding,
      NameValueCollection result)
    {
      if (query.Length == 0)
        return;
      string str1 = HttpUtility.HtmlDecode(query);
      int length = str1.Length;
      int num1 = 0;
      bool flag = true;
      while (num1 <= length)
      {
        int startIndex = -1;
        int num2 = -1;
        for (int index = num1; index < length; ++index)
        {
          if (startIndex == -1 && str1[index] == '=')
            startIndex = index + 1;
          else if (str1[index] == '&')
          {
            num2 = index;
            break;
          }
        }
        if (flag)
        {
          flag = false;
          if (str1[num1] == '?')
            ++num1;
        }
        string name;
        if (startIndex == -1)
        {
          name = (string) null;
          startIndex = num1;
        }
        else
          name = HttpUtility.UrlDecode(str1.Substring(num1, startIndex - num1 - 1), encoding);
        if (num2 < 0)
        {
          num1 = -1;
          num2 = str1.Length;
        }
        else
          num1 = num2 + 1;
        string str2 = HttpUtility.UrlDecode(str1.Substring(startIndex, num2 - startIndex), encoding);
        result.Add(name, str2);
        if (num1 == -1)
          break;
      }
    }

    private sealed class HttpQSCollection : NameValueCollection
    {
      public override string ToString()
      {
        int count = this.Count;
        if (count == 0)
          return "";
        StringBuilder stringBuilder = new StringBuilder();
        string[] allKeys = this.AllKeys;
        for (int index = 0; index < count; ++index)
          stringBuilder.AppendFormat("{0}={1}&", (object) allKeys[index], (object) HttpUtility.UrlEncode(this[allKeys[index]]));
        if (stringBuilder.Length > 0)
          --stringBuilder.Length;
        return stringBuilder.ToString();
      }
    }
  }
}
