using System.Collections.Generic;
using System.Linq;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace client_console
{
    public class Board
    {
        public readonly char[,] board;
        public static readonly int[] weights = new[]
        {
            100, -1, 5, 2, 2, 5, -1, 100,
            -1, -10,1, 1, 1, 1,-10, -1,
            5 , 1,  1, 1, 1, 1,  1,  5, 
            2 , 1,  1, 0, 0, 1,  1,  2,
            2 , 1,  1, 0, 0, 1,  1,  2,
            5 , 1,  1, 1, 1, 1,  1,  5,
            -1,-10, 1, 1, 1, 1,-10, -1,
            100, -1, 5, 2, 2, 5, -1, 100
        };
        public Board()
        {
            board = new char[8, 8];
            for (var i = 0; i < 8; i++)
            for (var j = 0; j < 8; j++)
                board[i, j] = 'E';
            board[3, 3] = board[4, 4] = 'W';
            board[3, 4] = board[4, 3] = 'B';
        }

        public int GetWeightValue((char, char) position)
        {
            return weights[8 * GetRowID(position.Item2) + GetColumnID(position.Item1)];
        }
        public int GetRowID(char numericCharacter)
        {
            return numericCharacter - '1';
        }

        public int GetColumnID(char numericCharacter)
        {
            return numericCharacter - 'a';
        }

        public void SetValue((int, int) position, char value)
        {
            board[position.Item1, position.Item2] = value;
        }

        public char GetValue((int, int) position)
        {
            return board[position.Item1, position.Item2];
        }

        public char GetValue((char, char) position)
        {
            return board[GetRowID(position.Item2), GetColumnID(position.Item1)];
        }

        private static bool IsOutOfRange(int r, int c)
        {
            return r < 0 || r > 7 || c < 0 || c > 7;
        }

        private bool IsDirectionPlaceable((char, char) position, (int, int) direction, char color)
        {
            if (GetValue(position) != 'E')
                return false;
            var rowId = GetRowID(position.Item2);
            var columnId = GetColumnID(position.Item1);
            var rowDirection = direction.Item1;
            var columnDirection = direction.Item2;
            for (var i = 1; i < 9; i++)
            {
                var r = rowId + i * rowDirection;
                var c = columnId + i * columnDirection;
                if (IsOutOfRange(r, c) || board[r, c] == 'E')
                    return false;
                if (board[r, c] == color)
                    return i != 1;
            }

            return false;
        }

        public bool IsPlaceable((char, char) position, char color)
        {
            return IsDirectionPlaceable(position, (1, 0), color) ||
                   IsDirectionPlaceable(position, (1, 1), color) ||
                   IsDirectionPlaceable(position, (0, 1), color) ||
                   IsDirectionPlaceable(position, (-1, 1), color) ||
                   IsDirectionPlaceable(position, (-1, 0), color) ||
                   IsDirectionPlaceable(position, (-1, -1), color) ||
                   IsDirectionPlaceable(position, (0, -1), color) ||
                   IsDirectionPlaceable(position, (1, -1), color);
        }

        public bool IsPlayable(char color)
        {
            return "12345678".Any(r => "abcdefgh".Any(c => IsPlaceable((c, r), color)));
        }

        public List<(char, char)> GetAllPossibleMoves(char color)
        {
            var result = new List<(char, char)>();
            foreach (var r in "12345678")
                result.AddRange(from c in "abcdefgh" where IsPlaceable((c, r), color) select (c, r));
            result.Sort((item1, item2) =>
            {
                var a = this.GetWeightValue(item1);
                var b = this.GetWeightValue(item2);
                return a < b ? 1 : a == b ? 0 : -1;
            });
            return result;
        }

        private IEnumerable<(int, int)> GetDirectionFlips((char, char) position, (int, int) direction, char color)
        {
            if (GetValue(position) != 'E')
                return new List<(int, int)>();
            var result = new List<(int, int)>();
            var rowId = GetRowID(position.Item2);
            var columnId = GetColumnID(position.Item1);
            for (var i = 1; i < 9; i++)
            {
                var r = rowId + i * direction.Item1;
                var c = columnId + i * direction.Item2;
                if (r < 0 || r > 7 || c < 0 || c > 7 || board[r, c] == 'E')
                    return new List<(int, int)>();
                if (board[r, c] == color)
                    return result;
                result.Add((r, c));
            }

            return null;
        }

        public List<(int, int)> GetFlips((char, char) position, char color)
        {
            var result = new List<(int, int)>();
            result.AddRange(GetDirectionFlips(position, (1, 0), color));
            result.AddRange(GetDirectionFlips(position, (1, 1), color));
            result.AddRange(GetDirectionFlips(position, (0, 1), color));
            result.AddRange(GetDirectionFlips(position, (-1, 1), color));
            result.AddRange(GetDirectionFlips(position, (-1, 0), color));
            result.AddRange(GetDirectionFlips(position, (-1, -1), color));
            result.AddRange(GetDirectionFlips(position, (0, -1), color));
            result.AddRange(GetDirectionFlips(position, (1, -1), color));
            return result;
        }

        public void Update(string[] cellLines)
        {
            for (var r = 0; r < 8; r++)
            {
                var cells = cellLines[r].Split(' ');
                for (var c = 0; c < 8; c++)
                    board[r, c] = cells[c].Trim()[0];
            }
        }

        public int CountColor(char color)
        {
            var count = 0;
            foreach (var item in board)
                if (item == color)
                    count++;
            return count;
        }
    }
}