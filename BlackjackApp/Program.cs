using BlackjackApp.Hubs;
using BlackjackApp.Services;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = true;
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<GameService>();

var app = builder.Build();

app.UseStaticFiles();

app.MapBlazorHub();
app.MapHub<BlackjackHub>("/blackjackhub");
app.MapFallbackToPage("/_Host");

app.Run();
