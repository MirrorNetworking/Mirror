/* 
 * Simple recursive descending JSON parser and
 * JSON string builder.
 * 
 * Jonas Drewsen - (C) Unity3d.com - 2010-2012
 *
 * JSONParser parser = new JSONParser(" { \"hello\" : 42.3 } ");
 * JSONValue value = parser.Parse();
 * 
 * bool is_it_float = value.isFloat();
 * float the_float = value.asFloat();
 * string the_string = value.Get("sub.structure.access").asString();
 * 
 */

using System.Collections.Generic;
using System;

namespace AssetStoreTools.Utility.Json
{

    /*
	 * JSON value structure 
	 * 
	 * Example:
	 * JSONValue v = JSONValue.NewDict();
	 * v["hello"] = JSONValue.NewString("world");
	 * asset(v["hello"].AsString() == "world");
	 * 
	 */
    internal struct JsonValue
	{
		public JsonValue(object o)
		{
			data = o;
		}
		public static implicit operator JsonValue(string s)
		{
			return new JsonValue(s);
		}

		public static implicit operator string(JsonValue s)
		{
			return s.AsString();
		}

		public static implicit operator JsonValue(float s)
		{
			return new JsonValue(s);
		}

		public static implicit operator float(JsonValue s)
		{
			return s.AsFloat();
		}

		public static implicit operator JsonValue(bool s)
		{
			return new JsonValue(s);
		}

		public static implicit operator bool(JsonValue s)
		{
			return s.AsBool();
		}

		public static implicit operator JsonValue(int s)
		{
			return new JsonValue((float)s);
		}

		public static implicit operator int(JsonValue s)
		{
			return (int)s.AsFloat();
		}

		public static implicit operator JsonValue(List<JsonValue> s)
		{
			return new JsonValue(s);
		}

		public static implicit operator List<JsonValue>(JsonValue s)
		{
			return s.AsList();
		}

		public static implicit operator Dictionary<string, JsonValue>(JsonValue s)
		{
			return s.AsDict();
		}

		public bool IsString() { return data is string; }
		public bool IsFloat() { return data is float; }
		public bool IsList() { return data is List<JsonValue>; }
		public bool IsDict() { return data is Dictionary<string, JsonValue>; }
		public bool IsBool() { return data is bool; }
		public bool IsNull() { return data == null; }

		public string AsString(bool nothrow = false)
		{
			if (data is string)
				return (string)data;
			if (!nothrow)
				throw new JSONTypeException("Tried to read non-string json value as string");
			return "";
		}
		public float AsFloat(bool nothrow = false)
		{
			if (data is float)
				return (float)data;
			if (!nothrow)
				throw new JSONTypeException("Tried to read non-float json value as float");
			return 0.0f;
		}
		public bool AsBool(bool nothrow = false)
		{
			if (data is bool)
				return (bool)data;
			if (!nothrow)
				throw new JSONTypeException("Tried to read non-bool json value as bool");
			return false;
		}
		public List<JsonValue> AsList(bool nothrow = false)
		{
			if (data is List<JsonValue>)
				return (List<JsonValue>)data;
			if (!nothrow)
				throw new JSONTypeException("Tried to read " + data.GetType().Name + " json value as list");
			return null;
		}
		public Dictionary<string, JsonValue> AsDict(bool nothrow = false)
		{
			if (data is Dictionary<string, JsonValue>)
				return (Dictionary<string, JsonValue>)data;
			if (!nothrow)
				throw new JSONTypeException("Tried to read non-dictionary json value as dictionary");
			return null;
		}

		public static JsonValue NewString(string val)
		{
			return new JsonValue(val);
		}

		public static JsonValue NewFloat(float val)
		{
			return new JsonValue(val);
		}

		public static JsonValue NewDict()
		{
			return new JsonValue(new Dictionary<string, JsonValue>());
		}

		public static JsonValue NewList()
		{
			return new JsonValue(new List<JsonValue>());
		}

		public static JsonValue NewBool(bool val)
		{
			return new JsonValue(val);
		}

		public static JsonValue NewNull()
		{
			return new JsonValue(null);
		}

		public JsonValue InitList()
		{
			data = new List<JsonValue>();
			return this;
		}

		public JsonValue InitDict()
		{
			data = new Dictionary<string, JsonValue>();
			return this;
		}

