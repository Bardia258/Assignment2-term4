using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MEMS.Public;
using MEMS.Public.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddPublicSiteServices();

// Data access goes through interfaces (Services/Api). Until the real MEMS.Api is merged,
// mock implementations backed by in-memory sample data are registered here.
builder.Services.AddMockElectionApi();

await builder.Build().RunAsync();
