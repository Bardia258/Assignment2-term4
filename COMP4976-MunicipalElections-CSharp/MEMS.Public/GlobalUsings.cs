// Single place that chooses where the election DTOs come from (applies to .cs and .razor files).
// Swap to `MEMS.Shared.Contracts` and delete TempModels/ once the backend branch is merged.
global using MEMS.Public.TempModels;

// Compile-time log messages (Services/Logging/Log.cs) available as ILogger extension methods everywhere.
global using MEMS.Public.Services.Logging;
