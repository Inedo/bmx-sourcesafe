using System;
using System.Collections.Generic;
using Inedo.BuildMaster.Files;

namespace Inedo.BuildMasterExtensions.SourceSafe
{
    internal sealed class SsNode
    {
        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the files.
        /// </summary>
        public FileEntryInfo[] Files { get; private set; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name
        {
            get
            {
                string trimmedPath = Path.TrimEnd('/');
                if (trimmedPath.Contains("/"))
                    return trimmedPath.Substring(trimmedPath.LastIndexOf('/') + 1);
                else if (string.IsNullOrEmpty(Path))
                    return string.Empty;
                else
                    return "$";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SsNode"/> class.
        /// </summary>
        public SsNode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SsNode"/> class.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="files">The files.</param>
        public SsNode(string path, string[] files)
        {
            Path = "";
            if (!string.IsNullOrEmpty(path)) Path = path.TrimEnd('/');
            List<FileEntryInfo> fileList = new List<FileEntryInfo>();
            foreach (string file in files)
            {
                fileList.Add(new FileEntryInfo(
                    file.Substring(file.LastIndexOf('/')),
                    path.TrimEnd('/') + '/' + file));
            }
            this.Files = fileList.ToArray();
        }

        /// <summary>
        /// Determines whether [is child of] [the specified possible parent].
        /// </summary>
        /// <param name="possibleParent">The possible parent.</param>
        public bool IsChildOf(SsNode possibleParent)
        {
            return possibleParent.Path.StartsWith(this.Path, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
