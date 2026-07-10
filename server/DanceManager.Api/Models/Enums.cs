namespace DanceManager.Api.Models;

public enum PayType
{
    Hourly,
    PerHeadcount
}

public enum AttendanceStatus
{
    Present,
    Absent,
    Excused
}

public enum OrderStatus
{
    NotOrdered,
    Ordered,
    Shipped,
    Delivered
}

public enum Gender
{
    Boys,
    Girls
}

public enum AuditionDecision
{
    Undecided,
    Yes,
    No
}

public enum ProgressStatus
{
    NotStarted,
    InProgress,
    Mastered
}

public enum MembershipRole
{
    Owner,
    Member
}
