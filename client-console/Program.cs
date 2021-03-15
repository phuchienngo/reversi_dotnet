using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Local
// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming

namespace client_console
{
    internal static class Program
    {
        private const string ip = /*"209.97.169.233"*/ "209.97.169.233";
        private const int port = 14003;
        private static readonly Random rd = new();
        private static List<string> history;
        private static readonly List<string> openings = new();
        private static bool usingOpeningList = true;

        private static void GetOpponentMove(Board cell)
        {
            var exceptList = new List<string> {"d4", "e4", "d5", "e5"};
            foreach (var str in "12345678".SelectMany(r =>
                from c in "abcdefgh"
                let str = "" + c + r
                where cell.GetValue((c, r)) != 'E' && !exceptList.Contains(str) && !history.Contains(str)
                select str))
                history.Add(str);
        }

        private static List<string> GetMoveFromOpening()
        {
            var strHistory = string.Join("", history);
            var availableMoves = new List<string>();
            var expireMoves = new List<string>();
            foreach (var move in openings)
            {
                var str = move.ToLower();
                if (str.Length <= strHistory.Length)
                {
                    expireMoves.Add(move);
                    continue;
                }

                var isMatch = true;
                var i = 0;
                while (i < strHistory.Length && isMatch)
                    if (str[i] != strHistory[i])
                    {
                        expireMoves.Add(move);
                        isMatch = false;
                    }
                    else
                    {
                        i++;
                    }

                if (isMatch)
                    availableMoves.Add(str.Substring(i, 2));
            }

            foreach (var item in expireMoves) openings.Remove(item);

            return availableMoves.Count > 0 ? availableMoves : null;
        }

        private static IEnumerable<((int, int), char)> DoMoves(Board cell, (char, char) position, char color)
        {
            var changedCells = cell.GetFlips(position, color);
            var oldState = new List<((int, int), char)>(changedCells.Count + 1);
            foreach (var pos in changedCells)
            {
                oldState.Add((pos, cell.GetValue(pos)));
                cell.SetValue(pos, color);
            }

            var r = cell.GetRowID(position.Item2);
            var c = cell.GetColumnID(position.Item1);
            oldState.Add(((r, c), cell.GetValue((r, c))));
            cell.SetValue((r, c), color);
            return oldState;
        }

        private static void UndoMoves(Board cell, IEnumerable<((int, int), char)> oldState)
        {
            foreach (var (valueTuple, item2) in oldState)
                cell.SetValue(valueTuple, item2);
        }

        private static int Heuristic(Board cell, char color, string[] victoryCells)
        {
            var total = 0;
            var oppColor = color == 'B' ? 'W' : 'B';
            for (var i = 0; i < 64; i++)
                if (cell.board[i / 8, i % 8] == color)
                    total += Board.weights[i];
                else if (cell.board[i / 8, i % 8] == oppColor)
                    total -= Board.weights[i];
            foreach (var item in victoryCells)
                if (cell.GetValue((item[0], item[1])) == color) {
                    total += 10;
                }
            return total * 2 + 3 * (cell.CountColor(color) - cell.CountColor(oppColor));
        }

        private static int NegamaxHelper(Board cell, char color, int depth, int alpha, int beta, string[] victoryCells)
        {
            if (depth == 0)
                return Heuristic(cell, color, victoryCells);
            var moves = cell.GetAllPossibleMoves(color);
            if (moves.Count == 0)
            {
                if (cell.GetAllPossibleMoves(color == 'W' ? 'B' : 'W').Count == 0)
                    return Heuristic(cell, color, victoryCells);
                var val = -NegamaxHelper(cell, color == 'W' ? 'B' : 'W', depth - 1, -beta, -alpha, victoryCells);
                if (val >= beta)
                    return val;
                if (val > alpha)
                    alpha = val;
            }
            else
            {
                foreach (var nextMove in moves)
                {
                    var oldState = DoMoves(cell, nextMove, color);
                    var val = -NegamaxHelper(cell, color == 'W' ? 'B' : 'W', depth - 1, -beta, -alpha, victoryCells);
                    UndoMoves(cell, oldState);
                    if (val >= beta)
                        return val;
                    if (val > alpha)
                        alpha = val;
                }
            }

            return alpha;
        }

        private static string Negamax(Board cell, char color, int depth, string[] victoryCells)
        {
            var alpha = -65;
            var beta = 65;
            var move = string.Empty;
            foreach (var nextMove in cell.GetAllPossibleMoves(color))
            {
                var oldState = DoMoves(cell, nextMove, color);
                var val = -NegamaxHelper(cell, color == 'W' ? 'B' : 'W', depth - 1, -beta, -alpha, victoryCells);
                UndoMoves(cell, oldState);
                if (val >= beta)
                    return "" + nextMove.Item1 + nextMove.Item2;
                if (val > alpha)
                {
                    alpha = val;
                    move = "" + nextMove.Item1 + nextMove.Item2;
                }
            }

            return move;
        }