		public JsonValue this[string index]
		{
			get
			{
				Dictionary<string, JsonValue> dict = AsDict();
				return dict[index];
			}
			set
			{
				if (data == null)
					data = new Dictionary<string, JsonValue>();
				Dictionary<string, JsonValue> dict = AsDict();
				dict[index] = value;
			}
		}

		public bool ContainsKey(string index)
		{
			if (!IsDict())
				return false;
			return AsDict().ContainsKey(index);
		}

		// Get the specified field in a dict or null json value if
		// no such field exists. The key can point to a nested structure
		// e.g. key1.key2 in  { key1 : { key2 : 32 } }
		public JsonValue Get(string key, out bool found)
		{
			found = false;
			if (!IsDict())
				return new JsonValue(null);
			JsonValue value = this;
			foreach (string part in key.Split('.'))
			{

				if (!value.ContainsKey(part))
					return new JsonValue(null);
				value = value[part];
			}
			found = true;
			return value;
		}

		public JsonValue Get(string key)
		{
			bool found;
			return Get(key, out found);
		}

		public bool Copy(string key, ref string dest)
		{
			return Copy(key, ref dest, true);
		}

		public bool Copy(string key, ref string dest, bool allowCopyNull)
		{
			bool found;
			JsonValue jv = Get(key, out found);
			if (found && (!jv.IsNull() || allowCopyNull))
				dest = jv.IsNull() ? null : jv.AsString();
			return found;
		}

		public bool Copy(string key, ref bool dest)
		{
			bool found;
			JsonValue jv = Get(key, out found);
			if (found && !jv.IsNull())
				dest = jv.AsBool();
			return found;
		}

		public bool Copy(string key, ref int dest)
		{
			bool found;
			JsonValue jv = Get(key, out found);
			if (found && !jv.IsNull())
				dest = (int)jv.AsFloat();
			return found;
		}

		// Convenience dict value setting
		public void Set(string key, string value)
		{
			Set(key, value, true);
		}
		public void Set(string key, string value, bool allowNull)
		{
			if (value == null)
			{
				if (!allowNull)
					return;
				this[key] = NewNull();
				return;
			}
			this[key] = NewString(value);
		}

		// Convenience dict value setting
		public void Set(string key, float value)
		{
			this[key] = NewFloat(value);
		}

		// Convenience dict value setting
		public void Set(string key, bool value)
		{
			this[key] = NewBool(value);
		}

		// Convenience list value add
		public void Add(string value)
		{
			List<JsonValue> list = AsList();
			if (value == null)
			{
				list.Add(NewNull());
				return;
			}
			list.Add(NewString(value));
		}

		// Convenience list value add
		public void Add(float value)
		{
			List<JsonValue> list = AsList();
			list.Add(NewFloat(value));
		}

		// Convenience list value add
		public void Add(bool value)
		{
			List<JsonValue> list = AsList();
			list.Add(NewBool(value));
		}

		public override string ToString()
		{
			return ToString(null, "");
		}
		/* 
		 * Serialize a JSON value to string. 
		 * This will recurse down through dicts and list type JSONValues.
		 */
		public string ToString(string curIndent, string indent)
		{
			bool indenting = curIndent != null;

			if (IsString())
			{
				return "\"" + EncodeString(AsString()) + "\"";
			}
			else if (IsFloat())
			{
				return AsFloat().ToString();
			}
			else if (IsList())
			{
				string res = "[";
				string delim = "";
				foreach (JsonValue i in AsList())
				{
					res += delim + i.ToString();
					delim = ", ";
				}
				return res + "]";
			}
			else if (IsDict())
			{
				string res = "{" + (indenting ? "\n" : "");
				string delim = "";
				foreach (KeyValuePair<string, JsonValue> kv in AsDict())
				{
					res += delim + curIndent + indent + '"' + EncodeString(kv.Key) + "\" : " + kv.Value.ToString(curIndent + indent, indent);
					delim = ", " + (indenting ? "\n" : "");
				}
				return res + (indenting ? "\n" + curIndent : "") + "}";
			}
			else if (IsBool())
			{
				return AsBool() ? "true" : "false";
			}
			else if (IsNull())
			{
				return "null";
			}
			else
			{
				throw new JSONTypeException("Cannot serialize json value of unknown type");
			}
		}



