using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.Audio;
using Discord.Net;
using Discord.Commands;
using VideoLibrary;
using System.Collections;
using System.IO;
using System.Xml;

namespace Discord_Bot_Functionalities
{
    class AudioStream : DiscordBotFunctions
    {
        private bool _stopAudio;
        private IAudioClient _audio;
        private bool _changeStation;
        private string _musicFolder;

        #region Constructors

        public void SetAudioClient(IAudioClient audio)
        {
            _audio = audio;
        }

        public void SetStopCall(bool StopCall)
        {
            _stopAudio = StopCall;
        }

        public void ChangeStation(bool Change)
        {
            _changeStation = Change;
        }

        private void SetMusicFolder(string Folder)
        {
            _musicFolder = Folder;
        }

        #endregion

        #region YoutubeStream

        // Downloads youtube video and adds it to the queue
        public async Task Download(string Url, CommandEventArgs e)
        {
            YouTube _TubeClient = YouTube.Default;
            YouTubeVideo Video = _TubeClient.GetVideo(Url);

            string Title = Video.Title;
            string FullName = Video.FullName;
            byte[] bytes = Video.GetBytes();

            await AddItem(FullName.Replace(' ', '_'), bytes, e, Title);
        }

        // Streaming service for youtube audio stream
        public async Task YoutubeStream(string pathOrUrl, CommandEventArgs e)
        {
                Process process = Process.Start(new ProcessStartInfo
                { // FFmpeg requires us to spawn a process and hook into its stdout, so we will create a Process
                    FileName = "ffmpeg",
                    Arguments = $"-i {pathOrUrl} " + // Here we provide a list of arguments to feed into FFmpeg. -i means the location of the file/URL it will read from
                            "-f s16le -ar 48000 -ac 2 pipe:1", // Next, we tell it to output 16-bit 48000Hz PCM, over 2 channels, to stdout.
                    UseShellExecute = false,
                    RedirectStandardOutput = true // Capture the stdout of the process
                });
                Thread.Sleep(2000); // Sleep for a few seconds to FFmpeg can start processing data.

                int blockSize = 3840; // The size of bytes to read per frame; 1920 for mono
                byte[] buffer = new byte[blockSize];
                int byteCount;

                while (true) // Loop forever, so data will always be read
                {
                    byteCount = process.StandardOutput.BaseStream // Access the underlying MemoryStream from the stdout of FFmpeg
                            .Read(buffer, 0, blockSize); // Read stdout into the buffer

                    int breaklimit = 0;
                    while (byteCount == 0 /*&& breaklimit != 5*/) // counter for failed attempts and sleeps so ffmpeg can read more audio
                    {
                        Thread.Sleep(2500);
                        breaklimit++;
                    }


                    _audio.Send(buffer, 0, byteCount); // Send our data to Discord
                    if (breaklimit == 6) // when the breaklimit reaches 6 failed attempts its fair to say that ffmpeg has either crashed or is finished with the song
                    {
                        break; // breaks the audio stream
                    }
                }
                _audio.Wait(); // Wait for the Voice Client to finish sending data, as ffMPEG may have already finished buffering out a song, and it is unsafe to return now.

                await NextSong(e); // starts the stream for the next song

            }

        #endregion

        #region QueueHandler
        private static Queue MusicQueue = new Queue();
        private static int CurrentIndex;

        // Adds an item to the queue
        public async Task AddItem(string FullName, byte[] bytes, CommandEventArgs e, string Name)
        {
            // Fires when no musicfolder has been set
            if (_musicFolder == null || _audio == null)
                throw new MissingMemberException();

            int Count = MusicQueue.Count;

            // Unassigned character is E022 used as a seperator this is chosen viewed results may vary
            string SaveItemName = $"{Count}{FullName}";

            MusicQueue.Enqueue(SaveItemName);
            File.WriteAllBytes(_musicFolder + "\\" + SaveItemName, bytes);
            await e.Channel.SendMessage($"{Name} Has been added to the queue");

        }

