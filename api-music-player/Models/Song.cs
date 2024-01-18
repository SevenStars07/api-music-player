namespace api_music_player.Models;

public class Song
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Album { get; set; }
    // public List<string> Features { get; set; }
    public int Duration { get; set; }
    public string FileName { get; set; }
    public string Url { get; set; }
}