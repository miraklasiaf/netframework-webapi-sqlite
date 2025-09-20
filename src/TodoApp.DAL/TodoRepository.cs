using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace TodoApp.DAL
{
    public class TodoRepository
    {
        private readonly TodoContextFactory _contextFactory;

        public TodoRepository(TodoContextFactory contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<IReadOnlyList<TodoItem>> GetAllAsync()
        {
            using (var context = _contextFactory.CreateDbContext())
            {
                return await context.TodoItems
                    .OrderBy(t => t.IsCompleted)
                    .ThenByDescending(t => t.CreatedAtUtc)
                    .AsNoTracking()
                    .ToListAsync()
                    .ConfigureAwait(false);
            }
        }

        public async Task<TodoItem> AddAsync(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A todo item requires a non-empty title.", nameof(title));
            }

            using (var context = _contextFactory.CreateDbContext())
            {
                var entity = new TodoItem
                {
                    Title = title.Trim(),
                    CreatedAtUtc = DateTime.UtcNow,
                    IsCompleted = false
                };

                await context.TodoItems.AddAsync(entity).ConfigureAwait(false);
                await context.SaveChangesAsync().ConfigureAwait(false);
                return entity;
            }
        }

        public async Task<bool> MarkCompletedAsync(int id)
        {
            using (var context = _contextFactory.CreateDbContext())
            {
                var entity = await context.TodoItems.FirstOrDefaultAsync(t => t.Id == id).ConfigureAwait(false);
                if (entity == null)
                {
                    return false;
                }

                if (!entity.IsCompleted)
                {
                    entity.IsCompleted = true;
                    await context.SaveChangesAsync().ConfigureAwait(false);
                }

                return true;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using (var context = _contextFactory.CreateDbContext())
            {
                var entity = await context.TodoItems.FirstOrDefaultAsync(t => t.Id == id).ConfigureAwait(false);
                if (entity == null)
                {
                    return false;
                }

                context.TodoItems.Remove(entity);
                await context.SaveChangesAsync().ConfigureAwait(false);
                return true;
            }
        }
    }
}
