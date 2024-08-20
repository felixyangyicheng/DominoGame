namespace DominoGame.srv.Models
{
    public class Game
    {
        public int HostId { get; set; }
        public int NumberOfPlayers { get; set; }
        public int NumberOfRounds { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public LinkedList<Domino> Board { get; set; } = new LinkedList<Domino>();
        public int CurrentRound { get; set; } = 0;
        public int? ValueFisrt { get; set; }
        public int? ValueLast { get; set; }

		public bool IsFull => Players.Count == NumberOfPlayers;
    }
}
