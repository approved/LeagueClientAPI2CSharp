using System;
using System.IO;
using System.Text;

namespace LCAPI2CSharp
{
    public class LeagueClientInfo
    {
        public string ProcessName = string.Empty;
        public int ProcessId;
        public int ProcessPort;
        public string Password = string.Empty;
        public string ComProtocol = string.Empty;

        private LeagueClientInfo() { }

        public static LeagueClientInfo FromFile (FileInfo file)
        {
            if (!file.Exists)
            {
                throw new FileNotFoundException("Could not find the lockfile responsible for communication data.");
            }

            string fileData = string.Empty;
            using(FileStream fs = File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] buffer = new byte[fs.Length];
                fs.Read(buffer, 0, (int)fs.Length);
                fileData = Encoding.UTF8.GetString(buffer);
            }

            return FromString(fileData);
        }

        public static LeagueClientInfo FromString(string data)
        { 
            if(string.IsNullOrEmpty(data))
            {
                throw new ArgumentException($"{nameof(data)} was {(data is null ? "null" : "empty")}.");
            }

            return FromStringArray(data.Split(":", StringSplitOptions.RemoveEmptyEntries));
        }

        public static LeagueClientInfo FromStringArray(string[] info)
        {
            if (info.Length < 5)
            {
                ThrowParseFailed(nameof(info));
            }

            LeagueClientInfo lci = new LeagueClientInfo
            {
                ProcessName = info[0],
                Password = info[3],
                ComProtocol = info[4]
            };

            if (!int.TryParse(info[1], out lci.ProcessId))
            {
                ThrowParseFailed(nameof(ProcessId));
            }
            if (!int.TryParse(info[2], out lci.ProcessPort))
            {
                ThrowParseFailed(nameof(ProcessPort));
            }
            return lci;
        }

        private static void ThrowParseFailed(string fieldName)
        {
            throw new InvalidDataException($"Unable to parse {fieldName} from lockfile.");
        }
    }
}
