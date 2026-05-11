using DominoGame.srv.Models;
using JiuLing.CommonLibs;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;

namespace DominoGame.srv.Hubs
{
    public class GameHub : Hub
    {
        private static readonly object Locker = new();
        private static readonly List<Game> Games = new();

        [HubMethodName("CreateGame")]
        public async Task<int> CreateGame(int numberOfPlayers, int maxPip)
        {
            numberOfPlayers = Math.Clamp(numberOfPlayers, 2, 6);
            maxPip = Math.Clamp(maxPip, 6, 9);

            var totalDominoes = CountDominoes(maxPip);
            var dominosPerPlayer = Math.Max(1, totalDominoes / numberOfPlayers);
            int hostId;
            Game game;

            lock (Locker)
            {
                do
                {
                    hostId = Convert.ToInt32(RandomUtils.GetOneByLength(4));
                } while (Games.Any(x => x.HostId == hostId));

                game = new Game
                {
                    HostId = hostId,
                    NumberOfPlayers = numberOfPlayers,
                    MaxPip = maxPip,
                    TotalDominoes = totalDominoes,
                    DominosPerPlayer = dominosPerPlayer,
                    BoneyardCount = totalDominoes - dominosPerPlayer * numberOfPlayers
                };

                Games.Add(game);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, game.HostId.ToString());
            await Clients.Caller.SendAsync("GameCreated", game);
            return hostId;
        }

        [HubMethodName("JoinGame")]
        public async Task JoinGame(string playerName, int hostId)
        {
            Game? game;
            Player? player;

            lock (Locker)
            {
                game = Games.FirstOrDefault(g => g.HostId == hostId);
                if (game is null)
                {
                    player = null;
                }
                else if (game.Status != "Waiting")
                {
                    player = game.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                }
                else
                {
                    player = game.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                    if (player is null && !game.IsFull)
                    {
                        player = new Player
                        {
                            ConnectionId = Context.ConnectionId,
                            Name = string.IsNullOrWhiteSpace(playerName) ? $"Joueur {game.Players.Count + 1}" : playerName.Trim()
                        };
                        game.Players.Add(player);
                    }
                }
            }

            if (game is null)
            {
                await Clients.Caller.SendAsync("GameError", "Salle introuvable.");
                return;
            }

            if (player is null)
            {
                await Clients.Caller.SendAsync("GameError", game.IsFull ? "La salle est deja pleine." : "La partie a deja commence.");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, game.HostId.ToString());
            await Clients.Group(game.HostId.ToString()).SendAsync("GameJoined", game);

            if (game.IsFull && game.Status == "Waiting")
            {
                await StartGame(game);
            }
        }

        [HubMethodName("PlayDomino")]
        public async Task PlayDomino(int hostId, int value1, int value2, bool toHead)
        {
            var result = TryPlayDomino(hostId, Context.ConnectionId, value1, value2, toHead);
            await PublishMoveResult(result);
        }

        [HubMethodName("PassTurn")]
        public async Task PassTurn(int hostId)
        {
            var result = TryPassTurn(hostId, Context.ConnectionId);
            await PublishMoveResult(result);
        }

        [HubMethodName("GetGame")]
        public async Task GetGame(int hostId)
        {
            var game = Games.FirstOrDefault(g => g.HostId == hostId);
            if (game is null)
            {
                await Clients.Caller.SendAsync("GameError", "Salle introuvable.");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, hostId.ToString());
            await Clients.Caller.SendAsync("GameUpdated", game);
        }

        private async Task StartGame(Game game)
        {
            lock (Locker)
            {
                var dominoes = GenerateDominoes(game.MaxPip)
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList();

                game.Board.Clear();
                game.Statistics.Clear();
                game.Status = "Playing";
                game.CurrentPlayerIndex = 0;
                game.ValueFirst = null;
                game.ValueLast = null;
                game.WinnerConnectionId = null;
                game.WinnerName = null;
                game.EndReason = null;

                foreach (var player in game.Players)
                {
                    player.Hand = dominoes.Take(game.DominosPerPlayer).ToList();
                    player.PlayedCount = 0;
                    player.PassedCount = 0;
                    player.Score = 0;
                    dominoes = dominoes.Skip(game.DominosPerPlayer).ToList();
                }

                game.BoneyardCount = dominoes.Count;
            }

            await Clients.Group(game.HostId.ToString()).SendAsync("GameStarted", game);
        }

