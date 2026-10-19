using BlackjackApp.Hubs;
using BlackjackApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<GameService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.MapRazorPages();
app.MapBlazorHub();
app.MapHub<BlackjackHub>("/blackjackhub");
app.MapHealthChecks("/healthz");
app.MapFallbackToPage("/_Host");

app.Run();