		// Encode a string into a json string
		private static string EncodeString(string str)
		{
			str = str.Replace("\\", "\\\\");
			str = str.Replace("\"", "\\\"");
			str = str.Replace("/", "\\/");
			str = str.Replace("\b", "\\b");
			str = str.Replace("\f", "\\f");
			str = str.Replace("\n", "\\n");
			str = str.Replace("\r", "\\r");
			str = str.Replace("\t", "\\t");
			// We do not use \uXXXX specifier but direct unicode in the string.
			return str;
		}

		object data;
	}

	internal class JSONParseException : Exception
	{
		public JSONParseException(string msg) : base(msg)
		{
		}
	}

	internal class JSONTypeException : Exception
	{
		public JSONTypeException(string msg) : base(msg)
		{
		}
	}

	/*
	 * Top down recursive JSON parser 
	 * 
	 * Example:
	 * string json = "{ \"hello\" : \"world\", \"age\" : 100000, "sister" : null }";
	 * JSONValue val = JSONParser.SimpleParse(json);
	 * asset( val["hello"].AsString() == "world" );
	 *
	 */
	internal class JSONParser
	{
		private string json;
		private int line;
		private int linechar;
		private int len;
		private int idx;
		private int pctParsed;
		private char cur;

		public static JsonValue SimpleParse(string jsondata)
		{
			var parser = new JSONParser(jsondata);
			try
			{
				return parser.Parse();
			}
			catch (JSONParseException ex)
			{
				Console.WriteLine(ex.Message);
				//DebugUtils.LogError(ex.Message);
			}
			return new JsonValue(null);
		}

		public static bool AssetStoreResponseParse(string responseJson, out ASError error, out JsonValue jval)
		{
			jval = new JsonValue();
			error = null;

			try
			{
				JSONParser parser = new JSONParser(responseJson);
				jval = parser.Parse();
			}
			catch (JSONParseException)
			{
				error = ASError.GetGenericError(new Exception("Error parsing reply from AssetStore"));
				return false;
			}

			// Some json responses return an error field on error
			if (jval.ContainsKey("error"))
			{
				// Server side error message
				// Do not write to console since this is an error that 
				// is "expected" ie. can be handled by the gui.
				error = ASError.GetGenericError(new Exception(jval["error"].AsString(true)));
			}
			// Some json responses return status+message fields instead of an error field. Go figure.
			else if (jval.ContainsKey("status") && jval["status"].AsString(true) != "ok")
			{
				error = ASError.GetGenericError(new Exception(jval["message"].AsString(true)));
			}
			return error == null;
		}

		/*
		 * Setup a parse to be ready for parsing the given string
		 */
		public JSONParser(string jsondata)
		{
			// TODO: fix that parser needs trailing spaces;
			json = jsondata + "    ";
			line = 1;
			linechar = 1;
			len = json.Length;
			idx = 0;
			pctParsed = 0;
		}

		/*
		 * Parse the entire json data string into a JSONValue structure hierarchy
		 */
		public JsonValue Parse()
		{
			cur = json[idx];
			return ParseValue();
		}

		private char Next()
		{
			if (cur == '\n')
			{
				line++;
				linechar = 0;
			}
			idx++;
			if (idx >= len)
				throw new JSONParseException("End of json while parsing at " + PosMsg());

			linechar++;

			int newPct = (int)((float)idx * 100f / (float)len);
			if (newPct != pctParsed)
			{
				pctParsed = newPct;
			}
			cur = json[idx];
			return cur;
		}

		private void SkipWs()
		{
			const string ws = " \n\t\r";
			while (ws.IndexOf(cur) != -1) Next();
		}

		private string PosMsg()
		{
			return "line " + line.ToString() + ", column " + linechar.ToString();
		}

		private JsonValue ParseValue()
		{
			// Skip spaces
			SkipWs();

			switch (cur)
			{
				case '[':
					return ParseArray();
				case '{':
					return ParseDict();
				case '"':
					return ParseString();
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					return ParseNumber();
				case 't':
				case 'f':
				case 'n':
					return ParseConstant();
				default:
					throw new JSONParseException("Cannot parse json value starting with '" + json.Substring(idx, 5) + "' at " + PosMsg());
			}
		}

		private JsonValue ParseArray()
		{
			Next();
			SkipWs();
			List<JsonValue> arr = new List<JsonValue>();
			while (cur != ']')
			{
				arr.Add(ParseValue());
				SkipWs();
				if (cur == ',')
				{
					Next();
					SkipWs();
				}
			}
			Next();
			return new JsonValue(arr);
		}

