using System.IO;
using UnityEngine;

namespace Crossroads.Core
{
    /// <summary>
    /// Resolves save files under Application.persistentDataPath/crossroads/
    /// (design §12.1: JSON, slot files + autosave; survives app restarts and re-installs of a new build).
    /// </summary>
    public class PersistentDataPathProvider : IPathProvider
    {
        private readonly string _dir;

        public PersistentDataPathProvider(string subFolder = "crossroads")
        {
            string root = Application.persistentDataPath;
            _dir = string.IsNullOrEmpty(subFolder) ? root : Path.Combine(root, subFolder);
        }

        public string Directory { get { return _dir; } }

        public string Resolve(string fileName)
        {
            return Path.Combine(_dir, fileName);
        }
    }
}
