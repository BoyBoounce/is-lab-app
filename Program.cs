using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var notes = new List<Note>();
var nextId = 1;

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        time = DateTime.UtcNow
    });
});

app.MapGet("/version", (IConfiguration config) =>
{
    return Results.Ok(new
    {
        app = config["App:Name"] ?? "IsLabApp",
        version = config["App:Version"] ?? "0.0.0",
        environment = app.Environment.EnvironmentName
    });
});

app.MapGet("/api/notes", () =>
{
    return Results.Ok(notes);
});

app.MapGet("/api/notes/{id:int}", (int id) =>
{
    var note = notes.FirstOrDefault(n => n.Id == id);
    return note is null
        ? Results.NotFound(new { message = "Note not found" })
        : Results.Ok(note);
});

app.MapPost("/api/notes", (CreateNoteRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { message = "Title is required" });
    }

    if (request.Title.Length > 200)
    {
        return Results.BadRequest(new { message = "Title must be 200 characters or less" });
    }

    var note = new Note(
        nextId++,
        request.Title.Trim(),
        string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim(),
        DateTime.UtcNow
    );

    notes.Add(note);

    return Results.Created($"/api/notes/{note.Id}", note);
});

app.MapDelete("/api/notes/{id:int}", (int id) =>
{
    var note = notes.FirstOrDefault(n => n.Id == id);

    if (note is null)
    {
        return Results.NotFound(new { message = "Note not found" });
    }

    notes.Remove(note);
    return Results.Ok(new { message = "Note deleted" });
});

app.MapGet("/db/ping", async (IConfiguration config) =>
{
    var connectionString = config.GetConnectionString("Postgres");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem("Connection string 'Postgres' is not configured.");
    }

    try
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT 1;", conn);
        var result = await cmd.ExecuteScalarAsync();

        return Results.Ok(new
        {
            status = "ok",
            database = "PostgreSQL",
            result
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database connection failed: {ex.Message}");
    }
});

app.Run();

record Note(int Id, string Title, string? Text, DateTime CreatedAt);

record CreateNoteRequest(string Title, string? Text);