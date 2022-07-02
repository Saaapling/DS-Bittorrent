﻿using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;

namespace BTProtocol.BitTorrent
{

    enum Events : UInt16
    {
        started = 0,
        paused  = 1,
        stopped = 2,
    }

    [Serializable()]
    struct TFData
    {
        public string torrent_name { get; }
        public string resource_path { get; }

        public byte[] piece_hash { get; }
        public long piece_size { get; }

        public bool[] pieces_downloaded;
        public uint bytes_uploaded;
        public uint bytes_downloaded;
        public UInt64 bytes_left;


        public Events _event;
        public bool compact;

        public TFData(string torrent_name, string resource_path, byte[] piece_hash, long piece_size)
        {
            this.torrent_name = torrent_name;
            this.resource_path = resource_path;
            this.piece_hash = piece_hash;
            this.piece_size = piece_size;

            pieces_downloaded = new bool[piece_hash.Length];
            bytes_uploaded = 0;
            bytes_downloaded = 0;
            bytes_left = Convert.ToUInt64(piece_size * piece_hash.Length);
            _event = Events.started;
            compact = true;
        }
    }
    
    class MainProc
    {
        const string resource_path = @"../../Resources/";
        const string serailized_path = resource_path + "TorrentData/";
        static Dictionary<String, TFData> torrent_file_dict = new Dictionary<string, TFData>();

        static BencodeParser parser = new BencodeParser();

        private string UrlSafeStringInfohash(byte[] Infohash)
        {
            return Encoding.UTF8.GetString(WebUtility.UrlEncodeToBytes(Infohash, 0, 20));
        }

        static void Main(string[] args)
        {
            /*
             Iterates through the torrent files in / resources, creating new serialized
             files for each torrent if they don't exist, and cleaning up old serialized
             files whose torrents have been removed
            */

            string[] torrent_files = Directory.GetFiles(resource_path);
            List<string> torrent_data_files = new List<string>(Directory.GetFiles(serailized_path));
            List<Tracker> trackers = new List<Tracker>();
            foreach (string file in torrent_files)
            {
                string torrent_name = file.Split('/').Last();
                torrent_name = torrent_name.Substring(0, torrent_name.Length - 8);
                string torrent_filedata_path = serailized_path + torrent_name;
                if (File.Exists(torrent_filedata_path))
                {
                    Stream openFileStream = File.OpenRead(torrent_filedata_path);
                    BinaryFormatter deserializer = new BinaryFormatter();
                    TFData file_data = (TFData)deserializer.Deserialize(openFileStream);
                    torrent_file_dict.Add(torrent_name, file_data);
                    Console.WriteLine("Torrent found: " + torrent_name);
                    torrent_data_files.Remove(torrent_filedata_path);
                } else
                {
                    Torrent torrent_file = parser.Parse<Torrent>(file);
                    TFData test_data = new TFData(torrent_name, file, torrent_file.Pieces, torrent_file.PieceSize);

                    Console.WriteLine("Creating new TFData serialized object: " + torrent_name);
                    Stream SaveFileStream = File.Create(serailized_path + torrent_name);
                    BinaryFormatter serializer = new BinaryFormatter();
                    serializer.Serialize(SaveFileStream, test_data);
                    SaveFileStream.Close();
                }
            }

            // Delete serialized files for removed torrents
            foreach (string file in torrent_data_files)
            {
                string torrent_name = file.Split('/').Last();
                Console.WriteLine("Removing serialized torrent data: " + torrent_name);
                File.Delete(file);
            }

            foreach (TFData entry in torrent_file_dict.Values){
                Torrent torrent_file = parser.Parse<Torrent>(entry.resource_path);
                Tracker tracker = new Tracker(torrent_file);
                trackers.Add(tracker);
                tracker.SendRecvToTracker(entry);
            }
        }

    }
}
