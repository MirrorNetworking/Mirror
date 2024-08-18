using UnityEngine;

namespace Mirror.Examples.Common.Controllers
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class ContollerUIBase : MonoBehaviour
    {

        // Returns a string representation of a KeyCode that is more suitable
        // for display in the UI than KeyCode.ToString() for "named" keys.
        internal string GetKeyText(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None:
                    return "";

                case KeyCode.Escape:
                    return "Esc";
                case KeyCode.BackQuote:
                    return "`";
                case KeyCode.Tilde:
                    return "~";

                // number keys
                case KeyCode.Alpha1:
                    return "1";
                case KeyCode.Alpha2:
                    return "2";
                case KeyCode.Alpha3:
                    return "3";
                case KeyCode.Alpha4:
                    return "4";
                case KeyCode.Alpha5:
                    return "5";
                case KeyCode.Alpha6:
                    return "6";
                case KeyCode.Alpha7:
                    return "7";
                case KeyCode.Alpha8:
                    return "8";
                case KeyCode.Alpha9:
                    return "9";
                case KeyCode.Alpha0:
                    return "0";

                // punctuation keys
                case KeyCode.Exclaim:
                    return "!";
                case KeyCode.At:
                    return "@";
                case KeyCode.Hash:
                    return "#";
                case KeyCode.Dollar:
                    return "$";
                case KeyCode.Percent:
                    return "%";
                case KeyCode.Caret:
                    return "^";
                case KeyCode.Ampersand:
                    return "&";
                case KeyCode.Asterisk:
                    return "*";
                case KeyCode.LeftParen:
                    return "(";
                case KeyCode.RightParen:
                    return ")";

                case KeyCode.Minus:
                    return "-";
                case KeyCode.Underscore:
                    return "_";
                case KeyCode.Plus:
                    return "+";
                case KeyCode.Equals:
                    return "=";
                case KeyCode.Backspace:
                    return "Back";

                case KeyCode.LeftBracket:
                    return "[";
                case KeyCode.LeftCurlyBracket:
                    return "{";
                case KeyCode.RightBracket:
                    return "]";
                case KeyCode.RightCurlyBracket:
                    return "}";
                case KeyCode.Pipe:
                    return "|";
                case KeyCode.Backslash:
                    return "\\";

                case KeyCode.Semicolon:
                    return ";";
                case KeyCode.Colon:
                    return ":";

                case KeyCode.Quote:
                    return "'";
                case KeyCode.DoubleQuote:
                    return "\"";
                case KeyCode.Return:
                    return "\u23CE";

                case KeyCode.Comma:
                    return ",";
                case KeyCode.Less:
                    return "<";
                case KeyCode.Period:
                    return ".";
                case KeyCode.Greater:
                    return ">";
                case KeyCode.Slash:
                    return "/";
                case KeyCode.Question:
                    return "?";

                // arrow keys
                case KeyCode.UpArrow:
                    return "\u25B2";
                case KeyCode.LeftArrow:
                    return "\u25C4";
                case KeyCode.DownArrow:
                    return "\u25BC";
                case KeyCode.RightArrow:
                    return "\u25BA";

                // special keys
                case KeyCode.PageUp:
                    return "Page\nUp";
                case KeyCode.PageDown:
                    return "Page\nDown";
                case KeyCode.Insert:
                    return "Ins";
                case KeyCode.Delete:
                    return "Del";

                // num pad keys
                case KeyCode.Keypad1:
                    return "Pad\n1";
                case KeyCode.Keypad2:
                    return "Pad\n2";
                case KeyCode.Keypad3:
                    return "Pad\n3";
                case KeyCode.Keypad4:
                    return "Pad\n4";
                case KeyCode.Keypad5:
                    return "Pad\n5";
                case KeyCode.Keypad6:
                    return "Pad\n6";
                case KeyCode.Keypad7:
                    return "Pad\n7";
                case KeyCode.Keypad8:
                    return "Pad\n8";
                case KeyCode.Keypad9:
                    return "Pad\n9";
                case KeyCode.Keypad0:
                    return "Pad\n0";
                case KeyCode.KeypadDivide:
                    return "Pad\n/";
                case KeyCode.KeypadMultiply:
                    return "Pad\n*";
                case KeyCode.KeypadMinus:
                    return "Pad\n-";
                case KeyCode.KeypadPlus:
                    return "Pad\n+";
                case KeyCode.KeypadEquals:
                    return "Pad\n=";
                case KeyCode.KeypadPeriod:
                    return "Pad\n.";
                case KeyCode.KeypadEnter:
                    return "Pad\n\u23CE";

                default:
                    return key.ToString();
            }
        }
    }
}