﻿using BeatSyncConsole.Utilities;
using System;
using System.IO;
using Newtonsoft.Json;
using static BeatSyncConsole.Utilities.Paths;
using Newtonsoft.Json.Converters;

namespace BeatSyncConsole.Configs
{
    public class CustomSongLocation : ISongLocation
    {

        public static void SetDefaultPaths(CustomSongLocation songLocation)
        {
            songLocation.SongsDirectory = songLocation.BasePath;
            songLocation.PlaylistDirectory = null;
            songLocation.HistoryPath = "BeatSyncHistory.json";
        }

        public static CustomSongLocation CreateEmptyLocation()
        {
            return new CustomSongLocation()
            {
                BasePath = string.Empty,
                PlaylistDirectory = "Playlists",
                HistoryPath = "BeatSyncHistory.json"
            };
        }
        [JsonConstructor]
        internal CustomSongLocation() { }
        
        public CustomSongLocation(string basePath)
        {
            if (string.IsNullOrEmpty(basePath))
                throw new ArgumentNullException(nameof(basePath));
            BasePath = basePath;
            SetDefaultPaths(this);
        }
        [JsonIgnore]
        private string? _songsDirectory;
        [JsonIgnore]
        private string? _historyPath;
        [JsonIgnore]
        private bool? _unzipBeatmaps;

        [JsonProperty("Enabled", Order = 0)]
        public bool Enabled { get; set; }

        [JsonProperty("BasePath", Order = 5)]
        public string BasePath { get; set; } = string.Empty;

        [JsonProperty("SongsDirectory", Order = 15)]
        public string SongsDirectory
        {
            get => _songsDirectory ?? BasePath;
            set
            {
                _songsDirectory = value;
            }
        }

        [JsonProperty("PlaylistDirectory", Order = 20)]
        public string? PlaylistDirectory { get; set; }

        [JsonProperty("HistoryPath", Order = 25)]
        public string HistoryPath
        {
            get => _historyPath ??= Path.Combine(BasePath, "BeatSyncHistory.json");
            set
            {
                _historyPath = value;
            }
        }


        [JsonProperty("UnzipBeatmaps", Order = 30)]
        public bool UnzipBeatmaps
        {
            get { return _unzipBeatmaps ??= true; }
            set { _unzipBeatmaps = value; }
        }


        public override string ToString()
        {
            if (Enabled)
                return $"Custom: {ReplaceWorkingDirectory(BasePath)}";
            else
                return $"(Disabled) Custom: {ReplaceWorkingDirectory(BasePath)}";
        }

        public bool IsValid(out string? reason)
        {
            if (string.IsNullOrEmpty(BasePath))
            {
                reason = "Path is empty.";
                return false;
            }
            reason = null;
            return true;
        }

        public bool IsValid() => IsValid(out _);
    }
}
