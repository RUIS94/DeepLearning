namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// A FollowUpThread is open for the entire multi-round back-and-forth and closed exactly
    /// once, by the user's explicit "结束追问" action (CloseFollowUpThreadCommand) — never
    /// reopened. See FollowUpThread's own doc comment for why the submission stays
    /// under_dispute for the thread's whole open lifetime instead of bouncing back to Graded
    /// after every message.
    /// </summary>
    public enum FollowUpThreadStatus
    {
        open,
        closed
    }
}