        // Gets the next song in the queue
        public async Task NextSong(CommandEventArgs e)
        {
            object[] MusicArray = MusicQueue.ToArray();

            string CurrentItem = MusicArray.ElementAt(CurrentIndex).ToString();

            string FileDirectory = _musicFolder + "\\" + CurrentItem;

            string NextSong = CurrentItem.Split('').Last().ToString();

            await e.Channel.SendMessage($"Now Playing {NextSong}");
            CurrentIndex++;

            await YoutubeStream(FileDirectory, e);

        }
        #endregion

        #region RadioStream

        // Streaming service for the radio stream
        public async Task RadioStream(string pathOrUrl, CommandEventArgs e)
        {
            // Fires when no IAudioClient has been found
            if (_audio == null)
                throw new MissingMemberException();

            // Runs ffmpeg in another thread so it does not block non async methods like Createcommands effectively blocking everything
            new Thread(() =>
            {
                Process process = Process.Start(new ProcessStartInfo
                {
                    // FFmpeg requires us to spawn a process and hook into its stdout, so we will create a Process
                    FileName = "ffmpeg",
                    Arguments =
                        $"-i {pathOrUrl} -y " +
                        // Here we provide a list of arguments to feed into FFmpeg. -i means the location of the file/URL it will read from
                        "-f s16le -ar 48000 -ac 2 pipe:1",
                    // Next, we tell it to output 16-bit 48000Hz PCM, over 2 channels, to stdout.
                    UseShellExecute = false,
                    RedirectStandardOutput = true // Capture the stdout of the process
                });
                Thread.Sleep(2000); // Sleep for a few seconds so FFmpeg can start processing data.

                int blockSize = 3840; // The size of bytes to read per frame; 1920 for mono
                byte[] buffer = new byte[blockSize];
                int byteCount;

                while (true) // Loop forever, so data will always be read
                {
                    byteCount = process.StandardOutput.BaseStream
                        // Access the underlying MemoryStream from the stdout of FFmpeg
                        .Read(buffer, 0, blockSize); // Read stdout into the buffer

                    while (byteCount == 0)
                    {
                        Thread.Sleep(2500);
                    }
                    // Call from leave command consider making boolean a method and making it return
                    if (_stopAudio)
                    {
                        Process[] Processes = Process.GetProcessesByName("ffmpeg");
                            // gets all processes called ffmpeg if more than one instance of ffmpeg is present unstable effects WILL occur
                        if (Processes.Length != 0)
                        {
                            foreach (Process Proc in Processes)
                            {
                                Proc.Kill(); // gets the first process called ffmpeg
                            }
                            _audio.Disconnect(); // leaves the audio channel
                        }
                        else
                            throw new ArgumentNullException();
                        _stopAudio = false; // resets the soundstopcall

                    }
                    // call to change station, kills ffmpeg process to free up pipes otherwise the pipe will break or overflow
                    if (_changeStation)
                    {
                        // gets all processes named ffmpeg.
                        Process[] Processes = Process.GetProcessesByName("ffmpeg"); // gets all processes called ffmpeg
                        if (Processes.Length != 0)
                        {
                            // there is a possibility that there are multiple ffmpeg processes running kills them all
                            foreach (Process Proc in Processes)
                            {
                                Proc.Kill(); // kills the process
                            }
                        }
                        _changeStation = false; // resets the changestation call

                    }

                    _audio.Send(buffer, 0, byteCount); // Send our data to Discord
                }

                //Program._audio.Wait(); // Wait for the Voice Client to finish sending data, as ffMPEG may have already finished buffering out a song, and it is unsafe to return now.
            }).Start();
        }

        // Returns all installed Radio Stations
        protected static Dictionary<string, string> GetRadioStations()
        {
            string xml = Path.GetFullPath("RadioStreams.xml");
            if (xml == null)
                throw new NullReferenceException();

            XmlDocument doc = new XmlDocument();
            doc.Load(xml);

            Dictionary<string, string> Dictionary = new Dictionary<string, string>();
            foreach (XmlNode Node in doc.ChildNodes.Item(1))
            {
                Dictionary.Add(Node.Name, Node.InnerText);
            }

            return Dictionary;

        }

        #endregion
    }
}