        private MoveResult TryPlayDomino(int hostId, string? playerId, int value1, int value2, bool toHead)
        {
            lock (Locker)
            {
                var validation = ValidateTurn(hostId, playerId);
                if (validation.Error is not null)
                {
                    return validation;
                }

                var game = validation.Game!;
                var player = validation.Player!;
                var domino = player.Hand.FirstOrDefault(d =>
                    d.Value1 == value1 && d.Value2 == value2 ||
                    d.Value1 == value2 && d.Value2 == value1);

                if (domino is null)
                {
                    return MoveResult.Fail(game, "Cette piece n'est pas dans votre main.");
                }

                var placedDomino = BuildPlacedDomino(game, domino, toHead);
                if (placedDomino is null)
                {
                    return MoveResult.Fail(game, "Cette piece doit toucher la tete ou la queue de la chaine.");
                }

                placedDomino.PlaceOrder = game.Board.Count;
                if (toHead)
                {
                    game.Board.Insert(0, placedDomino);
                }
                else
                {
                    game.Board.Add(placedDomino);
                }

                player.Hand.Remove(domino);
                player.PlayedCount++;
                game.ValueFirst = game.Board.First().Value1;
                game.ValueLast = game.Board.Last().Value2;

                if (player.Hand.Count == 0)
                {
                    EndGame(game, player, "main vide");
                    return MoveResult.Success(game, "GameEnded");
                }

                AdvanceTurn(game);
                if (!AnyPlayerCanMove(game))
                {
                    EndGame(game, GetBlockedWinner(game), "blocage");
                    return MoveResult.Success(game, "GameEnded");
                }

                return MoveResult.Success(game, "DominoPlayed", player, placedDomino);
            }
        }

        private MoveResult TryPassTurn(int hostId, string? playerId)
        {
            lock (Locker)
            {
                var validation = ValidateTurn(hostId, playerId);
                if (validation.Error is not null)
                {
                    return validation;
                }

                var game = validation.Game!;
                var player = validation.Player!;
                if (CanPlayerMove(game, player))
                {
                    return MoveResult.Fail(game, "Vous avez encore une piece jouable.");
                }

                player.PassedCount++;
                AdvanceTurn(game);

                if (!AnyPlayerCanMove(game))
                {
                    EndGame(game, GetBlockedWinner(game), "blocage");
                    return MoveResult.Success(game, "GameEnded");
                }

                return MoveResult.Success(game, "TurnPassed", player);
            }
        }

        private MoveResult ValidateTurn(int hostId, string? playerId)
        {
            var game = Games.FirstOrDefault(g => g.HostId == hostId);
            if (game is null)
            {
                return MoveResult.Fail(null, "Salle introuvable.");
            }

            if (game.Status != "Playing")
            {
                return MoveResult.Fail(game, "La partie n'est pas en cours.");
            }

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == playerId);
            if (player is null)
            {
                return MoveResult.Fail(game, "Joueur introuvable.");
            }

            if (game.Players[game.CurrentPlayerIndex].ConnectionId != player.ConnectionId)
            {
                return MoveResult.Fail(game, "Ce n'est pas votre tour.");
            }

            return MoveResult.Success(game, "GameUpdated", player);
        }

        private static Domino? BuildPlacedDomino(Game game, Domino domino, bool toHead)
        {
            if (game.Board.Count == 0)
            {
                return new Domino(domino.Value1, domino.Value2);
            }

            if (toHead)
            {
                var head = game.ValueFirst;
                if (domino.Value2 == head)
                {
                    return new Domino(domino.Value1, domino.Value2);
                }

                if (domino.Value1 == head)
                {
                    return new Domino(domino.Value2, domino.Value1) { IsReversed = true };
                }
            }
            else
            {
                var tail = game.ValueLast;
                if (domino.Value1 == tail)
                {
                    return new Domino(domino.Value1, domino.Value2);
                }

                if (domino.Value2 == tail)
                {
                    return new Domino(domino.Value2, domino.Value1) { IsReversed = true };
                }
            }

            return null;
        }

