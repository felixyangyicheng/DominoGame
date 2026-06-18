# 🀄 DominoGame

Jeu de dominos multijoueur en temps réel propulsé par **ASP.NET Core SignalR** sur **.NET 11 Preview 5**.

---

## Stack

| Composant | Technologie |
|---|---|
| Runtime | .NET 11.0 (preview) |
| Langage | C# 15 (preview) |
| Temps réel | SignalR (WebSocket) |
| Docs API | Swashbuckle / Swagger |
| Conteneurisation | Docker (Alpine) |
| CI/CD | Jenkins (Blue/Green) |

---

## Architecture

```
Client (WebSocket) ──→ SignalR Hub (/game) ──→ Game Logic (In-Memory)
                              │
                              ├── CreateGame    → Host crée une salle
                              ├── JoinGame      → Les joueurs rejoignent
                              ├── PlayDomino    → Pose d'un domino (tête/queue)
                              ├── PassTurn      → Passe son tour (bloqué)
                              └── GetGame       → Reconnexion / rafraîchissement
```

- **Stockage** : 100% en mémoire (`List<Game>` statique)
- **Concurrence** : `System.Threading.Lock` (.NET 9+)
- **Diffusion** : Groupes SignalR (`HostId`)

---

## Démarrage rapide

### Docker

```bash
docker build -f Dockerfile -t domino_game_srv DominoGame.srv/DominoGame.srv
docker run -d -p 5000:80 domino_game_srv
```

### Local (SDK .NET 11 requis)

```bash
cd DominoGame.srv/DominoGame.srv
dotnet run
```

Puis ouvrir `http://localhost:5000/swagger`

---

## Endpoints

| Chemin | Méthode | Description |
|---|---|---|
| `/game` | WebSocket | Hub SignalR (jeu en temps réel) |
| `/health` | GET | Health check (`{"status":"UP"}`) |
| `/swagger` | GET | Documentation Swagger UI |

### Méthodes Hub

| Méthode | Paramètres | Description |
|---|---|---|
| `CreateGame` | `numberOfPlayers (2-6)`, `maxPip (6-9)` | Crée une partie, retourne le `hostId` |
| `JoinGame` | `playerName`, `hostId` | Rejoint une partie |
| `PlayDomino` | `hostId`, `value1`, `value2`, `toHead` | Pose un domino |
| `PassTurn` | `hostId` | Passe son tour |
| `GetGame` | `hostId` | Récupère l'état complet (reconnexion) |

### Événements client

`GameCreated` · `GameJoined` · `GameStarted` · `GameUpdated` · `DominoPlayed` · `TurnPassed` · `GameEnded` · `InvalidMove` · `GameError` · `PlayerDisconnected`

---

## Règles du jeu

- **Variante Block Domino** : pas de pioche, on passe si on ne peut pas jouer
- **Fin de partie** : un joueur vide sa main **ou** blocage total
- **Score** : le gagnant empoche la somme des pips restants des autres joueurs
- **Blocage** : le joueur avec le moins de pips restants gagne

---

## Changelog

### v2.0.0 — .NET 11 Preview 5 + Corrections

> **⚠️ Breaking** : `Game.Status` passe de `string` à `GameStatus` enum (sérialisé en `"Waiting"/"Playing"/"Ended"` via `JsonStringEnumConverter` — compatible client)

#### 🔥 Performances
- `object` → `System.Threading.Lock` : verrouillage plus rapide (~10-20x sur les chemins async)
- `GenerateDominoes` : `[with(capacity: N)]` (C# 15) pré-alloue la taille exacte
- `Random.Shared.Shuffle<T>(Span<T>)` (.NET 9) remplace le Fisher-Yates manuel

#### 🛡️ Corrections de bugs
- **🚨 Reconnexion** : `JoinGame` retrouve le joueur par nom après déconnexion, met à jour `ConnectionId`
- **🚨 Fuite mémoire** : nettoyage automatique des parties terminées (>5 min) dans `CreateGame`
- **⚠️ Race condition** : `GetGame` lit `Games` sous `lock`
- **🟡 Doublons de nom** : `JoinGame` refuse un nom déjà pris dans la salle

#### ✨ Modernisation C# 13/15
- `LangVersion` → `preview`
- Collection expressions : tous les `new List<T>()` → `[]`
- `PlayerStatistic` : primary constructor (C# 12)
- `MoveResult` : `file sealed class` (C# 11)
- Événements : constants `Events.*` au lieu de chaînes magiques
- `GameStatus` : `enum` typé avec `JsonStringEnumConverter`

#### 🧹 Nettoyage
- Suppression de `Room.cs`, `WeatherForecast.cs`, `.http` (code mort)
- Suppression de `Domino.CanBePlacedNextTo()` (non utilisé)
- `using System.Threading.Tasks` redondant retiré de `Global.cs`

#### 🚀 CI/CD
- `Jenkinsfile` : pipeline Blue/Green (ports 5000-5003)
- `Program.cs` : endpoint `/health` pour health check
- Paquets mis à jour : Swashbuckle 10.2.1, SignalRSwaggerGen 4.9.0, Microsoft.OpenApi 2.9.0

---

### v1.0.0 — Version initiale

- Hub SignalR avec logique de jeu complète
- Génération et distribution des dominos
- Pose tête/queue, validation des tours, fin de partie
- Reconnexion basique (`GetGame`)
- Swagger UI + SignalRSwaggerGen
- Docker multi-stage (Alpine)

---

## Licence

MIT
