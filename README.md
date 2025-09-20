# TodoApp (.NET Framework 4.7)

This repository contains a simple todo list solution targeting **.NET Framework 4.7** with a shared SQLite database powered by **Entity Framework Core 3.1**. The solution includes:

- `TodoApp.DAL` – Entity Framework Core data access layer with the `TodoContext`, entities, and repository helpers.
- `TodoApp.CLI` – Console application for managing todo items directly from the terminal.
- `TodoApp.API` – Lightweight self-hosted HTTP API (based on `HttpListener`) that exposes the todo functionality over HTTP.

Both applications share the same SQLite database file located in an `App_Data/todo.db` folder created next to each executable.

## Projects

| Project        | Target        | Description |
|----------------|---------------|-------------|
| `TodoApp.DAL`  | `net471` class library | Defines the `TodoItem` entity, EF Core context, and repository used by other projects. |
| `TodoApp.CLI`  | `net471` console | Interactive command-line client for listing, adding, completing, and deleting todos. |
| `TodoApp.API`  | `net471` console | Self-hosted JSON API exposing `/todos` endpoints using the shared repository. |

## Running the console client

1. Restore NuGet packages (via Visual Studio or `nuget restore`).
2. Build the solution.
3. Run the `TodoApp.CLI` project. The menu-driven interface allows you to list, add, complete, and delete todo items.

## Running the HTTP API

1. Restore packages and build the solution.
2. Run the `TodoApp.API` project. The API listens on `http://localhost:5000/`.
3. Example requests:
   - `GET http://localhost:5000/todos` – list all todos.
   - `POST http://localhost:5000/todos` with JSON body `{ "title": "Buy milk" }` – create a todo.
   - `POST http://localhost:5000/todos/{id}/complete` – mark a todo as completed.
   - `DELETE http://localhost:5000/todos/{id}` – remove a todo.

Press **Ctrl+C** in the API console window to gracefully stop the server.

## Solution structure

The Visual Studio solution file `TodoApp.sln` references the three projects so they can be managed together. Both the CLI and API projects reference the shared data access layer to keep persistence logic centralized.