		private JsonValue ParseDict()
		{
			Next();
			SkipWs();
			Dictionary<string, JsonValue> dict = new Dictionary<string, JsonValue>();
			while (cur != '}')
			{
				JsonValue key = ParseValue();
				if (!key.IsString())
					throw new JSONParseException("Key not string type at " + PosMsg());
				SkipWs();
				if (cur != ':')
					throw new JSONParseException("Missing dict entry delimiter ':' at " + PosMsg());
				Next();
				dict.Add(key.AsString(), ParseValue());
				SkipWs();
				if (cur == ',')
				{
					Next();
					SkipWs();
				}
			}
			Next();
			return new JsonValue(dict);
		}

		static char[] endcodes = { '\\', '"' };

		private JsonValue ParseString()
		{
			string res = "";

			Next();

			while (idx < len)
			{
				int endidx = json.IndexOfAny(endcodes, idx);
				if (endidx < 0)
					throw new JSONParseException("missing '\"' to end string at " + PosMsg());

				res += json.Substring(idx, endidx - idx);

				if (json[endidx] == '"')
				{
					cur = json[endidx];
					idx = endidx;
					break;
				}

				endidx++; // get escape code
				if (endidx >= len)
					throw new JSONParseException("End of json while parsing while parsing string at " + PosMsg());

				// char at endidx is \			
				char ncur = json[endidx];
				switch (ncur)
				{
					case '"':
						goto case '/';
					case '\\':
						goto case '/';
					case '/':
						res += ncur;
						break;
					case 'b':
						res += '\b';
						break;
					case 'f':
						res += '\f';
						break;
					case 'n':
						res += '\n';
						break;
					case 'r':
						res += '\r';
						break;
					case 't':
						res += '\t';
						break;
					case 'u':
						// Unicode char specified by 4 hex digits 
						string digit = "";
						if (endidx + 4 >= len)
							throw new JSONParseException("End of json while parsing while parsing unicode char near " + PosMsg());
						digit += json[endidx + 1];
						digit += json[endidx + 2];
						digit += json[endidx + 3];
						digit += json[endidx + 4];
						try
						{
							int d = Int32.Parse(digit, System.Globalization.NumberStyles.AllowHexSpecifier);
							res += (char)d;
						}
						catch (FormatException)
						{
							throw new JSONParseException("Invalid unicode escape char near " + PosMsg());
						}
						endidx += 4;
						break;
					default:
						throw new JSONParseException("Invalid escape char '" + ncur + "' near " + PosMsg());
				}
				idx = endidx + 1;
			}
			if (idx >= len)
				throw new JSONParseException("End of json while parsing while parsing string near " + PosMsg());

			cur = json[idx];

			Next();
			return new JsonValue(res);
		}

		private JsonValue ParseNumber()
		{
			string resstr = "";

			if (cur == '-')
			{
				resstr = "-";
				Next();
			}

			while (cur >= '0' && cur <= '9')
			{
				resstr += cur;
				Next();
			}
			if (cur == '.')
			{
				Next();
				resstr += '.';
				while (cur >= '0' && cur <= '9')
				{
					resstr += cur;
					Next();
				}
			}

			if (cur == 'e' || cur == 'E')
			{
				resstr += "e";
				Next();
				if (cur != '-' && cur != '+')
				{
					// throw new JSONParseException("Missing - or + in 'e' potent specifier at " + PosMsg());				
					resstr += cur;
					Next();
				}
				while (cur >= '0' && cur <= '9')
				{
					resstr += cur;
					Next();
				}
			}

			try
			{
				float f = Convert.ToSingle(resstr);
				return new JsonValue(f);
			}
			catch (Exception)
			{
				throw new JSONParseException("Cannot convert string to float : '" + resstr + "' at " + PosMsg());
			}
		}

		private JsonValue ParseConstant()
		{
			string c = "" + cur + Next() + Next() + Next();
			Next();
			if (c == "true")
			{
				return new JsonValue(true);
			}
			else if (c == "fals")
			{
				if (cur == 'e')
				{
					Next();
					return new JsonValue(false);
				}
			}
			else if (c == "null")
			{
				return new JsonValue(null);
			}
			throw new JSONParseException("Invalid token at " + PosMsg());
		}
	};
}