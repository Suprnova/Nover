using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using SpotifyAPI.Web;
using TagLib;

namespace Nover
{
    class Program
    {
        public static class Globals
        {
            public static string SpotifyID = "<REDACTED>";
            public static string SpotifySecret = "<REDACTED>";
            public static SpotifyClient Spotify;
            public static string docPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Nover");
            public static string Song;
        }

        private static Timer timer;

        static async Task Main(string[] args)
        {
            Console.WriteLine("---Nover v0.1---");
            string docPath = Globals.docPath;
            if (!Directory.Exists(docPath))
            {
                Console.WriteLine("Creating Nover folder in the Documents folder...");
                Directory.CreateDirectory(docPath);
            }
            Console.WriteLine("Directory found!");
            if (!System.IO.File.Exists(Path.Combine(docPath, "settings.ini")))
            {
                Console.WriteLine("Settings file not found...");
                CreateSettings();
            }
            Console.WriteLine("Settings file found!");
            ConfigureSpotify();
            SetTimer();
            System.Threading.Thread.Sleep(-1);
        }

        private static void CreateSettings()
        {
            Directory.CreateDirectory(Globals.docPath);
            Console.WriteLine($"Creating a settings.ini file in {Path.Combine(Globals.docPath, "settings.ini")}");
            System.IO.File.WriteAllText(Path.Combine(Globals.docPath, "settings.ini"), "// Path for your local files folder\nLocal files directory: \n // Path for the image to show when no album cover is available, must be a .jpg\nNot playing/empty album art: \n// How often the program will check if the song updated in milliseconds, defaults to 1000, min of 100, max of 10000 \nSong check delay: 1000 \n// Format for the text file, supports {title} and {artist} \nText format: {title} by {artist}");
        }

        private static void ConfigureSpotify()
        {
            Console.WriteLine("Configuring Spotify...");
            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new ClientCredentialsAuthenticator(Globals.SpotifyID, Globals.SpotifySecret)).WithRetryHandler(new SimpleRetryHandler() { RetryAfter = TimeSpan.FromSeconds(1) });
            Globals.Spotify = new SpotifyClient(config);
            Console.WriteLine("Configured!");
        }

        private static void SetTimer()
        {
            double delay = Convert.ToDouble(Settings("Delay"));
            timer = new Timer(delay);
            timer.Elapsed += CheckPlayer;
            timer.AutoReset = false;
            timer.Enabled = true;
        }

