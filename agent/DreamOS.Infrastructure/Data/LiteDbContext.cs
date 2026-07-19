using System.IO;
using LiteDB;

namespace DreamOS.Infrastructure.Data
{
    public class LiteDbContext
    {
        public LiteDatabase Database { get; }

        public LiteDbContext()
        {
            var folder = @"C:\Users\Admin\.gemini\antigravity";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            var dbPath = Path.Combine(folder, "dreamos.db");
            Database = new LiteDatabase(dbPath);
        }
    }
}
