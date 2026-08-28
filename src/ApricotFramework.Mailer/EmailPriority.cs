namespace ApricotFramework.Mailer;

/// <summary>
/// The importance a message claims for itself.
/// </summary>
/// <remarks>
/// A hint only. Whether a client sorts, flags, or ignores it is the recipient's decision, and
/// transport that has no concept of priority leaves it out.
/// </remarks>
public enum EmailPriority
{
    /// <summary>
    /// The highest priority.
    /// </summary>
    Highest = 1,

    /// <summary>
    /// Above normal priority.
    /// </summary>
    High = 2,

    /// <summary>
    /// The priority a message carries when it says nothing.
    /// </summary>
    Normal = 3,

    /// <summary>
    /// Below normal priority.
    /// </summary>
    Low = 4,

    /// <summary>
    /// The lowest priority.
    /// </summary>
    Lowest = 5
}