        private static int NegaScoutHelper(Board cell, char color, int depth, int alpha, int beta,
            string[] victoryCells)
        {
            if (depth == 0 || !cell.IsPlayable(color))
                return Heuristic(cell, color, victoryCells);
            var bestScore = int.MinValue;
            var adaptiveBeta = beta;
            var allPossibleMoves = cell.GetAllPossibleMoves(color);
            if (allPossibleMoves.Count == 0)
                return Heuristic(cell, color, victoryCells);
            foreach (var nextMove in allPossibleMoves)
            {
                var oldStates = DoMoves(cell, nextMove, color);
                var currentScore = -NegaScoutHelper(cell, color == 'B' ? 'W' : 'B', depth - 1, -adaptiveBeta,
                    -Math.Max(alpha, bestScore), victoryCells);
                if (currentScore > bestScore)
                {
                    if (adaptiveBeta == beta || depth < 3)
                        bestScore = currentScore;
                    else
                        bestScore = -NegaScoutHelper(cell, color == 'B' ? 'W' : 'B', depth - 1, -beta, -currentScore,
                            victoryCells);

                    if (bestScore >= beta)
                    {
                        UndoMoves(cell, oldStates);
                        return Heuristic(cell, color, victoryCells);
                    }
                }

                UndoMoves(cell, oldStates);
                adaptiveBeta = Math.Max(alpha, bestScore) + 1;
            }

            return bestScore;
        }

        private static string NegaScout(Board cell, char color, int depth, string[] victoryCells)
        {
            var alpha = int.MinValue;
            var beta = int.MaxValue;
            var bestScore = int.MinValue;
            var bestMove = "NULL";
            var adaptiveBeta = beta;
            var nextMoves = cell.GetAllPossibleMoves(color);
            if (nextMoves.Count == 0)
                return "NULL";
            foreach (var move in nextMoves)
            {
                var oldStates = DoMoves(cell, move, color);
                var currentScore = -NegaScoutHelper(cell, color == 'B' ? 'W' : 'B', depth - 1, -adaptiveBeta,
                    -Math.Max(alpha, bestScore), victoryCells);
                if (currentScore > bestScore)
                {
                    if (adaptiveBeta == beta || depth < 3)
                    {
                        bestScore = currentScore;
                        bestMove = string.Join("",new[]{move.Item1,move.Item2});
                    }
                    else
                    {
                        bestScore = -NegaScoutHelper(cell, color == 'B' ? 'W' : 'B', depth - 1, -beta, -currentScore,
                            victoryCells);
                        bestMove = string.Join("",new[]{move.Item1,move.Item2});
                    }

                    if (bestScore >= beta)
                    {
                        UndoMoves(cell, oldStates);
                        return bestMove;
                    }
                }

                UndoMoves(cell, oldStates);
                adaptiveBeta = Math.Max(alpha, bestScore) + 1;
            }

            return bestMove;
        }

        private static string Bot(string[] victoryCell, Board cell, string you)
        {
            var color = you == "BLACK" ? 'B' : 'W';
            if (usingOpeningList)
            {
                GetOpponentMove(cell);
                var availableMoves = GetMoveFromOpening();
                if (availableMoves != null)
                    return availableMoves[rd.Next(0, availableMoves.Count - 1)];
                usingOpeningList = false;
                openings.Clear();
            }

            return NegaScout(cell, color, 8, victoryCell);
        }

        private static string CallBot(string gameInfo)
        {
            var lines = gameInfo.Split('\n');
            var victoryCell = lines[1].Split(' ');
            var cell = new Board();
            cell.Update(new List<string>(lines).GetRange(3, 8).ToArray());
            var you = lines[12];
            var result = Bot(victoryCell, cell, you);
            if (result != "NULL")
            {
                history.Add(result);
                return result;
            }

            return "NULL";
        }
        private static void Main()
        {
            var tcpClient = new TcpClient();
            if (!tcpClient.ConnectAsync(ip, port).Wait(3000))
                throw new InvalidProgramException("Cannot connect to server");
            Console.WriteLine("Connected to server");
            var stream = tcpClient.GetStream();
            history = new List<string>();
            try
            {
                var streamReader = new StreamReader("opening.txt");
                while (!streamReader.EndOfStream) openings.Add(streamReader.ReadLine());
                streamReader.Dispose();
                streamReader.Close();
            }
            catch
            {
                Console.WriteLine("Opening list not found");
                usingOpeningList = false;
            }

            while (true)
            {
                var data = new byte[256];
                var bytes = stream.Read(data, 0, data.Length);
                var response = Encoding.ASCII.GetString(data, 0, bytes);
                Console.WriteLine(response);
                if (!Regex.IsMatch(response, "^victory_cell"))
                    break;
                Console.WriteLine("Your turn ->\n");
                var datas = Encoding.ASCII.GetBytes(CallBot(response));
                stream.Write(datas, 0, datas.Length);
                Console.WriteLine("Your opponent turn ->\n");
            }

            tcpClient.Close();
        }
    }
}