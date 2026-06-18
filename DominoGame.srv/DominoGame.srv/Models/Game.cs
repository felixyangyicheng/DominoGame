using System.Text.Json.Serialization;

namespace DominoGame.srv.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GameStatus
    {
        Waiting,
        Playing,
        Ended
    }

    public class Game
    {
        public int HostId { get; set; }
        public int NumberOfPlayers { get; set; }
        public int MaxPip { get; set; } = 9;
        public int TotalDominoes { get; set; }
        public int DominosPerPlayer { get; set; }
        public int BoneyardCount { get; set; }
        public List<Player> Players { get; set; } = [];
        public List<Domino> Board { get; set; } = [];
        public int CurrentPlayerIndex { get; set; }
        public GameStatus Status { get; set; } = GameStatus.Waiting;
        public int? ValueFirst { get; set; }
        public int? ValueLast { get; set; }
        public string? WinnerConnectionId { get; set; }
        public string? WinnerName { get; set; }
        public string? EndReason { get; set; }
        public DateTime? EndedAt { get; set; }
        public List<PlayerStatistic> Statistics { get; set; } = [];

		public bool IsFull => Players.Count == NumberOfPlayers;
    }

    public class PlayerStatistic(string playerName, int score, int remainingDominoes, int remainingPips, int playedCount, int passedCount, bool isWinner)
    {
        public string PlayerName { get; set; } = playerName;
        public int Score { get; set; } = score;
        public int RemainingDominoes { get; set; } = remainingDominoes;
        public int RemainingPips { get; set; } = remainingPips;
        public int PlayedCount { get; set; } = playedCount;
        public int PassedCount { get; set; } = passedCount;
        public bool IsWinner { get; set; } = isWinner;
    }
}
