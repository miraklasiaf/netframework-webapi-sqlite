using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TodoApp.DAL
{
    public class TodoContextFactory
    {
        private readonly string _connectionString;

        public TodoContextFactory(string databaseFilePath)
        {
            if (string.IsNullOrWhiteSpace(databaseFilePath))
            {
                throw new ArgumentException("A valid database file path must be provided.", nameof(databaseFilePath));
            }

            var directory = Path.GetDirectoryName(databaseFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databaseFilePath
            };

            _connectionString = builder.ToString();
        }

        public TodoContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseSqlite(_connectionString)
                .Options;

            return new TodoContext(options);
        }

        public void EnsureDatabaseCreated()
        {
            using (var context = CreateDbContext())
            {
                context.Database.EnsureCreated();
            }
        }
    }
}
