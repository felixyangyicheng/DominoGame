namespace DominoGame.srv.Models
{
    public class Player
    {
        public string ConnectionId { get; set; } = "";
        public string Name { get; set; } = "";
        public List<Domino> Hand { get; set; } = [];
        public int Score { get; set; } = 0;
        public int PlayedCount { get; set; }
        public int PassedCount { get; set; }
        public bool IsConnected { get; set; } = true;
        public int RemainingPips => Hand.Sum(d => d.TotalValue);
    }
}
