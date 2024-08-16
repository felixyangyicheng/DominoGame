namespace DominoGame.srv.Models
{
    public class Room
    {
        /// <summary>
        /// room name
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// Party host id
        /// </summary>
        public string HostId { get; set; } = "";
        /// <summary>
        /// current player
        /// </summary>
        public int CurrentPlayer { get; set; }

 
    }
}
