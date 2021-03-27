using System;
using System.Collections.Generic;
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
        private const string ip = "209.97.169.233";
        private const int port = 14003;
        private const int MaxDepth = 10;
        private static readonly Board cell = new();

        private static IEnumerable<((int, int), char)> DoMoves((char, char) position, char color)
        {
            var changedCells = cell.GetFlips(position, color);
            var oldState = new List<((int, int), char)>(changedCells.Count + 1);
            foreach (var pos in changedCells)
            {
                oldState.Add((pos, cell.GetValue(pos)));
                cell.SetValue(pos, color);
            }

            var location = (Board.GetRowID(position.Item2), Board.GetColumnID(position.Item1));
            oldState.Add((location, cell.GetValue(location)));
            cell.SetValue(location, color);
            return oldState;
        }

        private static void UndoMoves(IEnumerable<((int, int), char)> oldState)
        {
            foreach (var (position, value) in oldState)
                cell.SetValue(position, value);
        }

        private static int Heuristic(char color, ICollection<(int, int)> victoryCells)
        {
            var total = 0;
            var oppColor = color == 'B' ? 'W' : 'B';
            for (var i = 0; i < 8; i++)
            for (var j = 0; j < 8; j++)
                if (cell.board[i, j] == color)
                {
                    if (victoryCells.Contains((i, j)))
                        total += 1000;
                    else total += Board.weights[i * 8 + j];
                }
                else if (cell.board[i, j] == oppColor)
                {
                    if (victoryCells.Contains((i, j)))
                        total -= 1000;
                    else total -= Board.weights[i * 8 + j];
                }

            return total;
        }

        private static int NegamaxHelper(char color, int depth, int alpha, int beta,
            ICollection<(int, int)> victoryCells)
        {
            if (depth == 0)
                return Heuristic(color, victoryCells);
            var moves = cell.GetAllPossibleMoves(color);
            if (moves.Count == 0)
            {
                if (cell.GetAllPossibleMoves(color == 'W' ? 'B' : 'W').Count == 0)
                    return Heuristic(color, victoryCells);
                var val = -NegamaxHelper(color == 'W' ? 'B' : 'W', depth - 1, -beta, -alpha, victoryCells);
                if (val >= beta)
                    return val;
                if (val > alpha)
                    alpha = val;
            }
            else
            {
                foreach (var nextMove in moves)
                {
                    var oldState = DoMoves(nextMove, color);
                    var val = -NegamaxHelper(color == 'W' ? 'B' : 'W', depth - 1, -beta, -alpha,
                        victoryCells);
                    UndoMoves(oldState);
                    if (val >= beta)
                        return val;
                    if (val > alpha)
                        alpha = val;
                }
            }

            return alpha;
        }

        private static string Negamax(char color, int depth, ICollection<(int, int)> victoryCells)
        {
            var alpha = -1000000;
            var beta = 1000000;
            var move = string.Empty;
            foreach (var nextMove in cell.GetAllPossibleMoves(color))
            {
                var oldState = DoMoves(nextMove, color);
                var val = -NegamaxHelper(color == 'W' ? 'B' : 'W', depth - 1, -beta, -alpha, victoryCells);
                UndoMoves(oldState);
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

        private static int NegaScoutHelper(char color, int depth, int alpha, int beta,
            ICollection<(int, int)> victoryCells)
        {
            if (depth == MaxDepth || !cell.IsPlayable(color))
                return Heuristic(color, victoryCells);
            var bestScore = -1000000;
            var adaptiveBeta = beta;
            foreach (var nextMove in cell.GetAllPossibleMoves(color))
            {
                var oldStates = DoMoves(nextMove, color);
                var currentScore = -NegaScoutHelper(color == 'B' ? 'W' : 'B', depth + 1, -adaptiveBeta,
                    -Math.Max(alpha, bestScore), victoryCells);
                if (currentScore > bestScore)
                {
                    if (adaptiveBeta == beta || depth >= MaxDepth - 2)
                        bestScore = currentScore;
                    else
                        bestScore = -NegaScoutHelper(color == 'B' ? 'W' : 'B', depth + 1, -beta,
                            -currentScore,
                            victoryCells);

                    if (bestScore >= beta)
                    {
                        UndoMoves(oldStates);
                        return Heuristic(color, victoryCells);
                    }
                }

                UndoMoves(oldStates);
                adaptiveBeta = Math.Max(alpha, bestScore) + 1;
            }

            return bestScore;
        }

        private static string NegaScout(char color, int depth, ICollection<(int, int)> victoryCells)
        {
            var alpha = -1000000;
            var beta = 1000000;
            var bestScore = -1000000;
            var bestMove = "NULL";
            var adaptiveBeta = beta;
            var nextMoves = cell.GetAllPossibleMoves(color);
            if (nextMoves.Count == 0)
                return "NULL";
            foreach (var move in nextMoves)
            {
                var oldStates = DoMoves(move, color);
                var currentScore = -NegaScoutHelper(color == 'B' ? 'W' : 'B', depth + 1, -adaptiveBeta,
                    -Math.Max(alpha, bestScore), victoryCells);
                if (currentScore > bestScore)
                {
                    if (adaptiveBeta == beta || depth >= MaxDepth - 2)
                    {
                        bestScore = currentScore;
                        bestMove = string.Concat(move.Item1, move.Item2);
                    }
                    else
                    {
                        bestScore = -NegaScoutHelper(color == 'B' ? 'W' : 'B', depth + 1, -beta,
                            -currentScore,
                            victoryCells);
                        bestMove = string.Concat(move.Item1, move.Item2);
                    }

                    if (bestScore >= beta)
                    {
                        UndoMoves(oldStates);
                        return bestMove;
                    }
                }

                UndoMoves(oldStates);
                adaptiveBeta = Math.Max(alpha, bestScore) + 1;
            }

            return bestMove;
        }

        private static string Bot(ICollection<(int, int)> victoryCell, string you)
        {
            return NegaScout(you == "BLACK" ? 'B' : 'W', 0, victoryCell);
        }

        private static string CallBot(string gameInfo)
        {
            var lines = gameInfo.Split('\n');
            cell.Update(lines.Skip(3).Take(8).ToList());
            var you = lines[12];
            var victoryCells = lines[1].Split(' ')
                .Where(item => cell.GetValue((item[0],item[1])) == 'E')
                .Select(item => (Board.GetRowID(item[1]), Board.GetColumnID(item[0])))
                .ToList();
            return Bot(victoryCells, you);
        }

        private static void Main()
        {
            var tcpClient = new TcpClient();
            if (!tcpClient.ConnectAsync(ip, port).Wait(3000))
                throw new InvalidProgramException("Cannot connect to server");
            Console.WriteLine("Connected to server");
            var stream = tcpClient.GetStream();
            var buffer = new byte[256];
            while (true)
            {
                var bytes = stream.Read(buffer, 0, buffer.Length);
                var response = Encoding.ASCII.GetString(buffer, 0, bytes);
                Console.WriteLine(response);
                if (!Regex.IsMatch(response, "^victory_cell"))
                    break;
                stream.Write(Encoding.ASCII.GetBytes(CallBot(response)));
            }

            tcpClient.Close();
        }
    }
}