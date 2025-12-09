using System.Collections.ObjectModel;
using System.Xml.Serialization;
using Microsoft.Maui.Storage;
using System.Text;

namespace EPBugTracker
{
    public class UserItem
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public static class UserStore
    {
        static readonly string dataFilePath = Path.Combine(FileSystem.AppDataDirectory, "users.xml");

        public static ObservableCollection<UserItem> Users { get; } = new();

        static UserStore()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(dataFilePath)) return;
                var xs = new XmlSerializer(typeof(List<UserItem>));
                using var fs = File.OpenRead(dataFilePath);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                var items = (List<UserItem>?)xs.Deserialize(reader);
                if (items != null)
                {
                    Users.Clear();
                    foreach (var u in items) Users.Add(u);
                }
            }
            catch
            {
                // ignore
            }
        }

        public static void Save()
        {
            try
            {
                var list = Users.ToList();
                var xs = new XmlSerializer(typeof(List<UserItem>));
                var dir = Path.GetDirectoryName(dataFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using var fs = File.Create(dataFilePath);
                using var writer = new StreamWriter(fs, Encoding.UTF8);
                xs.Serialize(writer, list);
            }
            catch
            {
                // ignore
            }
        }

        public static void AddOrUpdate(UserItem user)
        {
            var existing = Users.FirstOrDefault(u => string.Equals(u.Email, user.Email, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Name = user.Name;
            }
            else
            {
                Users.Add(user);
            }
            Save();
        }

        public static void Remove(UserItem user)
        {
            Users.Remove(user);
            Save();
        }

        public static UserItem? FindByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            return Users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        }
    }
}