        private static async void CheckPlayer(Object source, ElapsedEventArgs e)
        {
            try
            {
                FileInfo fi = new FileInfo(Path.Combine(Globals.docPath, "cover.jpg"));
                if (fi.Length == 0)
                {
                    SetCoverEmpty();
                }
            }
            catch { };
            var spotify = Process.GetProcessesByName("Spotify").FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle));
            if (spotify.MainWindowTitle == Globals.Song)
            {
                SetTimer();
                return;
            }
            if (spotify == null)
            {
                await UpdatePlayer(false, null);
                return;
            }
            if (string.Equals(spotify.MainWindowTitle, "Spotify", StringComparison.InvariantCultureIgnoreCase) || string.Equals(spotify.MainWindowTitle, "Spotify Premium", StringComparison.InvariantCultureIgnoreCase))
            {
                await UpdatePlayer(false, null);
                return;
            }
            await UpdatePlayer(true, spotify.MainWindowTitle);
        }

        private static async Task UpdatePlayer(bool isPlaying, string song)
        {
            string docPath = Globals.docPath;
            if (isPlaying)
            {
                string songName = song.Trim().Substring(song.Trim().IndexOf(" - ") + 3).Trim();
                string artistName = song.Trim().Substring(0, song.Trim().Length - songName.Length - 2).Trim();
                string textFormat = Settings("Text");
                if (textFormat != null)
                {
                    textFormat = textFormat.Replace("{title}", songName);
                    textFormat = textFormat.Replace("{artist}", artistName);
                    System.IO.File.WriteAllText(Path.Combine(docPath, "Nover.txt"), textFormat);
                    Console.WriteLine(textFormat);
                }
                else
                {
                    System.IO.File.WriteAllText(Path.Combine(docPath, "Nover.txt"), song);
                    Console.WriteLine(song);
                }
                Globals.Song = song;
                await FetchCover(songName, artistName);
                SetTimer();
            }
            else
            {
                System.IO.File.WriteAllText(Path.Combine(docPath, "Nover.txt"), "Not Playing");
                SetCoverEmpty();
                SetTimer();
            }
        }

        private static void LocalFile(string song, string artist)
        {
            string path = Settings("Local");
            string[] files = { "null" };
            try
            {
                files = Directory.GetFiles(path, "*.m*", SearchOption.TopDirectoryOnly);
            }
            catch { };
            foreach (string file in files)
            {
                if (file == "null")
                {
                    continue;
                }
                var tFile = TagLib.File.Create(file);
                if (tFile.Tag.Title == song)
                {
                    IPicture cover = tFile.Tag.Pictures.First();
                    IPicture[] covers = tFile.Tag.Pictures;
                    foreach (IPicture image in covers)
                    {
                        if (cover.Data.Data.Length < image.Data.Data.Length)
                        {
                            cover = image;
                        }
                    }
                    MemoryStream ms = new MemoryStream(cover.Data.Data);
                    FileStream fs = new FileStream(Path.Combine(Globals.docPath, "cover.jpg"), FileMode.Create, FileAccess.Write);
                    ms.WriteTo(fs);
                    // ensures the file fully saves before closing the stream
                    System.Threading.Thread.Sleep(200);
                    fs.Close();
                    ms.Close();
                    return;
                }
            }
            SetCoverEmpty();
            Console.Write("");
        }
        private static async Task FetchCover(string song, string artist)
        {
            song = song.Replace("\"", "");
            artist = artist.Replace("\"", "");
            song = song.Replace("\'", "");
            artist = artist.Replace("\'", "");
            var search = await Globals.Spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"track:\"{song}\" artist:\"{artist}\""));
            int i = 0;
            if (search.Tracks.Items.Count == 0)
            {
                LocalFile(song, artist);
            }
            await foreach (var item in Globals.Spotify.Paginate(search.Tracks, (s) => s.Tracks))
            {
                if (item.Name == song || item.Artists.First().Name == artist)
                {
                    WebClient wc = new WebClient();
                    wc.DownloadFile(item.Album.Images.First().Url, Path.Combine(Globals.docPath, "cover.jpg"));
                    break;
                }
                if (i >= search.Tracks.Items.Count)
                {
                    LocalFile(song, artist);
                    break;
                }
                i++;
                if (i > 100)
                {
                    // too many results to bother
                    LocalFile(song, artist);
                    break;
                }
            }
        }

        private static void SetCoverEmpty()
        {
            string docPath = Globals.docPath;
            string coverPath = Settings("Cover");
            if (!string.IsNullOrWhiteSpace(coverPath))
            {
                System.IO.File.Copy(coverPath, Path.Combine(docPath, "cover.jpg"), true);
            }
            else
            {
                System.IO.File.Delete(Path.Combine(docPath, "cover.jpg"));
            }
        }

        private static string Settings(string option)
        {
            string docPath = Globals.docPath;
            var settings = System.IO.File.ReadAllLines(Path.Combine(docPath, "settings.ini"));
            if (option == "Cover")
            {
                foreach (var line in settings)
                {
                    if (line.StartsWith("//"))
                    {
                        continue;
                    }
                    else if (line.StartsWith("Not playing/empty album art:"))
                    {
                        string lineReplaced = line.Replace("Not playing/empty album art:", "");
                        return lineReplaced.Trim();
                    }
                }
                Console.WriteLine("settings.ini is corrupt! Creating a new settings.ini file...");
                CreateSettings();
                return null;
            }
            else if (option == "Local")
            {
                foreach (var line in settings)
                {
                    if (line.StartsWith("//"))
                    {
                        continue;
                    }
                    else if (line.StartsWith("Local files directory:"))
                    {
                        string lineReplaced = line.Replace("Local files directory:", "");
                        return lineReplaced.Trim();
                    }
                }
                Console.WriteLine("settings.ini is corrupt! Creating a new settings.ini file...");
                CreateSettings();
                return null;
            }
            else if (option == "Delay")
            {
                foreach (var line in settings)
                {
                    if (line.StartsWith("//"))
                    {
                        continue;
                    }
                    else if (line.StartsWith("Song check delay:"))
                    {
                        string lineReplaced = line.Replace("Song check delay:", "");
                        double delay = Convert.ToDouble(lineReplaced);
                        if (delay != null && delay >= 100 && delay <= 10000)
                        {
                            string delayString = Convert.ToString(delay);
                            return delayString;
                        }
                        else
                        {
                            string delayString = "1000";
                            return delayString;
                        }
                    }
                }
                Console.WriteLine("settings.ini is corrupt! Creating a new settings.ini file...");
                CreateSettings();
                return null;
            }
            else if (option == "Text")
            {
                foreach (var line in settings)
                {
                    if (line.StartsWith("//"))
                    {
                        continue;
                    }
                    else if (line.StartsWith("Text format:"))
                    {
                        string lineReplaced = line.Replace("Text format: ", "");
                        return lineReplaced.Trim();
                    }
                }
                Console.WriteLine("settings.ini is corrupt! Creating a new settings.ini file...");
                CreateSettings();
                return null;
            }
            return null;
        }
    }
}
