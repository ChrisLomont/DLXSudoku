using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.CompilerServices;

namespace Lomont.DLXSudoku
{

    class Sudoku
    {
        public int[,] board = new int[9, 9];
    }

    class Program
    {
        public static List<Sudoku> FromFile(string filename)
        {
            var sudoku = new Sudoku();
            var sudokus = new List<Sudoku>();
            Regex digits = new Regex("\\d+");
            var lineCount = 0;
            var lineNumber = 0;
            foreach (var line in File.ReadAllLines(filename))
            {
                ++lineNumber;
                // read different styles:
                if (line.Trim().StartsWith("#"))
                    continue; // comment
                else if (line.Length == 81)
                {
                    // puzzle is entire line
                    var ss = new Sudoku();
                    for (var index = 0; index < 81; ++index)
                    {
                        var (i,j) = (index % 9,index/9);
                        var c = line[index];
                        ss.board[i, j] = (c == '.') ? 0 : c - '0';
                    }
                    sudokus.Add(ss);
                }
                else if (line.Length == 9 && digits.IsMatch(line))
                {
                    for (var i = 0 ; i < 9; ++i)
                        sudoku.board[i, lineCount] = line[i] - '0';
                    ++lineCount;
                    if (lineCount == 9)
                    {
                        lineCount = 0;
                        sudokus.Add(sudoku);
                        sudoku = new Sudoku();
                    }
                }
                else
                {
                    Console.WriteLine($"ERROR: unknown format at line number {lineNumber}, starts with {line.Substring(0,Math.Min(line.Length,10))}");
                }
            }

            return sudokus;
        }
        public static void Print(Sudoku sudoku)
        {
            for (var i = 0; i < 9; ++i)
            {
                for (var j = 0; j < 9; ++j)
                    Console.Write(sudoku.board[i,j]);
                Console.WriteLine();
            }
        }
        public static (int numSolutions, double totals, double solves) Solve(Sudoku sudoku)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var start = stopwatch.ElapsedTicks;
            var dlx = new DancingLinksSolver();

            // 324 dlx columns : 
            // 81 of: Cid = col i has digit d
            for (var i = 1 ; i <= 9; ++i)
            for (var d = 1; d <= 9; ++d)
                dlx.AddColumn($"C{i}{d}");
            // 81 of: Rjd = row j has digit d
            for (var j = 1; j <= 9; ++j)
            for (var d = 1; d <= 9; ++d)
                dlx.AddColumn($"R{j}{d}");
            // 81 of: Gijd = group col i row j has digit d
            for (var i = 1; i <= 3; ++i)
            for (var j = 1; j <= 3; ++j)
            for (var d = 1; d <= 9; ++d)
                dlx.AddColumn($"G{i}{j}{d}");
            // 81 of: cell Fij filled
            for (var i = 1; i <= 9; ++i)
            for (var j = 1; j <= 9; ++j)
                dlx.AddColumn($"F{i}{j}");

            // a few more columns, one for each initial hint, marked Hijd
            for (var i = 1; i <= 9; ++i)
            for (var j = 1; j <= 9; ++j)
            {
                var d = sudoku.board[i - 1, j - 1];
                if (d != 0)
                    dlx.AddColumn($"H{i}{j}{d}");
            }


            // dlx rows:
            // each is of form "digit d in position i,j"
            for (var d = 1; d <= 9; ++d)
            for (var i = 1; i <= 9; ++i)
            for (var j = 1; j <= 9; ++j)
            {
                dlx.NewRow();
                dlx.SetColumn($"C{i}{d}"); // col i has digit d
                dlx.SetColumn($"R{j}{d}"); // row j has digit d
                dlx.SetColumn($"G{1+(i-1)/3}{1+(j-1)/3}{d}"); // group col i row j has digit d
                dlx.SetColumn($"F{i}{j}"); // cell i,j filled

                // if this move already made, mark the P constraint
                if (sudoku.board[i-1,j-1] == d)
                    dlx.SetColumn($"H{i}{j}{d}");
            }

            // listen to solutions
            dlx.SolutionListener += (number, dequeueNumber, solution) =>
            {
                // solution is triples (or sometimes a quadruple) Rjd Cid Fij, sometimes Hijd
                // get col Cid, row Rid, and digit d
                foreach (var triple in solution)
                {
                    var col = triple.FirstOrDefault(s => s[0] == 'C');
                    var row = triple.FirstOrDefault(s => s[0] == 'R');
                    Debug.Assert(col != null && row != null && row[2] == col[2]);
                    var c = col[1] - '0';
                    var r = row[1] - '0';
                    var d = col[2] - '0';
                    if (sudoku.board[c - 1, r - 1] == 0)
                        sudoku.board[c - 1, r - 1] = d;
                    else
                        Debug.Assert(sudoku.board[c - 1, r - 1] == d);
                }
                return true; // continues enumeration if true
            };

            var start2 = stopwatch.ElapsedTicks;

            dlx.Solve();

            var end  = stopwatch.ElapsedTicks;

            // times in seconds
            var totalS = (double)(end - start) / Stopwatch.Frequency;
            var solveS = (double)(end - start2) / Stopwatch.Frequency;

            return ((int)dlx.NumSolutions, totalS, solveS);
        }


        static void Main(string[] args)
        {
            Console.WriteLine("Sudoku solver");
            var sudokus = FromFile(@"../../../sudokus.txt");
            Console.WriteLine($"{sudokus.Count} sudokus");
            foreach (var sudoku in sudokus)
            {
                Print(sudoku);
                Console.WriteLine("-------------");
                var (numSolutions, totalS, solveS) = Solve(sudoku);
                Print(sudoku);

                // convert to us
                var solveUs = (solveS * 1e6);
                var totalUs = (totalS * 1e6);

                Console.WriteLine($"Solns {numSolutions}  total us {totalUs:F1}  solve us {solveUs:F1}");
                Console.WriteLine();
                Console.WriteLine("-------------");
            }

            Console.WriteLine("Done!");
        }
    }
}
