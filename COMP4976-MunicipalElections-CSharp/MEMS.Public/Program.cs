using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MEMS.Public;
using MEMS.Public.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Services.AddPublicSiteServices();
builder.Services.AddElectionApi(builder.Configuration);

await builder.Build().RunAsync();
