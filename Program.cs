using  GenesisChess;
using GenesisChess.Pieces;
using GenesisChess.Enums;
using System.Diagnostics;
//using File = GenesisChess.Enums.File;

static void Print2DArray<T>(T[,] matrix)
{
    for (int i = 0; i < matrix.GetLength(0); i++)
    {
        for (int j = 0; j < matrix.GetLength(1); j++)
        {
            Console.Write(matrix[i, j] + "\t");
        }
        Console.WriteLine();
    }
}

static int[] linearSearch(Square[,] arr, int rank, int file)
{
    for (int i = 0; i < arr.GetLength(0); i++)
    {
        for (int j = 0; j < arr.GetLength(1); j++)
        {
            if (arr[i, j].Rank == rank && arr[i,j].File == file)
            {
                return new int[] { i, j };
            }
        }
    }
    return new int[] { -1, -1 };
}
static int DetermineFileFromChar(char file)
{
    switch (file)
    {
        case 'A':
            return 0;
        case 'B':
            return 1;
        case 'C':
            return 2;
        case 'D':
            return 3;
        case 'E':
            return 4;
        case 'F':
            return 5;
        case 'G':
            return 6;
        case 'H':
            return 7;
        default: return -1;
    }
}


Square[,] board = new Square[8,8];
//instantiates board such that [0,0] corresponds to A8 and [7,7] Corresponds to H1
//The first place corresponds to rank with 0 = rank 8
//The second place corresponds to file with 0 = A file
for (int i = 0; i < 8; i++) {
    int k = 8;
    for (int j = 0; j < 8; j++)
    {
        board[j, i] = new Square();
        board[j, i].File = i;
        board[j, i].Rank = k--;
    }
   
}

IPiece piece;
piece = new King(Color.White);
board[0,0].Piece = piece;
board[0,0].IsOccupied = true;

Debug.WriteLine("Test");
Console.WriteLine(piece.Name);
////File.A.ToString();

//foreach (Square square in board)
//{
//    Console.WriteLine(square.Rank + " " + square.File);
//}
Print2DArray(board);

int input = 0, input1 = 0; 

while (input != 9)
{
    Console.WriteLine("File: ");
    char input2 = Convert.ToChar(Console.ReadLine());


    Console.WriteLine("Rank: ");
    input1 = Convert.ToInt32(Console.ReadLine());

    int[] x = linearSearch(board, input1, DetermineFileFromChar(input2));


    foreach (int x2 in x)
    {
        Console.Write(x2.ToString() + '\t');
    }
    Console.WriteLine(board[x[0], x[1]].ToString());

    Console.WriteLine("Again? 9 to stop");
    input = Convert.ToInt32(Console.ReadLine());
}
