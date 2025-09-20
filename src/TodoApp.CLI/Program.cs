using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using TodoApp.DAL;

namespace TodoApp.CLI
{
    internal static class Program
    {
        private static readonly string DatabaseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "todo.db");

        private static TodoRepository _repository;

        private static void Main(string[] args)
        {
            var factory = new TodoContextFactory(DatabaseFilePath);
            factory.EnsureDatabaseCreated();
            _repository = new TodoRepository(factory);

            PrintWelcome();

            bool exitRequested = false;
            while (!exitRequested)
            {
                PrintMenu();
                Console.Write("Select an option: ");
                var input = Console.ReadLine();
                Console.WriteLine();

                switch ((input ?? string.Empty).Trim())
                {
                    case "1":
                        ListTodos();
                        break;
                    case "2":
                        CreateTodo();
                        break;
                    case "3":
                        CompleteTodo();
                        break;
                    case "4":
                        DeleteTodo();
                        break;
                    case "0":
                        exitRequested = true;
                        break;
                    default:
                        Console.WriteLine("Unknown option. Please select one of the available commands.");
                        break;
                }

                if (!exitRequested)
                {
                    Console.WriteLine();
                }
            }

            Console.WriteLine("Goodbye!");
        }

        private static void PrintWelcome()
        {
            Console.WriteLine("==============================");
            Console.WriteLine("        TODO LIST (CLI)        ");
            Console.WriteLine("==============================");
            Console.WriteLine();
        }

        private static void PrintMenu()
        {
            Console.WriteLine("1) List todo items");
            Console.WriteLine("2) Add a new todo item");
            Console.WriteLine("3) Complete a todo item");
            Console.WriteLine("4) Delete a todo item");
            Console.WriteLine("0) Exit");
        }

        private static void ListTodos()
        {
            try
            {
                var items = ExecuteAsync(() => _repository.GetAllAsync());
                if (items.Count == 0)
                {
                    Console.WriteLine("No todo items found. Add your first task!");
                    return;
                }

                Console.WriteLine("Current todo items:");
                foreach (var item in items)
                {
                    var status = item.IsCompleted ? "[x]" : "[ ]";
                    var createdAt = item.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
                    Console.WriteLine($"{status} {item.Id}: {item.Title} (Created {createdAt})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read todo items: {ex.Message}");
            }
        }

        private static void CreateTodo()
        {
            Console.Write("Enter a title for the todo item: ");
            var title = Console.ReadLine();

            try
            {
                var entity = ExecuteAsync(() => _repository.AddAsync(title ?? string.Empty));
                Console.WriteLine($"Created todo item #{entity.Id}: {entity.Title}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create todo item: {ex.Message}");
            }
        }

        private static void CompleteTodo()
        {
            Console.Write("Enter the id of the todo item to complete: ");
            var input = Console.ReadLine();
            if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                Console.WriteLine("The id must be a valid integer value.");
                return;
            }

            try
            {
                var updated = ExecuteAsync(() => _repository.MarkCompletedAsync(id));
                Console.WriteLine(updated ? "Todo item marked as completed." : "Todo item not found.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update todo item: {ex.Message}");
            }
        }

        private static void DeleteTodo()
        {
            Console.Write("Enter the id of the todo item to delete: ");
            var input = Console.ReadLine();
            if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                Console.WriteLine("The id must be a valid integer value.");
                return;
            }

            try
            {
                var removed = ExecuteAsync(() => _repository.DeleteAsync(id));
                Console.WriteLine(removed ? "Todo item deleted." : "Todo item not found.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete todo item: {ex.Message}");
            }
        }

        private static T ExecuteAsync<T>(Func<Task<T>> action)
        {
            return action().GetAwaiter().GetResult();
        }
    }
}
