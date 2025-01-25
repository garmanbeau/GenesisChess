
using GenesisChess.Enums;

namespace GenesisChess.Pieces
{
    internal class King : IPiece
    {
        public King(string color)
        {
            Color = color;
            Name = PieceName.King;
        }

        public string Name { get;} 
        public string Color { get;}
    }
}
