namespace DevAgent.Application.Abstractions.Messaging;

public static class JobTypes
{
    /// <summary>Advances a task through its next orchestration step.</summary>
    public const string ProcessTask = "task.process";

    /// <summary>Delivers one chat notification from the outbox, with retries.</summary>
    public const string SendNotification = "notification.send";
}
