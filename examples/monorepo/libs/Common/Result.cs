namespace Common;

public record Result<T>(bool Success, T? Value, string? Error);
