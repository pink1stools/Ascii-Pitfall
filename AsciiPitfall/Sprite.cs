/*
 * ASCII Pitfall!
 * Copyright (C) 2008 Michael Birken
 * 
 * This file is part of ASCII Pitfall!.
 *
 * ASCII Pitfall! is free software; you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published 
 * by the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 *
 * ASCII Pitfall! is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using Mischel.ConsoleDotNet;

namespace AsciiPitfall {
  class Sprite {
    public int Width;
    public int Height;
    public ConsoleColor[,] image;
    public bool[,] transparent;

    public Sprite(string fileName) : this(Assembly.GetExecutingAssembly()
        .GetManifestResourceStream("AsciiPitfall.images." + fileName)) {
    }

    public Sprite(Stream stream) {
      BinaryReader reader = new BinaryReader(stream);
      Width = reader.ReadInt32();
      Height = reader.ReadInt32();
      image = new ConsoleColor[Height, Width];
      transparent = new bool[Height, Width];
      for (int y = 0; y < Height; y++) {
        for (int x = 0; x < Width; x++) {
          int palette = reader.ReadInt32();
          if (palette < 0) {
            transparent[y, x] = true;
          } else {
            image[y, x] = (ConsoleColor)palette;
          }
        }
      }
    }

    public Sprite(string[] rows, ConsoleColor color) {
      Width = rows[0].Length;
      Height = rows.Length;
      image = new ConsoleColor[Height, Width];
      transparent = new bool[Height, Width];
      for (int y = 0; y < Height; y++) {
        for (int x = 0; x < Width; x++) {
          if (rows[y][x] == ' ') {
            transparent[y, x] = true;
          } else {
            image[y, x] = color;
            transparent[y, x] = false;
          }
        }
      }
    }

    public Sprite(string[] rows, ConsoleColor[] colors) {
      Width = rows[0].Length;
      Height = rows.Length;
      image = new ConsoleColor[Height, Width];
      transparent = new bool[Height, Width];
      for (int y = 0; y < Height; y++) {
        for (int x = 0; x < Width; x++) {
          if (rows[y][x] == ' ') {
            transparent[y, x] = true;
          } else {
            image[y, x] = colors[y];
            transparent[y, x] = false;
          }
        }
      }
    }

    public Sprite(string[] rows, char[] symbols, ConsoleColor[] colors) {
      Width = rows[0].Length;
      Height = rows.Length;
      image = new ConsoleColor[Height, Width];
      transparent = new bool[Height, Width];
      for (int y = 0; y < Height; y++) {
        for (int x = 0; x < Width; x++) {
          if (rows[y][x] == ' ') {
            transparent[y, x] = true;
          } else {
            for (int i = 0; i < symbols.Length; i++) {
              if (rows[y][x] == symbols[i]) {
                image[y, x] = colors[i];
                break;
              }
            }
            transparent[y, x] = false;
          }
        }
      }
    }

    public Sprite(string[] rows, ConsoleColor color, int multiple) {
      Width = rows[0].Length * multiple;
      Height = rows.Length;
      image = new ConsoleColor[Height, Width];
      transparent = new bool[Height, Width];
      for (int y = 0; y < Height; y++) {
        for (int x = 0; x < Width; x++) {
          if (rows[y][x / multiple] == ' ') {
            transparent[y, x] = true;
          } else {
            image[y, x] = color;
            transparent[y, x] = false;
          }
        }
      }
    }

    public Sprite(string[] rows, ConsoleColor[] colors, int multiple) {
      Width = rows[0].Length * multiple;
      Height = rows.Length;
      image = new ConsoleColor[Height, Width];
      transparent = new bool[Height, Width];
      for (int y = 0; y < Height; y++) {
        for (int x = 0; x < Width; x++) {
          if (rows[y][x / multiple] == ' ') {
            transparent[y, x] = true;
          } else {
            image[y, x] = colors[y];
            transparent[y, x] = false;
          }
        }
      }
    }

    public void Draw(int x, int y, ConsoleCharInfo[,] hiddenBuffer) {
      for (int i = 0; i < Height; i++) {
        for (int j = 0; j < Width; j++) {
          if (!transparent[i, j]) {
            int r = y + i;
            int c = x + j;
            if (c >= 0 && c < 160 && r > 0 && r < 50) {
              if ((c & 1) == 0) {
                hiddenBuffer[r, c >> 1].Foreground = image[i, j];
              } else {
                hiddenBuffer[r, c >> 1].Background = image[i, j];
              }
            }
          }
        }
      }
    }

    public void Draw(int x, int y, ConsoleCharInfo[,] hiddenBuffer, bool leftToRight) {
      if (leftToRight) {
        Draw(x, y, hiddenBuffer);
      } else {
        for (int i = 0; i < Height; i++) {
          for (int j = 0, j2 = Width - 1; j < Width; j++, j2--) {
            if (!transparent[i, j2]) {
              int r = y + i;
              int c = x + j;
              if (c >= 0 && c < 160 && r > 0 && r < 50) {
                if ((c & 1) == 0) {
                  hiddenBuffer[r, c >> 1].Foreground = image[i, j2];
                } else {
                  hiddenBuffer[r, c >> 1].Background = image[i, j2];
                }
              }
            }
          }
        }
      }
    }

    public bool Collide(int x1, int y1, Sprite sprite2, int x2, int y2) {
      if (RectanglesOverlap(x1, y1, Width, Height, x2, y2, sprite2.Width, sprite2.Height)) {
        x2 -= x1;
        y2 -= y1;
        for (int y = 0; y < Height; y++) {
          for (int x = 0; x < Width; x++) {
            if (!transparent[y, x]
                && InRectangle(x, y, x2, y2, sprite2.Width, sprite2.Height)
                && !sprite2.transparent[y - y2, x - x2]) {
              return true;
            }
          }
        }
      }
      return false;
    }

    public bool Collide(
        int x1, int y1, bool leftToRight1, 
        Sprite sprite2, int x2, int y2, bool leftToRight2) {

      if (leftToRight1) {
        if (leftToRight2) {
          if (RectanglesOverlap(x1, y1, Width, Height, x2, y2, sprite2.Width, sprite2.Height)) {
            x2 -= x1;
            y2 -= y1;
            for (int y = 0; y < Height; y++) {
              for (int x = 0; x < Width; x++) {
                if (!transparent[y, x]
                    && InRectangle(x, y, x2, y2, sprite2.Width, sprite2.Height)
                    && !sprite2.transparent[y - y2, x - x2]) {
                  return true;
                }
              }
            }
          }
        } else {
          if (RectanglesOverlap(x1, y1, Width, Height, x2, y2, sprite2.Width, sprite2.Height)) {
            x2 -= x1;
            y2 -= y1;
            int reverseX = sprite2.Width - 1;
            for (int y = 0; y < Height; y++) {
              for (int x = 0; x < Width; x++) {
                if (!transparent[y, x]
                    && InRectangle(x, y, x2, y2, sprite2.Width, sprite2.Height)
                    && !sprite2.transparent[y - y2, reverseX - x + x2]) {
                  return true;
                }
              }
            }
          }
        }
      } else {
        if (leftToRight2) {
          if (RectanglesOverlap(x1, y1, Width, Height, x2, y2, sprite2.Width, sprite2.Height)) {
            x2 -= x1;
            y2 -= y1;
            int rx = Width - 1;
            for (int y = 0; y < Height; y++) {
              for (int x = 0; x < Width; x++) {
                if (!transparent[y, rx - x]
                    && InRectangle(x, y, x2, y2, sprite2.Width, sprite2.Height)
                    && !sprite2.transparent[y - y2, x - x2]) {
                  return true;
                }
              }
            }
          }
        } else {
          if (RectanglesOverlap(x1, y1, Width, Height, x2, y2, sprite2.Width, sprite2.Height)) {
            x2 -= x1;
            y2 -= y1;
            int rx = Width - 1;
            int reverseX = sprite2.Width - 1;
            for (int y = 0; y < Height; y++) {
              for (int x = 0; x < Width; x++) {
                if (!transparent[y, rx - x]
                    && InRectangle(x, y, x2, y2, sprite2.Width, sprite2.Height)
                    && !sprite2.transparent[y - y2, reverseX - x + x2]) {
                  return true;
                }
              }
            }
          }
        }
      }
 
      return false;
    }

    private bool RectanglesOverlap(
        int rect1x, int rect1y, int rect1width, int rect1height,
        int rect2x, int rect2y, int rect2width, int rect2height) {
      return Rect2ContainsRect1(
          rect1x, rect1y, rect1width, rect1height,
          rect2x, rect2y, rect2width, rect2height)
        || Rect2ContainsRect1(
          rect2x, rect2y, rect2width, rect2height,
          rect1x, rect1y, rect1width, rect1height);
    }

    private bool Rect2ContainsRect1(
        int rect1x, int rect1y, int rect1width, int rect1height,
        int rect2x, int rect2y, int rect2width, int rect2height) {
      return InRectangle(rect1x, rect1y, rect2x, rect2y, rect2width, rect2height)
          || InRectangle(rect1x + rect1width, rect1y, rect2x, rect2y, rect2width, rect2height)
          || InRectangle(rect1x, rect1y + rect1height, rect2x, rect2y, rect2width, rect2height)
          || InRectangle(rect1x + rect1width, rect1y + rect1height, rect2x, rect2y, rect2width, rect2height);
    }

    private bool InRectangle(int x, int y, int rectX, int rectY, int width, int height) {
      return x >= rectX && y >= rectY && x < rectX + width && y < rectY + height;
    }
  }
}