        private static void AdvanceTurn(Game game)
        {
            game.CurrentPlayerIndex = (game.CurrentPlayerIndex + 1) % game.Players.Count;
        }

        private static bool AnyPlayerCanMove(Game game)
        {
            return game.Players.Any(player => CanPlayerMove(game, player));
        }

        private static bool CanPlayerMove(Game game, Player player)
        {
            if (game.Board.Count == 0)
            {
                return player.Hand.Count > 0;
            }

            return player.Hand.Any(d =>
                d.Value1 == game.ValueFirst ||
                d.Value2 == game.ValueFirst ||
                d.Value1 == game.ValueLast ||
                d.Value2 == game.ValueLast);
        }

        private static Player GetBlockedWinner(Game game)
        {
            return game.Players
                .OrderBy(p => p.RemainingPips)
                .ThenBy(p => p.Hand.Count)
                .First();
        }

        private static void EndGame(Game game, Player winner, string reason)
        {
            game.Status = "Ended";
            game.WinnerConnectionId = winner.ConnectionId;
            game.WinnerName = winner.Name;
            game.EndReason = reason;

            var totalRemainingPips = game.Players.Where(p => p.ConnectionId != winner.ConnectionId).Sum(p => p.RemainingPips);
            winner.Score += totalRemainingPips;

            game.Statistics = game.Players
                .OrderBy(p => p.RemainingPips)
                .ThenBy(p => p.Hand.Count)
                .Select(p => new PlayerStatistic
                {
                    PlayerName = p.Name,
                    Score = p.Score,
                    RemainingDominoes = p.Hand.Count,
                    RemainingPips = p.RemainingPips,
                    PlayedCount = p.PlayedCount,
                    PassedCount = p.PassedCount,
                    IsWinner = p.ConnectionId == winner.ConnectionId
                })
                .ToList();
        }

        private static List<Domino> GenerateDominoes(int maxPip)
        {
            var dominoes = new List<Domino>();
            for (var i = 0; i <= maxPip; i++)
            {
                for (var j = i; j <= maxPip; j++)
                {
                    dominoes.Add(new Domino(i, j));
                }
            }

            return dominoes;
        }

        private static int CountDominoes(int maxPip)
        {
            return (maxPip + 1) * (maxPip + 2) / 2;
        }

        private async Task PublishMoveResult(MoveResult result)
        {
            if (result.Error is not null)
            {
                await Clients.Caller.SendAsync("InvalidMove", result.Error);
                return;
            }

            if (result.Game is null)
            {
                return;
            }

            if (result.EventName == "DominoPlayed")
            {
                await Clients.Group(result.Game.HostId.ToString()).SendAsync(result.EventName, result.Game, result.Player, result.Domino);
                return;
            }

            if (result.EventName == "TurnPassed")
            {
                await Clients.Group(result.Game.HostId.ToString()).SendAsync(result.EventName, result.Game, result.Player);
                return;
            }

            await Clients.Group(result.Game.HostId.ToString()).SendAsync(result.EventName, result.Game);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Player? disconnectedPlayer = null;
            Game? game = null;

            lock (Locker)
            {
                game = Games.FirstOrDefault(g => g.Players.Any(p => p.ConnectionId == Context.ConnectionId));
                disconnectedPlayer = game?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                if (disconnectedPlayer is not null)
                {
                    disconnectedPlayer.IsConnected = false;
                }
            }

            if (game is not null && disconnectedPlayer is not null)
            {
                await Clients.Group(game.HostId.ToString()).SendAsync("PlayerDisconnected", disconnectedPlayer);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private sealed class MoveResult
        {
            public Game? Game { get; init; }
            public Player? Player { get; init; }
            public Domino? Domino { get; init; }
            public string EventName { get; init; } = "GameUpdated";
            public string? Error { get; init; }

            public static MoveResult Success(Game? game, string eventName, Player? player = null, Domino? domino = null)
            {
                return new MoveResult { Game = game, EventName = eventName, Player = player, Domino = domino };
            }

            public static MoveResult Fail(Game? game, string error)
            {
                return new MoveResult { Game = game, Error = error };
            }
        }
    }
}
