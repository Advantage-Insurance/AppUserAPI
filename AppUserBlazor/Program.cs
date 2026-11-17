using AppUserBlazor.Components;
using AppUserBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddHttpClient("AppUserAPI", ConfigureAppUserApiClient)
    .ConfigurePrimaryHttpMessageHandler(CreateAppUserApiHandler);

builder.Services.AddScoped<ApplicationUserService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static void ConfigureAppUserApiClient(HttpClient client)
{
    client.BaseAddress = new Uri("https://localhost:7098");
}

static HttpMessageHandler CreateAppUserApiHandler()
{
    return new HttpClientHandler
    {
        UseDefaultCredentials = true
    };
}