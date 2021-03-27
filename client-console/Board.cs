using System.Collections.Generic;
using System.Linq;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace client_console
{
    public class Board
    {
        public static readonly int[] weights =
        {
            200, -25, 20, 20, 20, 20, -25, 200,
            -25, -50, 5, 5, 5, 5, -50, -25,
            20, 5, 1, 1, 1, 1, 5, 20,
            20, 5, 1, 1, 1, 1, 5, 20,
            20, 5, 1, 1, 1, 1, 5, 20,
            20, 5, 1, 1, 1, 1, 5, 20,
            -25, -50, 5, 5, 5, 5, -50, -25,
            200, -25, 20, 20, 20, 20, -25, 200
        };

        public readonly char[,] board;

        public Board()
        {
            board = new char[8, 8];
        }

        public void Update(IEnumerable<string> cell)
        {
            var i = 0;
            foreach (var cells in cell.Select(row => row.Split(' ')))
            {
                for (var c = 0; c < 8; c++)
                    board[i, c] = cells[c][0];
                i++;
            }
        }

        private static int GetWeightValue((char, char) position)
        {
            return weights[8 * GetRowID(position.Item2) + GetColumnID(position.Item1)];
        }

        public static int GetRowID(char numericCharacter)
        {
            return numericCharacter - '1';
        }

        public static int GetColumnID(char numericCharacter)
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

        private bool IsPlaceable((char, char) position, char color)
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
                var a = GetWeightValue(item1);
                var b = GetWeightValue(item2);
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
    }
}