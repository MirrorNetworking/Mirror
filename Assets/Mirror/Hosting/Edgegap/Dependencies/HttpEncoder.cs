// MIRROR CHANGE: drop in Codice.Utils HttpUtility subset to not depend on Unity's plastic scm package
// SOURCE: Unity Plastic SCM package

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Edgegap.Codice.Utils // MIRROR CHANGE: namespace Edgegap.* to not collide if anyone has Plastic SCM installed already
{
  public class HttpEncoder
  {
    private static char[] hexChars = "0123456789abcdef".ToCharArray();
    private static object entitiesLock = new object();
    private static SortedDictionary<string, char> entities;
    private static HttpEncoder defaultEncoder = new HttpEncoder();
    private static HttpEncoder currentEncoder = HttpEncoder.defaultEncoder;

    private static IDictionary<string, char> Entities
    {
      get
      {
        lock (HttpEncoder.entitiesLock)
        {
          if (HttpEncoder.entities == null)
            HttpEncoder.InitEntities();
          return (IDictionary<string, char>) HttpEncoder.entities;
        }
      }
    }

    public static HttpEncoder Current
    {
      get => HttpEncoder.currentEncoder;
      set => HttpEncoder.currentEncoder = value != null ? value : throw new ArgumentNullException(nameof (value));
    }

    public static HttpEncoder Default => HttpEncoder.defaultEncoder;

    protected internal virtual void HeaderNameValueEncode(
      string headerName,
      string headerValue,
      out string encodedHeaderName,
      out string encodedHeaderValue)
    {
      encodedHeaderName = !string.IsNullOrEmpty(headerName) ? HttpEncoder.EncodeHeaderString(headerName) : headerName;
      if (string.IsNullOrEmpty(headerValue))
        encodedHeaderValue = headerValue;
      else
        encodedHeaderValue = HttpEncoder.EncodeHeaderString(headerValue);
    }

    private static void StringBuilderAppend(string s, ref StringBuilder sb)
    {
      if (sb == null)
        sb = new StringBuilder(s);
      else
        sb.Append(s);
    }

    private static string EncodeHeaderString(string input)
    {
      StringBuilder sb = (StringBuilder) null;
      for (int index = 0; index < input.Length; ++index)
      {
        char ch = input[index];
        if (ch < ' ' && ch != '\t' || ch == '\u007F')
          HttpEncoder.StringBuilderAppend(string.Format("%{0:x2}", (object) (int) ch), ref sb);
      }
      return sb != null ? sb.ToString() : input;
    }

    protected internal virtual void HtmlAttributeEncode(string value, TextWriter output)
    {
      if (output == null)
        throw new ArgumentNullException(nameof (output));
      if (string.IsNullOrEmpty(value))
        return;
      output.Write(HttpEncoder.HtmlAttributeEncode(value));
    }

    protected internal virtual void HtmlDecode(string value, TextWriter output)
    {
      if (output == null)
        throw new ArgumentNullException(nameof (output));
      output.Write(HttpEncoder.HtmlDecode(value));
    }

    protected internal virtual void HtmlEncode(string value, TextWriter output)
    {
      if (output == null)
        throw new ArgumentNullException(nameof (output));
      output.Write(HttpEncoder.HtmlEncode(value));
    }

    protected internal virtual byte[] UrlEncode(byte[] bytes, int offset, int count) => HttpEncoder.UrlEncodeToBytes(bytes, offset, count);

    protected internal virtual string UrlPathEncode(string value)
    {
      if (string.IsNullOrEmpty(value))
        return value;
      MemoryStream result = new MemoryStream();
      int length = value.Length;
      for (int index = 0; index < length; ++index)
        HttpEncoder.UrlPathEncodeChar(value[index], (Stream) result);
      return Encoding.ASCII.GetString(result.ToArray());
    }

    internal static byte[] UrlEncodeToBytes(byte[] bytes, int offset, int count)
    {
      int num1 = bytes != null ? bytes.Length : throw new ArgumentNullException(nameof (bytes));
      if (num1 == 0)
        return new byte[0];
      if (offset < 0 || offset >= num1)
        throw new ArgumentOutOfRangeException(nameof (offset));
      if (count < 0 || count > num1 - offset)
        throw new ArgumentOutOfRangeException(nameof (count));
      MemoryStream result = new MemoryStream(count);
      int num2 = offset + count;
      for (int index = offset; index < num2; ++index)
        HttpEncoder.UrlEncodeChar((char) bytes[index], (Stream) result, false);
      return result.ToArray();
    }

    internal static string HtmlEncode(string s)
    {
      switch (s)
      {
        case "":
          return string.Empty;
        case null:
          return (string) null;
        default:
          bool flag = false;
          for (int index = 0; index < s.Length; ++index)
          {
            char ch = s[index];
            if (ch == '&' || ch == '"' || ch == '<' || ch == '>' || ch > '\u009F' || ch == '\'')
            {
              flag = true;
              break;
            }
          }
          if (!flag)
            return s;
          StringBuilder stringBuilder = new StringBuilder();
          int length = s.Length;
          for (int index = 0; index < length; ++index)
          {
            char ch = s[index];
            switch (ch)
            {
              case '"':
                stringBuilder.Append("&quot;");
                break;
              case '&':
                stringBuilder.Append("&amp;");
                break;
              case '\'':
                stringBuilder.Append("&#39;");
                break;
              case '<':
                stringBuilder.Append("&lt;");
                break;
              case '>':
                stringBuilder.Append("&gt;");
                break;
              case '＜':
                stringBuilder.Append("&#65308;");
                break;
              case '＞':
                stringBuilder.Append("&#65310;");
                break;
              default:
                if (ch > '\u009F' && ch < 'Ā')
                {
                  stringBuilder.Append("&#");
                  stringBuilder.Append(((int) ch).ToString((IFormatProvider) CultureInfo.InvariantCulture));
                  stringBuilder.Append(";");
                  break;
                }
                stringBuilder.Append(ch);
                break;
            }
          }
          return stringBuilder.ToString();
      }
    }

    internal static string HtmlAttributeEncode(string s)
    {
      if (string.IsNullOrEmpty(s))
        return string.Empty;
      bool flag = false;
      for (int index = 0; index < s.Length; ++index)
      {
        char ch = s[index];
        int num;
        switch (ch)
        {
          case '"':
          case '&':
          case '<':
            num = 0;
            break;
          default:
            num = ch != '\'' ? 1 : 0;
            break;
        }
        if (num == 0)
        {
          flag = true;
          break;
        }
      }
      if (!flag)
        return s;
      StringBuilder stringBuilder = new StringBuilder();
      int length = s.Length;
      for (int index = 0; index < length; ++index)
      {
        char ch = s[index];
        switch (ch)
        {
          case '"':
            stringBuilder.Append("&quot;");
            break;
          case '&':
            stringBuilder.Append("&amp;");
            break;
          case '\'':
            stringBuilder.Append("&#39;");
            break;
          case '<':
            stringBuilder.Append("&lt;");
            break;
          default:
            stringBuilder.Append(ch);
            break;
        }
      }
      return stringBuilder.ToString();
    }

    internal static string HtmlDecode(string s)
    {
      switch (s)
      {
        case "":
          return string.Empty;
        case null:
          return (string) null;
        default:
          if (s.IndexOf('&') == -1)
            return s;
          StringBuilder stringBuilder1 = new StringBuilder();
          StringBuilder stringBuilder2 = new StringBuilder();
          StringBuilder stringBuilder3 = new StringBuilder();
          int length = s.Length;
          int num1 = 0;
          int num2 = 0;
          bool flag1 = false;
          bool flag2 = false;
          for (int index = 0; index < length; ++index)
          {
            char ch = s[index];
            if (num1 == 0)
            {
              if (ch == '&')
              {
                stringBuilder2.Append(ch);
                stringBuilder1.Append(ch);
                num1 = 1;
              }
              else
                stringBuilder3.Append(ch);
            }
            else if (ch == '&')
            {
              num1 = 1;
              if (flag2)
              {
                stringBuilder2.Append(num2.ToString((IFormatProvider) CultureInfo.InvariantCulture));
                flag2 = false;
              }
              stringBuilder3.Append(stringBuilder2.ToString());
              stringBuilder2.Length = 0;
              stringBuilder2.Append('&');
            }
            else
            {
              switch (num1)
              {
                case 1:
                  if (ch == ';')
                  {
                    num1 = 0;
                    stringBuilder3.Append(stringBuilder2.ToString());
                    stringBuilder3.Append(ch);
                    stringBuilder2.Length = 0;
                    break;
                  }
                  num2 = 0;
                  flag1 = false;
                  num1 = ch == '#' ? 3 : 2;
                  stringBuilder2.Append(ch);
                  stringBuilder1.Append(ch);
                  break;
                case 2:
                  stringBuilder2.Append(ch);
                  if (ch == ';')
                  {
                    string str = stringBuilder2.ToString();
                    if (str.Length > 1 && HttpEncoder.Entities.ContainsKey(str.Substring(1, str.Length - 2)))
                      str = HttpEncoder.Entities[str.Substring(1, str.Length - 2)].ToString();
                    stringBuilder3.Append(str);
                    num1 = 0;
                    stringBuilder2.Length = 0;
                    stringBuilder1.Length = 0;
                    break;
                  }
                  break;
                case 3:
                  if (ch == ';')
                  {
                    if (num2 == 0)
                      stringBuilder3.Append(stringBuilder1.ToString() + ";");
                    else if (num2 > (int) ushort.MaxValue)
                    {
                      stringBuilder3.Append("&#");
                      stringBuilder3.Append(num2.ToString((IFormatProvider) CultureInfo.InvariantCulture));
                      stringBuilder3.Append(";");
                    }
                    else
                      stringBuilder3.Append((char) num2);
                    num1 = 0;
                    stringBuilder2.Length = 0;
                    stringBuilder1.Length = 0;
                    flag2 = false;
                  }
                  else if (flag1 && Uri.IsHexDigit(ch))
                  {
                    num2 = num2 * 16 + Uri.FromHex(ch);
                    flag2 = true;
                    stringBuilder1.Append(ch);
                  }
                  else if (char.IsDigit(ch))
                  {
                    num2 = num2 * 10 + ((int) ch - 48);
                    flag2 = true;
                    stringBuilder1.Append(ch);
                  }
                  else if (num2 == 0 && (ch == 'x' || ch == 'X'))
                  {
                    flag1 = true;
                    stringBuilder1.Append(ch);
                  }
                  else
                  {
                    num1 = 2;
                    if (flag2)
                    {
                      stringBuilder2.Append(num2.ToString((IFormatProvider) CultureInfo.InvariantCulture));
                      flag2 = false;
                    }
                    stringBuilder2.Append(ch);
                  }
                  break;
              }
            }
          }
          if (stringBuilder2.Length > 0)
            stringBuilder3.Append(stringBuilder2.ToString());
          else if (flag2)
            stringBuilder3.Append(num2.ToString((IFormatProvider) CultureInfo.InvariantCulture));
          return stringBuilder3.ToString();
      }
    }

    internal static bool NotEncoded(char c) => c == '!' || c == '(' || c == ')' || c == '*' || c == '-' || c == '.' || c == '_';

    internal static void UrlEncodeChar(char c, Stream result, bool isUnicode)
    {
      if (c > 'ÿ')
      {
        int num = (int) c;
        result.WriteByte((byte) 37);
        result.WriteByte((byte) 117);
        int index1 = num >> 12;
        result.WriteByte((byte) HttpEncoder.hexChars[index1]);
        int index2 = num >> 8 & 15;
        result.WriteByte((byte) HttpEncoder.hexChars[index2]);
        int index3 = num >> 4 & 15;
        result.WriteByte((byte) HttpEncoder.hexChars[index3]);
        int index4 = num & 15;
        result.WriteByte((byte) HttpEncoder.hexChars[index4]);
      }
      else if (c > ' ' && HttpEncoder.NotEncoded(c))
        result.WriteByte((byte) c);
      else if (c == ' ')
        result.WriteByte((byte) 43);
      else if (c < '0' || c < 'A' && c > '9' || c > 'Z' && c < 'a' || c > 'z')
      {
        if (isUnicode && c > '\u007F')
        {
          result.WriteByte((byte) 37);
          result.WriteByte((byte) 117);
          result.WriteByte((byte) 48);
          result.WriteByte((byte) 48);
        }
        else
          result.WriteByte((byte) 37);
        int index5 = (int) c >> 4;
        result.WriteByte((byte) HttpEncoder.hexChars[index5]);
        int index6 = (int) c & 15;
        result.WriteByte((byte) HttpEncoder.hexChars[index6]);
      }
      else
        result.WriteByte((byte) c);
    }

    internal static void UrlPathEncodeChar(char c, Stream result)
    {
      if (c < '!' || c > '~')
      {
        byte[] bytes = Encoding.UTF8.GetBytes(c.ToString());
        for (int index1 = 0; index1 < bytes.Length; ++index1)
        {
          result.WriteByte((byte) 37);
          int index2 = (int) bytes[index1] >> 4;
          result.WriteByte((byte) HttpEncoder.hexChars[index2]);
          int index3 = (int) bytes[index1] & 15;
          result.WriteByte((byte) HttpEncoder.hexChars[index3]);
        }
      }
      else if (c == ' ')
      {
        result.WriteByte((byte) 37);
        result.WriteByte((byte) 50);
        result.WriteByte((byte) 48);
      }
      else
        result.WriteByte((byte) c);
    }

    private static void InitEntities()
    {
      HttpEncoder.entities = new SortedDictionary<string, char>((IComparer<string>) StringComparer.Ordinal);
      HttpEncoder.entities.Add("nbsp", ' ');
      HttpEncoder.entities.Add("iexcl", '¡');
      HttpEncoder.entities.Add("cent", '¢');
      HttpEncoder.entities.Add("pound", '£');
      HttpEncoder.entities.Add("curren", '¤');
      HttpEncoder.entities.Add("yen", '¥');
      HttpEncoder.entities.Add("brvbar", '¦');
      HttpEncoder.entities.Add("sect", '§');
      HttpEncoder.entities.Add("uml", '¨');
      HttpEncoder.entities.Add("copy", '©');
      HttpEncoder.entities.Add("ordf", 'ª');
      HttpEncoder.entities.Add("laquo", '«');
      HttpEncoder.entities.Add("not", '¬');
      HttpEncoder.entities.Add("shy", '\u00AD');
      HttpEncoder.entities.Add("reg", '®');
      HttpEncoder.entities.Add("macr", '¯');
      HttpEncoder.entities.Add("deg", '°');
      HttpEncoder.entities.Add("plusmn", '±');
      HttpEncoder.entities.Add("sup2", '\u00B2');
      HttpEncoder.entities.Add("sup3", '\u00B3');
      HttpEncoder.entities.Add("acute", '´');
      HttpEncoder.entities.Add("micro", 'µ');
      HttpEncoder.entities.Add("para", '¶');
      HttpEncoder.entities.Add("middot", '·');
      HttpEncoder.entities.Add("cedil", '¸');
      HttpEncoder.entities.Add("sup1", '\u00B9');
      HttpEncoder.entities.Add("ordm", 'º');
      HttpEncoder.entities.Add("raquo", '»');
      HttpEncoder.entities.Add("frac14", '\u00BC');
      HttpEncoder.entities.Add("frac12", '\u00BD');
      HttpEncoder.entities.Add("frac34", '\u00BE');
      HttpEncoder.entities.Add("iquest", '¿');
      HttpEncoder.entities.Add("Agrave", 'À');
      HttpEncoder.entities.Add("Aacute", 'Á');
      HttpEncoder.entities.Add("Acirc", 'Â');
      HttpEncoder.entities.Add("Atilde", 'Ã');
      HttpEncoder.entities.Add("Auml", 'Ä');
      HttpEncoder.entities.Add("Aring", 'Å');
      HttpEncoder.entities.Add("AElig", 'Æ');
      HttpEncoder.entities.Add("Ccedil", 'Ç');
      HttpEncoder.entities.Add("Egrave", 'È');
      HttpEncoder.entities.Add("Eacute", 'É');
      HttpEncoder.entities.Add("Ecirc", 'Ê');
      HttpEncoder.entities.Add("Euml", 'Ë');
      HttpEncoder.entities.Add("Igrave", 'Ì');
      HttpEncoder.entities.Add("Iacute", 'Í');
      HttpEncoder.entities.Add("Icirc", 'Î');
      HttpEncoder.entities.Add("Iuml", 'Ï');
      HttpEncoder.entities.Add("ETH", 'Ð');
      HttpEncoder.entities.Add("Ntilde", 'Ñ');
      HttpEncoder.entities.Add("Ograve", 'Ò');
      HttpEncoder.entities.Add("Oacute", 'Ó');
      HttpEncoder.entities.Add("Ocirc", 'Ô');
      HttpEncoder.entities.Add("Otilde", 'Õ');
      HttpEncoder.entities.Add("Ouml", 'Ö');
      HttpEncoder.entities.Add("times", '×');
      HttpEncoder.entities.Add("Oslash", 'Ø');
      HttpEncoder.entities.Add("Ugrave", 'Ù');
      HttpEncoder.entities.Add("Uacute", 'Ú');
      HttpEncoder.entities.Add("Ucirc", 'Û');
      HttpEncoder.entities.Add("Uuml", 'Ü');
      HttpEncoder.entities.Add("Yacute", 'Ý');
      HttpEncoder.entities.Add("THORN", 'Þ');
      HttpEncoder.entities.Add("szlig", 'ß');
      HttpEncoder.entities.Add("agrave", 'à');
      HttpEncoder.entities.Add("aacute", 'á');
      HttpEncoder.entities.Add("acirc", 'â');
      HttpEncoder.entities.Add("atilde", 'ã');
      HttpEncoder.entities.Add("auml", 'ä');
      HttpEncoder.entities.Add("aring", 'å');
      HttpEncoder.entities.Add("aelig", 'æ');
      HttpEncoder.entities.Add("ccedil", 'ç');
      HttpEncoder.entities.Add("egrave", 'è');
      HttpEncoder.entities.Add("eacute", 'é');
      HttpEncoder.entities.Add("ecirc", 'ê');
      HttpEncoder.entities.Add("euml", 'ë');
      HttpEncoder.entities.Add("igrave", 'ì');
      HttpEncoder.entities.Add("iacute", 'í');
      HttpEncoder.entities.Add("icirc", 'î');
      HttpEncoder.entities.Add("iuml", 'ï');
      HttpEncoder.entities.Add("eth", 'ð');
      HttpEncoder.entities.Add("ntilde", 'ñ');
      HttpEncoder.entities.Add("ograve", 'ò');
      HttpEncoder.entities.Add("oacute", 'ó');
      HttpEncoder.entities.Add("ocirc", 'ô');
      HttpEncoder.entities.Add("otilde", 'õ');
      HttpEncoder.entities.Add("ouml", 'ö');
      HttpEncoder.entities.Add("divide", '÷');
      HttpEncoder.entities.Add("oslash", 'ø');
      HttpEncoder.entities.Add("ugrave", 'ù');
      HttpEncoder.entities.Add("uacute", 'ú');
      HttpEncoder.entities.Add("ucirc", 'û');
      HttpEncoder.entities.Add("uuml", 'ü');
      HttpEncoder.entities.Add("yacute", 'ý');
      HttpEncoder.entities.Add("thorn", 'þ');
      HttpEncoder.entities.Add("yuml", 'ÿ');
      HttpEncoder.entities.Add("fnof", 'ƒ');
      HttpEncoder.entities.Add("Alpha", 'Α');
      HttpEncoder.entities.Add("Beta", 'Β');
      HttpEncoder.entities.Add("Gamma", 'Γ');
      HttpEncoder.entities.Add("Delta", 'Δ');
      HttpEncoder.entities.Add("Epsilon", 'Ε');
      HttpEncoder.entities.Add("Zeta", 'Ζ');
      HttpEncoder.entities.Add("Eta", 'Η');
      HttpEncoder.entities.Add("Theta", 'Θ');
      HttpEncoder.entities.Add("Iota", 'Ι');
      HttpEncoder.entities.Add("Kappa", 'Κ');
      HttpEncoder.entities.Add("Lambda", 'Λ');
      HttpEncoder.entities.Add("Mu", 'Μ');
      HttpEncoder.entities.Add("Nu", 'Ν');
      HttpEncoder.entities.Add("Xi", 'Ξ');
      HttpEncoder.entities.Add("Omicron", 'Ο');
      HttpEncoder.entities.Add("Pi", 'Π');
      HttpEncoder.entities.Add("Rho", 'Ρ');
      HttpEncoder.entities.Add("Sigma", 'Σ');
      HttpEncoder.entities.Add("Tau", 'Τ');
      HttpEncoder.entities.Add("Upsilon", 'Υ');
      HttpEncoder.entities.Add("Phi", 'Φ');
      HttpEncoder.entities.Add("Chi", 'Χ');
      HttpEncoder.entities.Add("Psi", 'Ψ');
      HttpEncoder.entities.Add("Omega", 'Ω');
      HttpEncoder.entities.Add("alpha", 'α');
      HttpEncoder.entities.Add("beta", 'β');
      HttpEncoder.entities.Add("gamma", 'γ');
      HttpEncoder.entities.Add("delta", 'δ');
      HttpEncoder.entities.Add("epsilon", 'ε');
      HttpEncoder.entities.Add("zeta", 'ζ');
      HttpEncoder.entities.Add("eta", 'η');
      HttpEncoder.entities.Add("theta", 'θ');
      HttpEncoder.entities.Add("iota", 'ι');
      HttpEncoder.entities.Add("kappa", 'κ');
      HttpEncoder.entities.Add("lambda", 'λ');
      HttpEncoder.entities.Add("mu", 'μ');
      HttpEncoder.entities.Add("nu", 'ν');
      HttpEncoder.entities.Add("xi", 'ξ');
      HttpEncoder.entities.Add("omicron", 'ο');
      HttpEncoder.entities.Add("pi", 'π');
      HttpEncoder.entities.Add("rho", 'ρ');
      HttpEncoder.entities.Add("sigmaf", 'ς');
      HttpEncoder.entities.Add("sigma", 'σ');
      HttpEncoder.entities.Add("tau", 'τ');
      HttpEncoder.entities.Add("upsilon", 'υ');
      HttpEncoder.entities.Add("phi", 'φ');
      HttpEncoder.entities.Add("chi", 'χ');
      HttpEncoder.entities.Add("psi", 'ψ');
      HttpEncoder.entities.Add("omega", 'ω');
      HttpEncoder.entities.Add("thetasym", 'ϑ');
      HttpEncoder.entities.Add("upsih", 'ϒ');
      HttpEncoder.entities.Add("piv", 'ϖ');
      HttpEncoder.entities.Add("bull", '•');
      HttpEncoder.entities.Add("hellip", '…');
      HttpEncoder.entities.Add("prime", '′');
      HttpEncoder.entities.Add("Prime", '″');
      HttpEncoder.entities.Add("oline", '‾');
      HttpEncoder.entities.Add("frasl", '⁄');
      HttpEncoder.entities.Add("weierp", '℘');
      HttpEncoder.entities.Add("image", 'ℑ');
      HttpEncoder.entities.Add("real", 'ℜ');
      HttpEncoder.entities.Add("trade", '™');
      HttpEncoder.entities.Add("alefsym", 'ℵ');
      HttpEncoder.entities.Add("larr", '←');
      HttpEncoder.entities.Add("uarr", '↑');
      HttpEncoder.entities.Add("rarr", '→');
      HttpEncoder.entities.Add("darr", '↓');
      HttpEncoder.entities.Add("harr", '↔');
      HttpEncoder.entities.Add("crarr", '↵');
      HttpEncoder.entities.Add("lArr", '⇐');
      HttpEncoder.entities.Add("uArr", '⇑');
      HttpEncoder.entities.Add("rArr", '⇒');
      HttpEncoder.entities.Add("dArr", '⇓');
      HttpEncoder.entities.Add("hArr", '⇔');
      HttpEncoder.entities.Add("forall", '∀');
      HttpEncoder.entities.Add("part", '∂');
      HttpEncoder.entities.Add("exist", '∃');
      HttpEncoder.entities.Add("empty", '∅');
      HttpEncoder.entities.Add("nabla", '∇');
      HttpEncoder.entities.Add("isin", '∈');
      HttpEncoder.entities.Add("notin", '∉');
      HttpEncoder.entities.Add("ni", '∋');
      HttpEncoder.entities.Add("prod", '∏');
      HttpEncoder.entities.Add("sum", '∑');
      HttpEncoder.entities.Add("minus", '−');
      HttpEncoder.entities.Add("lowast", '∗');
      HttpEncoder.entities.Add("radic", '√');
      HttpEncoder.entities.Add("prop", '∝');
      HttpEncoder.entities.Add("infin", '∞');
      HttpEncoder.entities.Add("ang", '∠');
      HttpEncoder.entities.Add("and", '∧');
      HttpEncoder.entities.Add("or", '∨');
      HttpEncoder.entities.Add("cap", '∩');
      HttpEncoder.entities.Add("cup", '∪');
      HttpEncoder.entities.Add("int", '∫');
      HttpEncoder.entities.Add("there4", '∴');
      HttpEncoder.entities.Add("sim", '∼');
      HttpEncoder.entities.Add("cong", '≅');
      HttpEncoder.entities.Add("asymp", '≈');
      HttpEncoder.entities.Add("ne", '≠');
      HttpEncoder.entities.Add("equiv", '≡');
      HttpEncoder.entities.Add("le", '≤');
      HttpEncoder.entities.Add("ge", '≥');
      HttpEncoder.entities.Add("sub", '⊂');
      HttpEncoder.entities.Add("sup", '⊃');
      HttpEncoder.entities.Add("nsub", '⊄');
      HttpEncoder.entities.Add("sube", '⊆');
      HttpEncoder.entities.Add("supe", '⊇');
      HttpEncoder.entities.Add("oplus", '⊕');
      HttpEncoder.entities.Add("otimes", '⊗');
      HttpEncoder.entities.Add("perp", '⊥');
      HttpEncoder.entities.Add("sdot", '⋅');
      HttpEncoder.entities.Add("lceil", '⌈');
      HttpEncoder.entities.Add("rceil", '⌉');
      HttpEncoder.entities.Add("lfloor", '⌊');
      HttpEncoder.entities.Add("rfloor", '⌋');
      HttpEncoder.entities.Add("lang", '〈');
      HttpEncoder.entities.Add("rang", '〉');
      HttpEncoder.entities.Add("loz", '◊');
      HttpEncoder.entities.Add("spades", '♠');
      HttpEncoder.entities.Add("clubs", '♣');
      HttpEncoder.entities.Add("hearts", '♥');
      HttpEncoder.entities.Add("diams", '♦');
      HttpEncoder.entities.Add("quot", '"');
      HttpEncoder.entities.Add("amp", '&');
      HttpEncoder.entities.Add("lt", '<');
      HttpEncoder.entities.Add("gt", '>');
      HttpEncoder.entities.Add("OElig", 'Œ');
      HttpEncoder.entities.Add("oelig", 'œ');
      HttpEncoder.entities.Add("Scaron", 'Š');
      HttpEncoder.entities.Add("scaron", 'š');
      HttpEncoder.entities.Add("Yuml", 'Ÿ');
      HttpEncoder.entities.Add("circ", 'ˆ');
      HttpEncoder.entities.Add("tilde", '˜');
      HttpEncoder.entities.Add("ensp", ' ');
      HttpEncoder.entities.Add("emsp", ' ');
      HttpEncoder.entities.Add("thinsp", ' ');
      HttpEncoder.entities.Add("zwnj", '\u200C');
      HttpEncoder.entities.Add("zwj", '\u200D');
      HttpEncoder.entities.Add("lrm", '\u200E');
      HttpEncoder.entities.Add("rlm", '\u200F');
      HttpEncoder.entities.Add("ndash", '–');
      HttpEncoder.entities.Add("mdash", '—');
      HttpEncoder.entities.Add("lsquo", '‘');
      HttpEncoder.entities.Add("rsquo", '’');
      HttpEncoder.entities.Add("sbquo", '‚');
      HttpEncoder.entities.Add("ldquo", '“');
      HttpEncoder.entities.Add("rdquo", '”');
      HttpEncoder.entities.Add("bdquo", '„');
      HttpEncoder.entities.Add("dagger", '†');
      HttpEncoder.entities.Add("Dagger", '‡');
      HttpEncoder.entities.Add("permil", '‰');
      HttpEncoder.entities.Add("lsaquo", '‹');
      HttpEncoder.entities.Add("rsaquo", '›');
      HttpEncoder.entities.Add("euro", '€');
    }
  }
}
