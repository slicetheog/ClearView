// FILE: Logic/ClipboardHistoryManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ClearView.Logic
{
    public static class ClipboardHistoryManager
    {
        private static readonly ReaderWriterLockSlim _lock = new();
        private static readonly LinkedList<string> _history = new();
        private const int MaxItems = 50;
        private static readonly string PersistPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClearView", "clipboard_history.json");

        static ClipboardHistoryManager()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PersistPath) ?? string.Empty);
                LoadFromDisk();
            }
            catch { /* ignore */ }
        }

        public static void AddEntry(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _lock.EnterWriteLock();
            try
            {
                // normalize line endings and trim
                var normalized = text.Replace("\r\n", "\n").Trim();
                if (string.IsNullOrEmpty(normalized)) return;

                // remove duplicates (case-sensitive)
                var node = _history.FirstOrDefault(x => x == normalized);
                if (node != null)
                {
                    _history.Remove(node);
                }

                _history.AddFirst(normalized);
                while (_history.Count > MaxItems)
                    _history.RemoveLast();
                // persist asynchronously
                Task.Run(() => SaveToDisk());
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public static List<string> GetHistory(int max = MaxItems)
        {
            _lock.EnterReadLock();
            try
            {
                return _history.Take(max).ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private static void SaveToDisk()
        {
            try
            {
                _lock.EnterReadLock();
                var list = _history.ToArray();
                using var ms = new MemoryStream();
                var ser = new DataContractJsonSerializer(typeof(string[]));
                ser.WriteObject(ms, list);
                File.WriteAllBytes(PersistPath, ms.ToArray());
            }
            catch { /* ignore save errors */ }
            finally { if (_lock.IsReadLockHeld) _lock.ExitReadLock(); }
        }

        private static void LoadFromDisk()
        {
            try
            {
                if (!File.Exists(PersistPath)) return;
                var bytes = File.ReadAllBytes(PersistPath);
                using var ms = new MemoryStream(bytes);
                var ser = new DataContractJsonSerializer(typeof(string[]));
                var arr = (string[]?)ser.ReadObject(ms); // CHANGED: Explicitly define as nullable to resolve CS8600 warning.
                if (arr == null) return;
                _lock.EnterWriteLock();
                try
                {
                    _history.Clear();
                    foreach (var s in arr.Reverse())
                        _history.AddFirst(s);
                }
                finally { _lock.ExitWriteLock(); }
            }
            catch { /* ignore load errors */ }
        }
    }
}