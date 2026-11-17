namespace MEMS.Data.Services;

public sealed class RecordConflictException(string message) : Exception(message);
