using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TodoApp.DAL;

namespace TodoApp.API
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

            using (var cancellationSource = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellationSource.Cancel();
                };

                Console.WriteLine("Starting TodoApp API on http://localhost:5000/ ... Press Ctrl+C to stop.");
                RunServerAsync(cancellationSource.Token).GetAwaiter().GetResult();
            }
        }

        private static async Task RunServerAsync(CancellationToken cancellationToken)
        {
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add("http://localhost:5000/");
                listener.Start();

                Console.WriteLine("TodoApp API is ready to accept requests.");

                using (cancellationToken.Register(() => listener.Stop()))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        HttpListenerContext context;
                        try
                        {
                            context = await listener.GetContextAsync().ConfigureAwait(false);
                        }
                        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Unhandled listener exception: {ex.Message}");
                            continue;
                        }

                        _ = Task.Run(() => ProcessRequestAsync(context), cancellationToken);
                    }
                }

                listener.Close();
            }
        }

        private static async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                var segments = request.Url?.AbsolutePath?.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

                if (segments.Length == 0)
                {
                    await WriteJsonAsync(response, new { message = "TodoApp API is running." }).ConfigureAwait(false);
                    return;
                }

                if (!string.Equals(segments[0], "todos", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteErrorAsync(response, HttpStatusCode.NotFound, "Resource not found.").ConfigureAwait(false);
                    return;
                }

                if (segments.Length == 1)
                {
                    switch (request.HttpMethod?.ToUpperInvariant())
                    {
                        case "GET":
                            await HandleGetTodosAsync(response).ConfigureAwait(false);
                            return;
                        case "POST":
                            await HandleCreateTodoAsync(request, response).ConfigureAwait(false);
                            return;
                        default:
                            await WriteMethodNotAllowedAsync(response, "GET, POST").ConfigureAwait(false);
                            return;
                    }
                }

                if (segments.Length == 2)
                {
                    if (!int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    {
                        await WriteErrorAsync(response, HttpStatusCode.BadRequest, "The todo id must be a valid integer.").ConfigureAwait(false);
                        return;
                    }

                    switch (request.HttpMethod?.ToUpperInvariant())
                    {
                        case "DELETE":
                            await HandleDeleteTodoAsync(response, id).ConfigureAwait(false);
                            return;
                        default:
                            await WriteMethodNotAllowedAsync(response, "DELETE").ConfigureAwait(false);
                            return;
                    }
                }

                if (segments.Length == 3 && string.Equals(segments[2], "complete", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    {
                        await WriteErrorAsync(response, HttpStatusCode.BadRequest, "The todo id must be a valid integer.").ConfigureAwait(false);
                        return;
                    }

                    if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteMethodNotAllowedAsync(response, "POST").ConfigureAwait(false);
                        return;
                    }

                    await HandleCompleteTodoAsync(response, id).ConfigureAwait(false);
                    return;
                }

                await WriteErrorAsync(response, HttpStatusCode.NotFound, "Resource not found.").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.Message}");
                try
                {
                    await WriteErrorAsync(context.Response, HttpStatusCode.InternalServerError, "An unexpected server error occurred.").ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        private static async Task HandleGetTodosAsync(HttpListenerResponse response)
        {
            var items = await _repository.GetAllAsync().ConfigureAwait(false);
            await WriteJsonAsync(response, items).ConfigureAwait(false);
        }

        private static async Task HandleCreateTodoAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            var payload = JsonConvert.DeserializeObject<CreateTodoRequest>(body ?? string.Empty) ?? new CreateTodoRequest();
            if (string.IsNullOrWhiteSpace(payload.Title))
            {
                await WriteErrorAsync(response, HttpStatusCode.BadRequest, "The todo title is required.").ConfigureAwait(false);
                return;
            }

            try
            {
                var item = await _repository.AddAsync(payload.Title).ConfigureAwait(false);
                response.StatusCode = (int)HttpStatusCode.Created;
                response.Headers["Location"] = $"/todos/{item.Id}";
                await WriteJsonAsync(response, item).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await WriteErrorAsync(response, HttpStatusCode.InternalServerError, ex.Message).ConfigureAwait(false);
            }
        }

        private static async Task HandleCompleteTodoAsync(HttpListenerResponse response, int id)
        {
            var updated = await _repository.MarkCompletedAsync(id).ConfigureAwait(false);
            if (!updated)
            {
                await WriteErrorAsync(response, HttpStatusCode.NotFound, "Todo item not found.").ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(response, new { message = "Todo item marked as completed." }).ConfigureAwait(false);
        }

        private static async Task HandleDeleteTodoAsync(HttpListenerResponse response, int id)
        {
            var deleted = await _repository.DeleteAsync(id).ConfigureAwait(false);
            if (!deleted)
            {
                await WriteErrorAsync(response, HttpStatusCode.NotFound, "Todo item not found.").ConfigureAwait(false);
                return;
            }

            response.StatusCode = (int)HttpStatusCode.NoContent;
            await WriteBodyAsync(response, string.Empty).ConfigureAwait(false);
        }

        private static Task WriteJsonAsync(HttpListenerResponse response, object value)
        {
            response.ContentType = "application/json";
            var json = JsonConvert.SerializeObject(value);
            return WriteBodyAsync(response, json);
        }

        private static Task WriteErrorAsync(HttpListenerResponse response, HttpStatusCode statusCode, string message)
        {
            response.StatusCode = (int)statusCode;
            return WriteJsonAsync(response, new { error = message });
        }

        private static Task WriteMethodNotAllowedAsync(HttpListenerResponse response, string allowedMethods)
        {
            response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            response.Headers["Allow"] = allowedMethods;
            return WriteJsonAsync(response, new { error = "Method not allowed." });
        }

        private static Task WriteBodyAsync(HttpListenerResponse response, string body)
        {
            var buffer = Encoding.UTF8.GetBytes(body ?? string.Empty);
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.LongLength;
            return response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private sealed class CreateTodoRequest
        {
            public string Title { get; set; }
        }
    }
}
