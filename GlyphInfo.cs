using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FontAppX
{
    public class GlyphInfo
    {
        public int CharCode;
        public string AsciiChar;

        public bool Include = true;

        public int X = 0;
        public int Y = 0;
        public int Width = 0;
        public int Height = 0;

        public int DestX = 0;
        public int DestY = 0;

        public Button GridButton;

        public GlyphInfo(int charCode)
        {
            CharCode = charCode;
            AsciiChar = ((char)charCode).ToString();
        }

        public GlyphInfo(int charCode, int x, int y, int width, int height)
        {
            CharCode = charCode;
            AsciiChar = ((char)charCode).ToString();
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
