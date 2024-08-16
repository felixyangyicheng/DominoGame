namespace DominoGame.srv.Hubs
{


    public class GameHub : Hub
    {
        private static List<Game> games = new List<Game>();

        public async Task CreateGame(string hostId, int numberOfPlayers, int numberOfRounds)
        {
            var game = new Game
            {
                HostId = hostId,
                NumberOfPlayers = numberOfPlayers,
                NumberOfRounds = numberOfRounds
            };

            games.Add(game);
            await Groups.AddToGroupAsync(Context.ConnectionId, hostId);
            await Clients.Caller.SendAsync("GameCreated", game);
        }

        public async Task JoinGame(string playerId, string playerName, string hostId)
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
                await Groups.AddToGroupAsync(Context.ConnectionId, hostId);

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
            await Clients.Group(game.HostId).SendAsync("GameStarted", game);
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

        public async Task PlayDomino(string hostId, string playerId, int value1, int value2, bool toHead)
        {
            var game = games.FirstOrDefault(g => g.HostId == hostId);
            var player = game?.Players.FirstOrDefault(p => p.ConnectionId == playerId);

            if (game != null && player != null)
            {
                var domino = player.Hand.FirstOrDefault(d => (d.Value1 == value1 && d.Value2 == value2) || (d.Value1 == value2 && d.Value2 == value1));

                if (domino != null)
                {
                    bool canPlace = false;

                    if (toHead)
                    {
                        var headValue = game.Board.First.Value.Value1;
                        if (domino.CanBePlacedNextTo(headValue))
                        {
                            if (domino.Value1 == headValue)
                            {
                                game.Board.AddFirst(domino);
                            }
                            else
                            {
                                game.Board.AddFirst(new Domino(domino.Value2, domino.Value1));
                            }
                            canPlace = true;
                        }
                    }
                    else
                    {
                        var tailValue = game.Board.Last.Value.Value2;
                        if (domino.CanBePlacedNextTo(tailValue))
                        {
                            if (domino.Value2 == tailValue)
                            {
                                game.Board.AddLast(domino);
                            }
                            else
                            {
                                game.Board.AddLast(new Domino(domino.Value2, domino.Value1));
                            }
                            canPlace = true;
                        }
                    }

                    if (canPlace)
                    {
                        player.Hand.Remove(domino);
                        await Clients.Group(hostId).SendAsync("DominoPlayed", game, player, domino);

                        if (player.Hand.Count == 0)
                        {
                            player.Score += game.Players.Sum(p => p.Hand.Sum(d => d.TotalValue));
                            await Clients.Group(hostId).SendAsync("RoundEnded", game, player);

                            if (game.CurrentRound < game.NumberOfRounds)
                            {
                                game.CurrentRound++;
                                await StartGame(game);
                            }
                            else
                            {
                                await Clients.Group(hostId).SendAsync("GameEnded", game);
                            }
                        }
                    }
                }
            }
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }

}
