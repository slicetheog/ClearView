using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClearView.Data
{
    public class ExclusionSettings
    {
        public List<string> ExcludedFolders { get; set; } = new List<string>();
        public List<string> ExcludedExtensions { get; set; } = new List<string>();
        public List<string> ExcludedFileTypes { get; set; } = new List<string>();

        public bool IsExcluded(string path)
        {
            // CHANGED: Add a specific check to exclude the "Personal Vault" folder by name.
            if (Path.GetFileName(path).Equals("Personal Vault", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ExcludedFolders.Any(folder => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }
            
            if (ExcludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) || 
                ExcludedFileTypes.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}