using DominoGame.srv.Models;
using JiuLing.CommonLibs;
using Microsoft.Extensions.Hosting;
using SignalRSwaggerGen.Attributes;
using System.Threading.Tasks;

namespace DominoGame.srv.Hubs
{

    public class GameHub : Hub
    {
		private static readonly object Locker = new();

		private static List<Game> games = new List<Game>();
		public GameHub()
		{


		}
		[HubMethodName("CreateGame")]
		public async Task<int> CreateGame( int numberOfPlayers, int numberOfRounds)
		{
			int hostId;
            var game = new Game();
			lock (Locker)
            {

				do
			    {
				    hostId = Convert.ToInt32(RandomUtils.GetOneByLength(4));
			    } while (games.Any(x => x.HostId == hostId));


				game.HostId = hostId;
				game. NumberOfPlayers = numberOfPlayers;
				game.NumberOfRounds = numberOfRounds;
			  
			    games.Add(game);
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, game.HostId.ToString());
            await Clients.Caller.SendAsync("GameCreated", game);
			return hostId;

		}
		[HubMethodName("JoinGame")]

		public async Task JoinGame(string playerId, string playerName, int hostId)
        {
            var game = games.FirstOrDefault(g => g.HostId == hostId);
            if (game != null && !game.IsFull)
            {
                var player = new Player
                {
                    ConnectionId = playerId,
                    Name = playerName
                };

                game.Players.Add(player);
                await Groups.AddToGroupAsync(Context.ConnectionId, game.HostId.ToString());

                await Clients.Caller.SendAsync("GameJoined", game);

                if (game.IsFull)
                {
                    await StartGame(game);
                }
            }
        }

        private async Task StartGame(Game game)
        {
            var dominoes = GenerateDominoes(0, 9);
            Random rng = new Random();
            dominoes = dominoes.OrderBy(d => rng.Next()).ToList();

            int dominosPerPlayer = dominoes.Count / game.NumberOfPlayers;

            foreach (var player in game.Players)
            {
                player.Hand = dominoes.Take(dominosPerPlayer).ToList();
                dominoes = dominoes.Skip(dominosPerPlayer).ToList();
            }

            game.CurrentRound = 1;
            await Clients.Group(game.HostId.ToString()).SendAsync("GameStarted", game);
        }

		private List<Domino> GenerateDominoes(int min, int max)
        {
            var dominoes = new List<Domino>();
            for (int i = min; i <= max; i++)
            {
                for (int j = i; j <= max; j++)
                {
                    dominoes.Add(new Domino(i, j));
                }
            }
            return dominoes;
        }
		[HubMethodName("PlayDomino")]
		public async Task PlayDomino(int hostId, string playerId, int value1, int value2)
		{
			var game = games.FirstOrDefault(g => g.HostId == hostId);
			var player = game?.Players.FirstOrDefault(p => p.ConnectionId == playerId);

			if (game != null && player != null)
			{
				var domino = player.Hand.FirstOrDefault(d => (d.Value1 == value1 && d.Value2 == value2) || (d.Value1 == value2 && d.Value2 == value1));

				if (domino != null)
				{
					var firstPlayableValue = GetLinkListFirstPlayableValue(game.Board);
					var lastPlayableValue = GetLinkListLastPlayableValue(game.Board);

					var placed=	AddDomino(game,domino); // Place the domino on the board using the new logic
					if (placed)
					{

						player.Hand.Remove(domino);
						await Clients.Group(hostId.ToString()).SendAsync("DominoPlayed", game, player, domino);

						if (player.Hand.Count == 0)
						{
							player.Score += game.Players.Sum(p => p.Hand.Sum(d => d.TotalValue));
							await Clients.Group(hostId.ToString()).SendAsync("RoundEnded", game, player);

							if (game.CurrentRound < game.NumberOfRounds)
							{
								game.CurrentRound++;
								await StartGame(game);
							}
							else
							{
								await Clients.Group(hostId.ToString()).SendAsync("GameEnded", game);
							}
						}
					}
					else
					{
						await Clients.Caller.SendAsync("InvalidMove", "You must skip your turn.");
					}
				}
			}
		}

		private int? GetLinkListLastPlayableValue(LinkedList<Domino> board)
		{
			var firstDomino = board.Last?.Value;
			var nextDomino = board.Last?.Previous?.Value;
			if (firstDomino?.Value1 == nextDomino?.Value1 || firstDomino?.Value1 == nextDomino?.Value2)
			{
				return firstDomino?.Value2;
			}
			if (firstDomino?.Value2 == nextDomino?.Value1 || firstDomino?.Value2 == nextDomino?.Value2)
			{
				return firstDomino?.Value1;
			}
			return null;
		}

		private int? GetLinkListFirstPlayableValue(LinkedList<Domino> board)
		{
			var firstDomino = board.First?.Value;
			var nextDomino = board.First?.Next?.Value;
			if (firstDomino?.Value1 == nextDomino?.Value1 || firstDomino?.Value1 == nextDomino?.Value2)
			{
				return firstDomino?.Value2;
			}
			if (firstDomino?.Value2 == nextDomino?.Value1 || firstDomino?.Value2 == nextDomino?.Value2)
			{
				return firstDomino?.Value1;
			}
			return null;
		}

		private bool AddDomino( Game game,Domino domino)
		{
			bool result = false;
		    int	placedOrder = game.Board.Count;
				domino.PlaceOrder = placedOrder;

			if (domino.Value1 == GetLinkListFirstPlayableValue(game.Board) || domino.Value2 == GetLinkListFirstPlayableValue(game.Board))
			{
				game.Board.AddFirst(domino);
				result = true;
			}
			else if (domino.Value1 == GetLinkListLastPlayableValue(game.Board) || domino.Value2 == GetLinkListLastPlayableValue(game.Board))
			{
				game.Board.AddLast(domino);
				result = true;

			}

			game.ValueFisrt = GetLinkListFirstPlayableValue(game.Board);
			game.ValueLast = GetLinkListLastPlayableValue(game.Board);
			return result;
		}


		public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception?? throw new ArgumentNullException());
        }
    }

}
