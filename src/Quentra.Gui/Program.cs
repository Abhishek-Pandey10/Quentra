using System.Diagnostics;
using Quentra.Gui;
using Quentra.Infrastructure;

// A local, single-user web GUI over the same engine as the CLI. It listens on this machine only.
var port = 5178;
var openBrowser = true;
// Accepted runs and ETABS extractions are written here as they are made, so nothing reviewed exists only in memory.
var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Quentra");
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--no-browser") openBrowser = false;
    else if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[++i], out var p) && p is > 0 and < 65536) port = p;
    else if (args[i] == "--data-dir" && i + 1 < args.Length) dataDirectory = Path.GetFullPath(args[++i]);
    else
    {
        Console.Error.WriteLine("Usage: Quentra.Gui [--port <number>] [--no-browser] [--data-dir <folder>]");
        return 2;
    }
}

var builder = WebApplication.CreateBuilder();
builder.Configuration["AllowedHosts"] = "localhost;127.0.0.1"; // Refuses DNS-rebinding requests from other sites.
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
// The largest request is a run file opened from disk.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = SnapshotJson.MaximumRunBytes + 1024 * 1024);
var app = builder.Build();

string page;
await using (var stream = typeof(GuiSession).Assembly.GetManifestResourceStream("Quentra.Gui.index.html")!)
    page = await new StreamReader(stream).ReadToEndAsync();
var session = new GuiSession(dataDirectory, new LiveEtabsGateway());

app.MapGet("/", () => Results.Content(page, "text/html; charset=utf-8"));
app.MapGet("/api/template", () => Results.Json(new { snapshot = GuiSession.Template() }));
string format;
await using (var stream = typeof(GuiSession).Assembly.GetManifestResourceStream("Quentra.Docs.SNAPSHOT_FORMAT.md")!)
    format = await new StreamReader(stream).ReadToEndAsync();
app.MapGet("/format", () => Results.Text(format, "text/plain; charset=utf-8"));
app.MapGet("/api/runs", () => Results.Json(session.HeldRuns()));
app.MapGet("/api/runs/{id}", (string id) => Handle(() => session.Show(id)));
// Snapshot and run files are the request body itself: a large run cannot travel as one string inside JSON.
app.MapPost("/api/calculate", (HttpRequest r, CancellationToken t) => HandleBody(r, text => session.Calculate(text, t)));
app.MapPost("/api/open", (HttpRequest r, CancellationToken t) => HandleBody(r, text => session.Open(text, t)));
app.MapPost("/api/override", (OverrideRequest r, CancellationToken t) => Handle(() => session.Override(r, t)));
app.MapPost("/api/accept", (AcceptRequest r) => Handle(() => session.Accept(r)));
app.MapGet("/api/runs/{id}/file", (string id) =>
{
    try { var (json, name) = session.Download(id); return Results.File(json, "application/json", name); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapPost("/api/export", async (RunIdRequest r, CancellationToken t) =>
{
    try
    {
        var (zip, name) = await session.Export(r.RunId, t);
        return Results.File(zip, "application/zip", name);
    }
    catch (Exception e) when (IsInputError(e)) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapGet("/api/saved", () => Results.Json(new { folder = session.RunsDirectory, runs = session.SavedRuns() }));
app.MapPost("/api/saved/open", (SavedRunRequest r, CancellationToken t) => Handle(() => session.OpenSaved(r.Name, t)));
// ETABS calls block while ETABS works, so they run off the request thread; ETABS itself is only read.
app.MapGet("/api/etabs/status", async (int? pid) => Results.Json(await Task.Run(() => session.EtabsStatus(pid))));
app.MapPost("/api/etabs/extract", async (EtabsExtractRequest r, CancellationToken t) =>
{
    try { return Results.Json(await Task.Run(() => session.EtabsExtract(r, t), t)); }
    catch (Exception e) when (IsInputError(e)) { return Results.BadRequest(new { error = e.Message }); }
});
app.MapPost("/api/etabs/check", async (EtabsCheckRequest r, CancellationToken t) =>
{
    try { return Results.Json(await Task.Run(() => session.EtabsCheck(r, t), t)); }
    catch (Exception e) when (IsInputError(e)) { return Results.BadRequest(new { error = e.Message }); }
});

try { await app.StartAsync(); }
catch (IOException e)
{
    Console.Error.WriteLine($"Quentra GUI could not listen on port {port}: {e.Message} Try --port with another number.");
    return 1;
}
var url = $"http://127.0.0.1:{port}/";
Console.WriteLine($"Quentra GUI: {url}");
Console.WriteLine($"Accepted runs are saved in {session.RunsDirectory}; ETABS extractions in {session.ExtractionsDirectory}.");
Console.WriteLine("Draft runs are kept in memory while this runs; download run.json to keep one. Press Ctrl+C to stop.");
if (openBrowser)
{
    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
    catch (Exception) { Console.WriteLine("Open the address above in a browser."); }
}
await app.WaitForShutdownAsync();
return 0;

// Requiring the JSON media type means another website cannot post here without a CORS preflight, which is refused.
static async Task<IResult> HandleBody(HttpRequest request, Func<string, RunResponse> action)
{
    if (request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true)
        return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
    string text;
    try
    {
        using var reader = new StreamReader(request.Body, new System.Text.UTF8Encoding(false, true));
        text = (await reader.ReadToEndAsync(request.HttpContext.RequestAborted)).TrimStart('\uFEFF');
    }
    catch (System.Text.DecoderFallbackException) { return Results.BadRequest(new { error = "The file is not UTF-8 text. Save it as a UTF-8 JSON file." }); }
    return Handle(() => action(text));
}

static IResult Handle(Func<RunResponse> action)
{
    try { return Results.Json(action()); }
    catch (Exception e) when (IsInputError(e)) { return Results.BadRequest(new { error = e.Message }); }
}

// The same failures the CLI reports as input errors (exit code 1); anything else is a defect and is not hidden.
static bool IsInputError(Exception e) => e is ArgumentException or InvalidDataException or NotSupportedException or System.Text.Json.JsonException;
