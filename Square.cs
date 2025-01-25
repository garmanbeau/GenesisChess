namespace GenesisChess;

using GenesisChess.Pieces;
using GenesisChess.Enums;

internal class Square
{
    public int Rank { get; set; }
    public int File { get; set; }
    public bool IsOccupied { get; set; }

    public IPiece? Piece { get; set; }

    public override string ToString()
    {
        if (!IsOccupied)
        {
            return $"{DetermineFile(File)} {Rank}";
        }
        else
        { char pieceType, pieceColor;
            if (Piece == null) { throw new NullReferenceException(nameof(Piece)); }
            if (Piece.Color == Color.White)
                pieceColor = 'W';
            else pieceColor = 'B';

            pieceType = DeterminePiece(Piece.Name);
            return $"{pieceColor} {pieceType}";
        }
        //return $"{File} {Rank}";
    }

    private char DetermineFile(int file) { 
        switch (file)
        {
            case 0:
                return 'A';
            case 1:
                return 'B';
            case 2:
                return 'C';
            case 3:
                return 'D';
            case 4:
                return 'E';
            case 5:
                return 'F';
            case 6:
                return 'G';
            case 7:
                return 'H';
            default: return 'X';
        }
    }

    private char DeterminePiece(string pieceName)
    {
        switch (pieceName)
        {
            case PieceName.Pawn:
                return 'P';
            case PieceName.Rook:
                return 'R';
            case PieceName.Knight:
                return 'N';
            case PieceName.Bishop:
                return 'B';
            case PieceName.Queen:
                return 'Q';
            case PieceName.King:
                return 'K';
            default: return 'X';
        }
    }
}
