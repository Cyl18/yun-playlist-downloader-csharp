namespace YunPlaylistDownloader.Models;

public class Song
{
    public string Singer { get; set; } = string.Empty;
    public string SongName { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool? IsFreeTrial { get; set; }
    public string? Extension { get; set; }
    public string Index { get; set; } = string.Empty;
    public int RawIndex { get; set; }
    public object? RawData { get; set; }
}

public class Playlist
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<TrackData> Tracks { get; set; } = new();
}

public class Album
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PicUrl { get; set; }
    public List<TrackData> Songs { get; set; } = new();
    public ArtistData Artist { get; set; } = new();
}

public class DjRadio
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PicUrl { get; set; }
    public string Desc { get; set; } = string.Empty;
}

public class DjProgram
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public int SerialNum { get; set; }
    public MainSong MainSong { get; set; } = new();
}

public class TrackData
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ArtistData> Ar { get; set; } = new();
    public AlbumData Al { get; set; } = new();
    public int Dt { get; set; } // Duration
    public SongPlayUrlInfo? PlayUrlInfo { get; set; }
}

public class ArtistData
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AlbumData
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PicUrl { get; set; }
}

public class MainSong
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ArtistData> Artists { get; set; } = new();
    public AlbumData Album { get; set; } = new();
    public SongPlayUrlInfo? PlayUrlInfo { get; set; }
}

public class SongPlayUrlInfo
{
    public long Id { get; set; }
    public string? Url { get; set; }
    public int Br { get; set; } // Bitrate
    public long Size { get; set; }
    public string? Md5 { get; set; }
    public int Code { get; set; }
    public int Expi { get; set; }
    public string? Type { get; set; }
    public int Gain { get; set; }
    public int Fee { get; set; }
    public object? FreeTrialInfo { get; set; }
    public string? Message { get; set; }
}

public class DownloadOptions
{
    public string Url { get; set; } = string.Empty;
    public int Concurrency { get; set; } = 5;
    public string Format { get; set; } = ":name/:singer - :songName.:ext";
    public int Quality { get; set; } = 999;
    public int RetryTimeout { get; set; } = 3;
    public int RetryTimes { get; set; } = 3;
    public bool Skip { get; set; } = true;
    public bool Progress { get; set; } = true;
    public bool Cover { get; set; } = false;
    public string Cookie { get; set; } = "yun.cookie.txt";
    public bool SkipTrial { get; set; } = false;
}

public enum PageType
{
    Playlist,
    Album,
    DjRadio
}
