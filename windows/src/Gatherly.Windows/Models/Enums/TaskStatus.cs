namespace Gatherly.Windows.Models.Enums;

public enum TaskStatus
{
    pending,
    recognizing,
    parsing,
    downloading,
    completed,
    failed
}

public static class TaskStatusExtensions
{
    public static string ToRawValue(this TaskStatus s) => s.ToString();

    public static TaskStatus FromRawValue(string value) =>
        Enum.TryParse<TaskStatus>(value, true, out var result) ? result : TaskStatus.pending;
}
