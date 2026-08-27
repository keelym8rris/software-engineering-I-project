using Blackjack.Web.Components;
using Blackjack.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Shared server-side registry for local prototype lobbies.
builder.Services.AddSingleton<InMemoryLobbyService>();
builder.Services.AddSingleton<BlackjackGameService>();

// One session object per Blazor circuit/browser connection.
builder.Services.AddScoped<PlayerSessionService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
